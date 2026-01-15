using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace QuantumMechanic.Networking
{
    /// <summary>
    /// Multi-threaded TCP server host for the Mini-MORPG.
    /// Manages client connections, handles packet routing, and orchestrates world state.
    /// Thread-safe using lock mechanisms and Unity main-thread message queue.
    /// </summary>
    public class ServerHost : MonoBehaviour
    {
        [Header("Server Configuration")]
        [SerializeField] private int _port = 7777;
        [SerializeField] private int _maxConnections = 100;
        [SerializeField] private float _tickRate = 20f; // Server tick rate in Hz
        
        private TcpListener _listener;
        private Thread _listenerThread;
        private Thread _tickThread;
        private bool _isRunning;
        
        private Dictionary<uint, ClientConnection> _clients = new Dictionary<uint, ClientConnection>();
        private Queue<Action> _mainThreadQueue = new Queue<Action>();
        private object _queueLock = new object();
        private uint _nextClientId = 1;
        
        // Event system for game logic hooks
        public event Action<uint> OnClientConnected;
        public event Action<uint> OnClientDisconnected;
        public event Action<uint, NetworkPacket> OnPacketReceived;
        
        /// <summary>
        /// Represents a connected client with TCP socket and state tracking.
        /// </summary>
        private class ClientConnection
        {
            public uint ClientId;
            public TcpClient Socket;
            public NetworkStream Stream;
            public Thread ReceiveThread;
            public bool IsConnected;
            public DateTime LastKeepAlive;
            public byte[] ReceiveBuffer = new byte[8192];
            public List<byte> PartialPacket = new List<byte>();
        }
        
        /// <summary>
        /// Initializes the server when the component starts.
        /// </summary>
        private void Start()
        {
            StartServer();
        }
        
        /// <summary>
        /// Starts the TCP listener and tick thread.
        /// </summary>
        public void StartServer()
        {
            if (_isRunning) return;
            
            _isRunning = true;
            
            // Start TCP listener thread
            _listenerThread = new Thread(ListenForClients)
            {
                IsBackground = true,
                Name = "ServerListener"
            };
            _listenerThread.Start();
            
            // Start server tick thread
            _tickThread = new Thread(ServerTick)
            {
                IsBackground = true,
                Name = "ServerTick"
            };
            _tickThread.Start();
            
            Debug.Log($"[ServerHost] Server started on port {_port}");
        }
        
        /// <summary>
        /// Main listener loop - accepts incoming client connections.
        /// Runs on background thread.
        /// </summary>
        private void ListenForClients()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                
                while (_isRunning)
                {
                    if (_listener.Pending())
                    {
                        TcpClient client = _listener.AcceptTcpClient();
                        HandleNewClient(client);
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerHost] Listener error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles a newly connected client, assigns ID, starts receive thread.
        /// </summary>
        private void HandleNewClient(TcpClient socket)
        {
            if (_clients.Count >= _maxConnections)
            {
                socket.Close();
                Debug.LogWarning("[ServerHost] Max connections reached, rejected client");
                return;
            }
            
            uint clientId = _nextClientId++;
            ClientConnection client = new ClientConnection
            {
                ClientId = clientId,
                Socket = socket,
                Stream = socket.GetStream(),
                IsConnected = true,
                LastKeepAlive = DateTime.UtcNow
            };
            
            lock (_clients)
            {
                _clients[clientId] = client;
            }
            
            // Start receive thread for this client
            client.ReceiveThread = new Thread(() => ReceiveFromClient(client))
            {
                IsBackground = true,
                Name = $"ClientReceive_{clientId}"
            };
            client.ReceiveThread.Start();
            
            // Notify on main thread
            EnqueueMainThread(() =>
            {
                OnClientConnected?.Invoke(clientId);
                Debug.Log($"[ServerHost] Client {clientId} connected");
            });
        }
        
        /// <summary>
        /// Receive loop for a specific client. Handles packet framing and reassembly.
        /// Runs on background thread.
        /// </summary>
        private void ReceiveFromClient(ClientConnection client)
        {
            try
            {
                while (_isRunning && client.IsConnected)
                {
                    if (client.Stream.DataAvailable)
                    {
                        int bytesRead = client.Stream.Read(client.ReceiveBuffer, 0, client.ReceiveBuffer.Length);
                        if (bytesRead == 0)
                        {
                            DisconnectClient(client.ClientId);
                            break;
                        }
                        
                        ProcessReceivedData(client, client.ReceiveBuffer, bytesRead);
                    }
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerHost] Client {client.ClientId} receive error: {ex.Message}");
                DisconnectClient(client.ClientId);
            }
        }
        
        /// <summary>
        /// Processes raw bytes, handles packet framing with 4-byte size prefix.
        /// </summary>
        private void ProcessReceivedData(ClientConnection client, byte[] buffer, int length)
        {
            // Add to partial packet buffer
            for (int i = 0; i < length; i++)
            {
                client.PartialPacket.Add(buffer[i]);
            }
            
            // Try to extract complete packets
            while (client.PartialPacket.Count >= 4)
            {
                byte[] header = client.PartialPacket.GetRange(0, 4).ToArray();
                int packetLength = PacketProcessor.ReadPacketLength(header);
                
                if (packetLength <= 0 || packetLength > 1048576) // 1MB sanity check
                {
                    Debug.LogError($"[ServerHost] Invalid packet length: {packetLength}");
                    DisconnectClient(client.ClientId);
                    return;
                }
                
                if (client.PartialPacket.Count < 4 + packetLength)
                {
                    // Wait for more data
                    break;
                }
                
                // Extract complete packet
                byte[] packetData = client.PartialPacket.GetRange(4, packetLength).ToArray();
                client.PartialPacket.RemoveRange(0, 4 + packetLength);
                
                // Deserialize and handle
                NetworkPacket packet = PacketProcessor.Deserialize(packetData);
                if (packet != null)
                {
                    client.LastKeepAlive = DateTime.UtcNow;
                    HandlePacket(client.ClientId, packet);
                }
            }
        }
        
        /// <summary>
        /// Routes received packet to appropriate handler on main thread.
        /// </summary>
        private void HandlePacket(uint clientId, NetworkPacket packet)
        {
            EnqueueMainThread(() =>
            {
                OnPacketReceived?.Invoke(clientId, packet);
            });
        }
        
        /// <summary>
        /// Server tick loop - handles timeouts, keepalives, state synchronization.
        /// Runs on background thread.
        /// </summary>
        private void ServerTick()
        {
            float tickInterval = 1f / _tickRate;
            
            while (_isRunning)
            {
                DateTime startTime = DateTime.UtcNow;
                
                // Check for client timeouts
                lock (_clients)
                {
                    List<uint> disconnected = new List<uint>();
                    foreach (var kvp in _clients)
                    {
                        if ((DateTime.UtcNow - kvp.Value.LastKeepAlive).TotalSeconds > 30)
                        {
                            disconnected.Add(kvp.Key);
                        }
                    }
                    
                    foreach (uint clientId in disconnected)
                    {
                        DisconnectClient(clientId);
                    }
                }
                
                // Sleep for remaining tick time
                TimeSpan elapsed = DateTime.UtcNow - startTime;
                int sleepMs = (int)((tickInterval * 1000) - elapsed.TotalMilliseconds);
                if (sleepMs > 0)
                {
                    Thread.Sleep(sleepMs);
                }
            }
        }
        
        /// <summary>
        /// Sends a packet to a specific client.
        /// Thread-safe.
        /// </summary>
        public void SendToClient(uint clientId, NetworkPacket packet)
        {
            ClientConnection client;
            lock (_clients)
            {
                if (!_clients.TryGetValue(clientId, out client) || !client.IsConnected)
                {
                    return;
                }
            }
            
            try
            {
                byte[] data = PacketProcessor.Serialize(packet);
                if (data != null)
                {
                    client.Stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerHost] Send to client {clientId} failed: {ex.Message}");
                DisconnectClient(clientId);
            }
        }
        
        /// <summary>
        /// Broadcasts a packet to all connected clients.
        /// </summary>
        public void BroadcastToAll(NetworkPacket packet)
        {
            lock (_clients)
            {
                foreach (var client in _clients.Values)
                {
                    SendToClient(client.ClientId, packet);
                }
            }
        }
        
        /// <summary>
        /// Broadcasts to all clients except the specified one.
        /// </summary>
        public void BroadcastToAllExcept(uint excludeClientId, NetworkPacket packet)
        {
            lock (_clients)
            {
                foreach (var client in _clients.Values)
                {
                    if (client.ClientId != excludeClientId)
                    {
                        SendToClient(client.ClientId, packet);
                    }
                }
            }
        }
        
        /// <summary>
        /// Disconnects a client and cleans up resources.
        /// </summary>
        private void DisconnectClient(uint clientId)
        {
            ClientConnection client;
            lock (_clients)
            {
                if (!_clients.TryGetValue(clientId, out client))
                {
                    return;
                }
                _clients.Remove(clientId);
            }
            
            client.IsConnected = false;
            try
            {
                client.Stream?.Close();
                client.Socket?.Close();
            }
            catch { }
            
            EnqueueMainThread(() =>
            {
                OnClientDisconnected?.Invoke(clientId);
                Debug.Log($"[ServerHost] Client {clientId} disconnected");
            });
        }
        
        /// <summary>
        /// Enqueues an action to run on Unity's main thread.
        /// </summary>
        private void EnqueueMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }
        
        /// <summary>
        /// Processes main thread queue every frame.
        /// </summary>
        private void Update()
        {
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    Action action = _mainThreadQueue.Dequeue();
                    action?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// Cleanup on shutdown.
        /// </summary>
        private void OnDestroy()
        {
            StopServer();
        }
        
        /// <summary>
        /// Stops the server and disconnects all clients.
        /// </summary>
        public void StopServer()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            
            // Disconnect all clients
            lock (_clients)
            {
                foreach (var client in _clients.Values)
                {
                    DisconnectClient(client.ClientId);
                }
                _clients.Clear();
            }
            
            // Stop listener
            _listener?.Stop();
            
            Debug.Log("[ServerHost] Server stopped");
        }
        
        /// <summary>
        /// Returns the number of connected clients.
        /// </summary>
        public int GetClientCount()
        {
            lock (_clients)
            {
                return _clients.Count;
            }
        }
    }
}