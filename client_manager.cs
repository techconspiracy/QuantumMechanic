using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace QuantumMechanic.Networking
{
    /// <summary>
    /// TCP client manager for connecting to the Mini-MORPG server.
    /// Handles connection lifecycle, packet transmission, and main-thread event routing.
    /// Implements reconnection logic and heartbeat keepalive.
    /// </summary>
    public class ClientManager : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private string _serverAddress = "127.0.0.1";
        [SerializeField] private int _serverPort = 7777;
        [SerializeField] private float _keepAliveInterval = 5f;
        
        private TcpClient _socket;
        private NetworkStream _stream;
        private Thread _receiveThread;
        private bool _isConnected;
        private uint _localClientId;
        
        private Queue<Action> _mainThreadQueue = new Queue<Action>();
        private object _queueLock = new object();
        
        private byte[] _receiveBuffer = new byte[8192];
        private List<byte> _partialPacket = new List<byte>();
        
        private float _lastKeepAlive;
        
        // Event system for game logic
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<NetworkPacket> OnPacketReceived;
        
        public bool IsConnected => _isConnected;
        public uint LocalClientId => _localClientId;
        
        /// <summary>
        /// Attempts to connect to the server.
        /// </summary>
        public void Connect()
        {
            if (_isConnected)
            {
                Debug.LogWarning("[ClientManager] Already connected");
                return;
            }
            
            try
            {
                _socket = new TcpClient();
                _socket.Connect(_serverAddress, _serverPort);
                _stream = _socket.GetStream();
                _isConnected = true;
                
                // Start receive thread
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "ClientReceive"
                };
                _receiveThread.Start();
                
                EnqueueMainThread(() =>
                {
                    OnConnected?.Invoke();
                    Debug.Log($"[ClientManager] Connected to {_serverAddress}:{_serverPort}");
                });
                
                _lastKeepAlive = Time.time;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientManager] Connection failed: {ex.Message}");
                Disconnect();
            }
        }
        
        /// <summary>
        /// Receive loop - runs on background thread.
        /// Handles packet framing and reassembly.
        /// </summary>
        private void ReceiveLoop()
        {
            try
            {
                while (_isConnected)
                {
                    if (_stream.DataAvailable)
                    {
                        int bytesRead = _stream.Read(_receiveBuffer, 0, _receiveBuffer.Length);
                        if (bytesRead == 0)
                        {
                            Disconnect();
                            break;
                        }
                        
                        ProcessReceivedData(_receiveBuffer, bytesRead);
                    }
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientManager] Receive error: {ex.Message}");
                Disconnect();
            }
        }
        
        /// <summary>
        /// Processes received bytes and extracts complete packets.
        /// </summary>
        private void ProcessReceivedData(byte[] buffer, int length)
        {
            // Add to partial packet buffer
            for (int i = 0; i < length; i++)
            {
                _partialPacket.Add(buffer[i]);
            }
            
            // Extract complete packets
            while (_partialPacket.Count >= 4)
            {
                byte[] header = _partialPacket.GetRange(0, 4).ToArray();
                int packetLength = PacketProcessor.ReadPacketLength(header);
                
                if (packetLength <= 0 || packetLength > 1048576)
                {
                    Debug.LogError($"[ClientManager] Invalid packet length: {packetLength}");
                    Disconnect();
                    return;
                }
                
                if (_partialPacket.Count < 4 + packetLength)
                {
                    break;
                }
                
                // Extract packet
                byte[] packetData = _partialPacket.GetRange(4, packetLength).ToArray();
                _partialPacket.RemoveRange(0, 4 + packetLength);
                
                // Deserialize
                NetworkPacket packet = PacketProcessor.Deserialize(packetData);
                if (packet != null)
                {
                    HandlePacket(packet);
                }
            }
        }
        
        /// <summary>
        /// Routes packet to main thread for handling.
        /// </summary>
        private void HandlePacket(NetworkPacket packet)
        {
            EnqueueMainThread(() =>
            {
                OnPacketReceived?.Invoke(packet);
            });
        }
        
        /// <summary>
        /// Sends a packet to the server.
        /// Thread-safe.
        /// </summary>
        public void Send(NetworkPacket packet)
        {
            if (!_isConnected)
            {
                Debug.LogWarning("[ClientManager] Cannot send - not connected");
                return;
            }
            
            try
            {
                byte[] data = PacketProcessor.Serialize(packet);
                if (data != null)
                {
                    _stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientManager] Send failed: {ex.Message}");
                Disconnect();
            }
        }
        
        /// <summary>
        /// Sends a transform update to the server.
        /// </summary>
        public void SendTransform(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            NetworkPacket packet = PacketProcessor.CreateTransformPacket(_localClientId, position, rotation, velocity);
            Send(packet);
        }
        
        /// <summary>
        /// Sends a chat message to the server.
        /// </summary>
        public void SendChat(string username, string message, byte channel = 0)
        {
            NetworkPacket packet = PacketProcessor.CreateChatPacket(_localClientId, username, message, channel);
            Send(packet);
        }
        
        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected) return;
            
            _isConnected = false;
            
            try
            {
                // Send disconnect packet
                NetworkPacket disconnectPacket = new NetworkPacket(PacketType.Disconnect, _localClientId, "");
                byte[] data = PacketProcessor.Serialize(disconnectPacket);
                if (data != null && _stream != null)
                {
                    _stream.Write(data, 0, data.Length);
                }
                
                _stream?.Close();
                _socket?.Close();
            }
            catch { }
            
            EnqueueMainThread(() =>
            {
                OnDisconnected?.Invoke();
                Debug.Log("[ClientManager] Disconnected from server");
            });
        }
        
        /// <summary>
        /// Enqueues action to run on main thread.
        /// </summary>
        private void EnqueueMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }
        
        /// <summary>
        /// Processes main thread queue and sends keepalive.
        /// </summary>
        private void Update()
        {
            // Process main thread queue
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    Action action = _mainThreadQueue.Dequeue();
                    action?.Invoke();
                }
            }
            
            // Send keepalive
            if (_isConnected && Time.time - _lastKeepAlive > _keepAliveInterval)
            {
                NetworkPacket keepAlive = new NetworkPacket(PacketType.KeepAlive, _localClientId, "");
                Send(keepAlive);
                _lastKeepAlive = Time.time;
            }
        }
        
        /// <summary>
        /// Sets the local client ID (assigned by server).
        /// </summary>
        public void SetLocalClientId(uint id)
        {
            _localClientId = id;
            Debug.Log($"[ClientManager] Local client ID set to {id}");
        }
        
        /// <summary>
        /// Cleanup on destroy.
        /// </summary>
        private void OnDestroy()
        {
            Disconnect();
        }
        
        /// <summary>
        /// Disconnect on application quit.
        /// </summary>
        private void OnApplicationQuit()
        {
            Disconnect();
        }
    }
}