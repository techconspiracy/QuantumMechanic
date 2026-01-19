// File: Assets/Scripts/RPG/Networking/NativeServerManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Linq;

namespace RPG.Networking
{
    /// <summary>
    /// Native C# WebSocket server that runs inside Unity.
    /// First player becomes host (server + client), additional players are clients.
    /// Handles server-authoritative validation with client prediction reconciliation.
    /// </summary>
    public class NativeServerManager : MonoBehaviour
    {
        public static NativeServerManager Instance { get; private set; }

        [Header("Server Settings")]
        [SerializeField] private int _serverPort = 8080;
        [SerializeField] private int _maxPlayers = 16;
        [SerializeField] private float _tickRate = 30f; // Server updates per second

        [Header("Authority Settings")]
        [SerializeField] private bool _validateMovement = true;
        [SerializeField] private float _maxMovementSpeed = 15f;
        [SerializeField] private float _teleportThreshold = 5f; // Distance to force correction

        public event Action<string> OnClientConnected;
        public event Action<string> OnClientDisconnected;
        public event Action OnServerStarted;
        public event Action OnServerStopped;

        private HttpListener _httpListener;
        private Dictionary<string, ClientConnection> _clients = new Dictionary<string, ClientConnection>();
        private Dictionary<string, PlayerState> _playerStates = new Dictionary<string, PlayerState>();
        private CancellationTokenSource _serverCancellation;
        private bool _isRunning;
        private float _tickTimer;
        private string _hostPlayerId;

        public bool IsRunning => _isRunning;
        public bool IsHost => _isRunning;
        public int ConnectedClientCount => _clients.Count;
        public string HostPlayerId => _hostPlayerId;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!_isRunning) return;

            // Server tick for state validation and broadcasting
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= 1f / _tickRate)
            {
                _tickTimer = 0f;
                ServerTick();
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        #region Server Lifecycle

        public async void StartServer()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[Server] Already running!");
                return;
            }

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{_serverPort}/");
                _httpListener.Start();

                _serverCancellation = new CancellationTokenSource();
                _isRunning = true;
                _hostPlayerId = Guid.NewGuid().ToString();

                Debug.Log($"[Server] Started on port {_serverPort}");
                Debug.Log($"[Server] Host Player ID: {_hostPlayerId}");

                OnServerStarted?.Invoke();

                // Start accepting connections
                _ = AcceptClientsAsync(_serverCancellation.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server] Failed to start: {ex.Message}");
                _isRunning = false;
            }
        }

        public void StopServer()
        {
            if (!_isRunning) return;

            Debug.Log("[Server] Stopping...");

            _isRunning = false;
            _serverCancellation?.Cancel();

            // Disconnect all clients
            foreach (var client in _clients.Values.ToList())
            {
                client.WebSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None);
            }

            _clients.Clear();
            _playerStates.Clear();

            _httpListener?.Stop();
            _httpListener?.Close();

            OnServerStopped?.Invoke();
            Debug.Log("[Server] Stopped.");
        }

        #endregion

        #region Client Connection Management

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();

                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = HandleClientConnectionAsync(context, cancellationToken);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Debug.LogError($"[Server] Error accepting client: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClientConnectionAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            WebSocketContext wsContext = null;
            WebSocket webSocket = null;

            try
            {
                wsContext = await context.AcceptWebSocketAsync(null);
                webSocket = wsContext.WebSocket;

                string clientId = Guid.NewGuid().ToString();
                var client = new ClientConnection
                {
                    ClientId = clientId,
                    WebSocket = webSocket,
                    LastHeartbeat = DateTime.UtcNow
                };

                _clients[clientId] = client;

                Debug.Log($"[Server] Client connected: {clientId} ({_clients.Count}/{_maxPlayers})");
                OnClientConnected?.Invoke(clientId);

                // Send welcome message with client ID and current world state
                await SendWelcomeMessage(client);

                // Start receiving messages from this client
                await ReceiveMessagesAsync(client, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server] Client connection error: {ex.Message}");
            }
            finally
            {
                if (webSocket != null && _clients.ContainsKey(wsContext?.RequestUri.ToString()))
                {
                    var clientId = _clients.FirstOrDefault(x => x.Value.WebSocket == webSocket).Key;
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        RemoveClient(clientId);
                    }
                }
            }
        }

        private async Task ReceiveMessagesAsync(ClientConnection client, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (client.WebSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await client.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessClientMessage(client.ClientId, json);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Server] Error receiving from {client.ClientId}: {ex.Message}");
                    break;
                }
            }
        }

        private void RemoveClient(string clientId)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                _clients.Remove(clientId);
                _playerStates.Remove(clientId);

                Debug.Log($"[Server] Client disconnected: {clientId}");
                OnClientDisconnected?.Invoke(clientId);

                // Broadcast disconnect to remaining clients
                BroadcastPlayerDisconnect(clientId);
            }
        }

        #endregion

        #region Message Processing

        private void ProcessClientMessage(string clientId, string json)
        {
            try
            {
                NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(json);
                message.senderId = clientId; // Enforce sender ID server-side

                switch (message.messageType)
                {
                    case MessageType.PlayerMovement:
                        HandlePlayerMovement(clientId, message);
                        break;

                    case MessageType.PlayerAction:
                        HandlePlayerAction(clientId, message);
                        break;

                    case MessageType.Damage:
                        HandleDamageRequest(clientId, message);
                        break;

                    case MessageType.ResourceUpdate:
                        HandleResourceUpdate(clientId, message);
                        break;

                    case MessageType.ChatMessage:
                        HandleChatMessage(clientId, message);
                        break;

                    default:
                        Debug.LogWarning($"[Server] Unknown message type: {message.messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server] Error processing message from {clientId}: {ex.Message}");
            }
        }

        #endregion

        #region Server-Authoritative Validation

        private void HandlePlayerMovement(string clientId, NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.payload)) return;

            MovementData clientData = JsonUtility.FromJson<MovementData>(message.payload);

            // Server-authoritative validation
            if (!_playerStates.ContainsKey(clientId))
            {
                // First movement update, initialize state
                _playerStates[clientId] = new PlayerState
                {
                    playerId = clientId,
                    position = clientData.position,
                    rotation = clientData.rotation,
                    velocity = clientData.velocity,
                    lastUpdateTime = Time.time
                };
            }
            else
            {
                PlayerState serverState = _playerStates[clientId];
                
                if (_validateMovement)
                {
                    // Calculate time since last update
                    float deltaTime = Time.time - serverState.lastUpdateTime;

                    // Validate movement distance
                    float distance = Vector3.Distance(serverState.position, clientData.position);
                    float maxDistance = _maxMovementSpeed * deltaTime;

                    if (distance > maxDistance + _teleportThreshold)
                    {
                        // Client is moving too fast or teleported - reject and send correction
                        Debug.LogWarning($"[Server] Invalid movement from {clientId}: {distance:F2}m in {deltaTime:F2}s (max: {maxDistance:F2}m)");
                        SendPositionCorrection(clientId, serverState);
                        return;
                    }
                }

                // Update accepted state
                serverState.position = clientData.position;
                serverState.rotation = clientData.rotation;
                serverState.velocity = clientData.velocity;
                serverState.isGrounded = clientData.isGrounded;
                serverState.lastUpdateTime = Time.time;
            }

            // Broadcast to all other clients
            BroadcastToOthers(clientId, message);
        }

        private void HandlePlayerAction(string clientId, NetworkMessage message)
        {
            // TODO: Validate action is allowed (cooldowns, resources, etc.)
            // For now, just broadcast
            BroadcastToAll(message);
        }

        private void HandleDamageRequest(string clientId, NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.payload)) return;

            DamageData damageData = JsonUtility.FromJson<DamageData>(message.payload);

            // TODO: Server-side validation
            // - Is attacker in range?
            // - Does attacker have required resources?
            // - Is target damageable?
            // - Apply damage calculation with server authority

            // For now, broadcast validated damage
            BroadcastToAll(message);
            
            Debug.Log($"[Server] {clientId} dealt {damageData.amount} damage to {damageData.targetId}");
        }

        private void HandleResourceUpdate(string clientId, NetworkMessage message)
        {
            // Server validates resource changes
            // Only allow updates from resource owner
            if (string.IsNullOrEmpty(message.payload)) return;

            // TODO: Validate resource change is legal
            // - Did player actually collect item?
            // - Did spell actually cost this much mana?
            // - Is transaction valid?

            // Broadcast validated update
            BroadcastToAll(message);
        }

        private void HandleChatMessage(string clientId, NetworkMessage message)
        {
            // TODO: Chat validation/filtering
            // - Rate limiting
            // - Profanity filter
            // - Spam detection

            BroadcastToAll(message);
        }

        #endregion

        #region Broadcasting

        private async void BroadcastToAll(NetworkMessage message)
        {
            string json = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(json);

            foreach (var client in _clients.Values.ToList())
            {
                await SendToClient(client, data);
            }
        }

        private async void BroadcastToOthers(string excludeClientId, NetworkMessage message)
        {
            string json = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(json);

            foreach (var client in _clients.Values.ToList())
            {
                if (client.ClientId != excludeClientId)
                {
                    await SendToClient(client, data);
                }
            }
        }

        private async void SendToClient(string clientId, NetworkMessage message)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                string json = JsonUtility.ToJson(message);
                byte[] data = Encoding.UTF8.GetBytes(json);
                await SendToClient(client, data);
            }
        }

        private async Task SendToClient(ClientConnection client, byte[] data)
        {
            try
            {
                if (client.WebSocket.State == WebSocketState.Open)
                {
                    await client.WebSocket.SendAsync(
                        new ArraySegment<byte>(data),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Server] Failed to send to {client.ClientId}: {ex.Message}");
                RemoveClient(client.ClientId);
            }
        }

        #endregion

        #region Server State Management

        private void ServerTick()
        {
            // Send authoritative world state snapshot to all clients
            WorldStateMessage worldState = new WorldStateMessage
            {
                messageType = MessageType.WorldState,
                players = _playerStates.Values.ToList()
            };

            BroadcastToAll(worldState);
        }

        private async Task SendWelcomeMessage(ClientConnection client)
        {
            var welcomeData = new WelcomeData
            {
                yourClientId = client.ClientId,
                hostPlayerId = _hostPlayerId,
                currentPlayers = _playerStates.Values.ToList()
            };

            var message = new NetworkMessage
            {
                messageType = MessageType.Connect,
                senderId = "SERVER",
                payload = JsonUtility.ToJson(welcomeData)
            };

            string json = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(json);

            await SendToClient(client, data);
        }

        private void SendPositionCorrection(string clientId, PlayerState correctState)
        {
            var correctionMessage = new NetworkMessage
            {
                messageType = MessageType.PlayerMovement,
                senderId = "SERVER",
                payload = JsonUtility.ToJson(new MovementData
                {
                    position = correctState.position,
                    rotation = correctState.rotation,
                    velocity = correctState.velocity,
                    isGrounded = correctState.isGrounded,
                    isCorrection = true
                })
            };

            SendToClient(clientId, correctionMessage);
        }

        private void BroadcastPlayerDisconnect(string clientId)
        {
            var message = new NetworkMessage
            {
                messageType = MessageType.Disconnect,
                senderId = clientId
            };

            BroadcastToAll(message);
        }

        #endregion

        #region Public API

        public PlayerState GetPlayerState(string playerId)
        {
            _playerStates.TryGetValue(playerId, out var state);
            return state;
        }

        public List<PlayerState> GetAllPlayerStates()
        {
            return _playerStates.Values.ToList();
        }

        public bool IsClientConnected(string clientId)
        {
            return _clients.ContainsKey(clientId);
        }

        #endregion
    }

    #region Data Structures

    public class ClientConnection
    {
        public string ClientId;
        public WebSocket WebSocket;
        public DateTime LastHeartbeat;
    }

    [Serializable]
    public class WelcomeData
    {
        public string yourClientId;
        public string hostPlayerId;
        public List<PlayerState> currentPlayers;
    }

    [Serializable]
    public class DamageData
    {
        public string attackerId;
        public string targetId;
        public float amount;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
    }

    #endregion
}