// File: Assets/Scripts/RPG/Networking/HybridNetworkManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;

namespace RPG.Networking
{
    /// <summary>
    /// Unified network manager that handles both host and client modes.
    /// First player starts as host (server + client), others join as clients.
    /// Provides seamless switching between modes with client prediction and server reconciliation.
    /// </summary>
    public class HybridNetworkManager : MonoBehaviour
    {
        public static HybridNetworkManager Instance { get; private set; }

        [Header("Network Mode")]
        [SerializeField] private NetworkMode _networkMode = NetworkMode.Offline;

        [Header("Host/Server Settings")]
        [SerializeField] private int _hostPort = 8080;
        [SerializeField] private int _maxPlayers = 16;

        [Header("Client Settings")]
        [SerializeField] private string _serverAddress = "ws://localhost";

        [Header("Prediction Settings")]
        [SerializeField] private bool _enableClientPrediction = true;
        [SerializeField] private float _reconciliationThreshold = 0.5f;
        [SerializeField] private int _inputBufferSize = 32;

        public event Action OnNetworkReady;
        public event Action<string> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action<NetworkMessage> OnMessageReceived;

        private NativeServerManager _serverManager;
        private WebSocketNetworkManager _clientManager;
        private Queue<PredictedInput> _inputBuffer = new Queue<PredictedInput>();
        private uint _currentInputSequence = 0;

        public NetworkMode CurrentMode => _networkMode;
        public bool IsHost => _networkMode == NetworkMode.Host;
        public bool IsClient => _networkMode == NetworkMode.Client;
        public bool IsOffline => _networkMode == NetworkMode.Offline;
        public string LocalPlayerId { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Setup managers
            _serverManager = gameObject.AddComponent<NativeServerManager>();
            _clientManager = gameObject.AddComponent<WebSocketNetworkManager>();

            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #region Mode Management

        /// <summary>
        /// Start as host (first player: server + client)
        /// </summary>
        public void StartHost()
        {
            if (_networkMode != NetworkMode.Offline)
            {
                Debug.LogWarning("[Network] Already connected!");
                return;
            }

            Debug.Log("[Network] Starting as HOST...");
            _networkMode = NetworkMode.Host;

            // Start local server
            _serverManager.StartServer();

            // Connect to own server as client
            _clientManager.ConnectAsync();
        }

        /// <summary>
        /// Join as client (subsequent players)
        /// </summary>
        public void JoinAsClient(string serverAddress = null)
        {
            if (_networkMode != NetworkMode.Offline)
            {
                Debug.LogWarning("[Network] Already connected!");
                return;
            }

            Debug.Log("[Network] Joining as CLIENT...");
            _networkMode = NetworkMode.Client;

            // Override server address if provided
            if (!string.IsNullOrEmpty(serverAddress))
            {
                _serverAddress = serverAddress;
            }

            // Connect to remote server
            _clientManager.ConnectAsync();
        }

        /// <summary>
        /// Disconnect and return to offline mode
        /// </summary>
        public void Disconnect()
        {
            Debug.Log($"[Network] Disconnecting from {_networkMode} mode...");

            if (IsHost)
            {
                _serverManager.StopServer();
            }

            _clientManager.DisconnectAsync();

            _networkMode = NetworkMode.Offline;
            _inputBuffer.Clear();
            _currentInputSequence = 0;
        }

        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            // Server events
            _serverManager.OnServerStarted += HandleServerStarted;
            _serverManager.OnClientConnected += HandleClientConnected;
            _serverManager.OnClientDisconnected += HandleClientDisconnected;

            // Client events
            _clientManager.OnConnected += HandleClientConnected;
            _clientManager.OnDisconnected += HandleClientDisconnected;
            _clientManager.OnMessageReceived += HandleMessageReceived;
        }

        private void UnsubscribeFromEvents()
        {
            // Server events
            if (_serverManager != null)
            {
                _serverManager.OnServerStarted -= HandleServerStarted;
                _serverManager.OnClientConnected -= HandleClientConnected;
                _serverManager.OnClientDisconnected -= HandleClientDisconnected;
            }

            // Client events
            if (_clientManager != null)
            {
                _clientManager.OnConnected -= HandleClientConnected;
                _clientManager.OnDisconnected -= HandleClientDisconnected;
                _clientManager.OnMessageReceived -= HandleMessageReceived;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleServerStarted()
        {
            Debug.Log("[Network] Server started successfully!");
            LocalPlayerId = _serverManager.HostPlayerId;
        }

        private void HandleClientConnected(string playerId = null)
        {
            Debug.Log($"[Network] Client connected: {playerId ?? "self"}");

            if (IsClient && string.IsNullOrEmpty(LocalPlayerId))
            {
                LocalPlayerId = _clientManager.ClientId;
                OnNetworkReady?.Invoke();
            }
            else if (IsHost && !string.IsNullOrEmpty(playerId))
            {
                OnPlayerJoined?.Invoke(playerId);
            }
        }

        private void HandleClientDisconnected(string reason)
        {
            Debug.Log($"[Network] Disconnected: {reason}");
            
            if (!string.IsNullOrEmpty(reason))
            {
                OnPlayerLeft?.Invoke(reason);
            }
        }

        private void HandleMessageReceived(NetworkMessage message)
        {
            // Handle special message types
            switch (message.messageType)
            {
                case MessageType.Connect:
                    HandleWelcomeMessage(message);
                    break;

                case MessageType.PlayerMovement:
                    HandleMovementMessage(message);
                    break;

                default:
                    // Forward to subscribers
                    OnMessageReceived?.Invoke(message);
                    break;
            }
        }

        private void HandleWelcomeMessage(NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.payload)) return;

            WelcomeData welcomeData = JsonUtility.FromJson<WelcomeData>(message.payload);
            LocalPlayerId = welcomeData.yourClientId;

            Debug.Log($"[Network] Received welcome! Your ID: {LocalPlayerId}");
            OnNetworkReady?.Invoke();
        }

        private void HandleMovementMessage(NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.payload)) return;

            MovementData movementData = JsonUtility.FromJson<MovementData>(message.payload);

            // Server reconciliation for client prediction
            if (_enableClientPrediction && message.senderId == LocalPlayerId && movementData.isCorrection)
            {
                ReconcilePrediction(movementData);
            }

            // Forward to subscribers
            OnMessageReceived?.Invoke(message);
        }

        #endregion

        #region Client Prediction & Server Reconciliation

        /// <summary>
        /// Send predicted input to server and store for reconciliation
        /// </summary>
        public void SendPredictedInput(Vector3 movement, Quaternion rotation, bool jump)
        {
            if (!_enableClientPrediction)
            {
                SendMovement(movement, rotation);
                return;
            }

            // Create predicted input
            PredictedInput input = new PredictedInput
            {
                sequence = _currentInputSequence++,
                movement = movement,
                rotation = rotation,
                jump = jump,
                timestamp = Time.time
            };

            // Store in buffer
            _inputBuffer.Enqueue(input);
            if (_inputBuffer.Count > _inputBufferSize)
            {
                _inputBuffer.Dequeue();
            }

            // Send to server
            SendMovement(movement, rotation);
        }

        /// <summary>
        /// Reconcile client prediction with server authority
        /// </summary>
        private void ReconcilePrediction(MovementData serverState)
        {
            // TODO: Implement full reconciliation
            // 1. Find input with matching sequence number
            // 2. Compare predicted position with server position
            // 3. If difference > threshold, snap to server position
            // 4. Replay inputs after correction

            Debug.Log("[Network] Server correction received - reconciling prediction...");
        }

        #endregion

        #region Message Sending

        public void SendMovement(Vector3 position, Quaternion rotation, Vector3 velocity = default)
        {
            MovementData data = new MovementData
            {
                position = position,
                rotation = rotation,
                velocity = velocity,
                isGrounded = true // TODO: Pass actual grounded state
            };

            NetworkMessage message = new NetworkMessage
            {
                messageType = MessageType.PlayerMovement,
                payload = JsonUtility.ToJson(data)
            };

            SendMessage(message);
        }

        public void SendDamage(string targetId, float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            DamageData data = new DamageData
            {
                attackerId = LocalPlayerId,
                targetId = targetId,
                amount = amount,
                hitPoint = hitPoint,
                hitNormal = hitNormal
            };

            NetworkMessage message = new NetworkMessage
            {
                messageType = MessageType.Damage,
                payload = JsonUtility.ToJson(data)
            };

            SendMessage(message);
        }

        public void SendResourceUpdate(string resourceType, float value)
        {
            NetworkMessage message = new NetworkMessage
            {
                messageType = MessageType.ResourceUpdate,
                payload = JsonUtility.ToJson(new { resourceType, value })
            };

            SendMessage(message);
        }

        public void SendChatMessage(string text)
        {
            NetworkMessage message = new NetworkMessage
            {
                messageType = MessageType.ChatMessage,
                payload = text
            };

            SendMessage(message);
        }

        public void SendMessage(NetworkMessage message)
        {
            if (_networkMode == NetworkMode.Offline)
            {
                Debug.LogWarning("[Network] Cannot send message - offline!");
                return;
            }

            _clientManager?.SendMessage(message);
        }

        #endregion

        #region Public API

        public List<PlayerState> GetAllPlayers()
        {
            if (IsHost)
            {
                return _serverManager.GetAllPlayerStates();
            }
            return new List<PlayerState>();
        }

        public PlayerState GetPlayer(string playerId)
        {
            if (IsHost)
            {
                return _serverManager.GetPlayerState(playerId);
            }
            return null;
        }

        public bool IsPlayerConnected(string playerId)
        {
            if (IsHost)
            {
                return _serverManager.IsClientConnected(playerId);
            }
            return false;
        }

        #endregion
    }

    #region Enums

    public enum NetworkMode
    {
        Offline,
        Host,      // Server + Client (first player)
        Client     // Client only (additional players)
    }

    #endregion

    #region Data Structures

    [Serializable]
    public class PredictedInput
    {
        public uint sequence;
        public Vector3 movement;
        public Quaternion rotation;
        public bool jump;
        public float timestamp;
    }

    [Serializable]
    public class MovementData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public bool isGrounded;
        public bool isCorrection; // Server correction flag
        public uint sequence;     // For reconciliation
    }

    #endregion
}