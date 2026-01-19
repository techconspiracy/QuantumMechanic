// File: Assets/Scripts/RPG/Networking/WebSocketNetworkManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using NativeWebSocket; // Install via Package Manager: https://github.com/endel/NativeWebSocket

namespace RPG.Networking
{
    /// <summary>
    /// WebSocket-based network manager with AES encryption.
    /// Replaces Unity NGO with custom authoritative server architecture.
    /// </summary>
    public class WebSocketNetworkManager : MonoBehaviour
    {
        public static WebSocketNetworkManager Instance { get; private set; }

        [Header("Connection Settings")]
        [SerializeField] private string _serverAddress = "wss://your-server.com";
        [SerializeField] private int _serverPort = 8080;
        [SerializeField] private bool _useEncryption = true;
        
        [Header("Reconnection")]
        [SerializeField] private bool _autoReconnect = true;
        [SerializeField] private float _reconnectDelay = 3f;
        [SerializeField] private int _maxReconnectAttempts = 5;

        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<NetworkMessage> OnMessageReceived;

        private WebSocket _webSocket;
        private string _clientId;
        private bool _isConnected;
        private int _reconnectAttempts;
        private float _reconnectTimer;
        private Queue<NetworkMessage> _outgoingMessages = new Queue<NetworkMessage>();
        
        // AES Encryption
        private Aes _aesProvider;
        private byte[] _encryptionKey;
        private byte[] _encryptionIV;

        public string ClientId => _clientId;
        public bool IsConnected => _isConnected;
        public bool IsServer => false; // Client-only for now

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeEncryption();
        }

        private void Update()
        {
            // Process WebSocket messages
            #if !UNITY_WEBGL || UNITY_EDITOR
            _webSocket?.DispatchMessageQueue();
            #endif

            // Handle reconnection
            if (!_isConnected && _autoReconnect && _reconnectAttempts < _maxReconnectAttempts)
            {
                _reconnectTimer += Time.deltaTime;
                if (_reconnectTimer >= _reconnectDelay)
                {
                    _reconnectTimer = 0f;
                    _reconnectAttempts++;
                    Debug.Log($"[WebSocket] Reconnect attempt {_reconnectAttempts}/{_maxReconnectAttempts}");
                    ConnectAsync();
                }
            }

            // Process outgoing message queue
            ProcessOutgoingMessages();
        }

        private void OnDestroy()
        {
            DisconnectAsync();
            _aesProvider?.Dispose();
        }

        private void OnApplicationQuit()
        {
            DisconnectAsync();
        }

        #region Connection Management

        public async void ConnectAsync()
        {
            if (_isConnected)
            {
                Debug.LogWarning("[WebSocket] Already connected!");
                return;
            }

            string url = $"{_serverAddress}:{_serverPort}";
            _webSocket = new WebSocket(url);

            _webSocket.OnOpen += HandleConnectionOpened;
            _webSocket.OnMessage += HandleMessageReceived;
            _webSocket.OnError += HandleError;
            _webSocket.OnClose += HandleConnectionClosed;

            Debug.Log($"[WebSocket] Connecting to {url}...");
            await _webSocket.Connect();
        }

        public async void DisconnectAsync()
        {
            if (_webSocket == null) return;

            _isConnected = false;
            await _webSocket.Close();
            _webSocket = null;
            Debug.Log("[WebSocket] Disconnected.");
        }

        #endregion

        #region Event Handlers

        private void HandleConnectionOpened()
        {
            _isConnected = true;
            _reconnectAttempts = 0;
            _clientId = Guid.NewGuid().ToString();

            Debug.Log($"[WebSocket] Connected! ClientID: {_clientId}");

            // Send initial handshake
            SendMessage(new NetworkMessage
            {
                messageType = MessageType.Connect,
                senderId = _clientId,
                timestamp = DateTime.UtcNow.Ticks
            });

            OnConnected?.Invoke();
        }

        private void HandleMessageReceived(byte[] data)
        {
            try
            {
                byte[] decryptedData = _useEncryption ? Decrypt(data) : data;
                string json = Encoding.UTF8.GetString(decryptedData);
                NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(json);

                OnMessageReceived?.Invoke(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocket] Failed to parse message: {ex.Message}");
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[WebSocket] Error: {error}");
        }

        private void HandleConnectionClosed(WebSocketCloseCode code)
        {
            _isConnected = false;
            string reason = code.ToString();
            Debug.Log($"[WebSocket] Connection closed: {reason}");
            OnDisconnected?.Invoke(reason);
        }

        #endregion

        #region Message Sending

        public void SendMessage(NetworkMessage message)
        {
            if (!_isConnected)
            {
                Debug.LogWarning("[WebSocket] Cannot send message - not connected!");
                return;
            }

            message.senderId = _clientId;
            message.timestamp = DateTime.UtcNow.Ticks;
            _outgoingMessages.Enqueue(message);
        }

        private async void ProcessOutgoingMessages()
        {
            if (_outgoingMessages.Count == 0 || _webSocket == null) return;

            while (_outgoingMessages.Count > 0)
            {
                NetworkMessage message = _outgoingMessages.Dequeue();
                
                try
                {
                    string json = JsonUtility.ToJson(message);
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    byte[] encryptedData = _useEncryption ? Encrypt(data) : data;

                    await _webSocket.Send(encryptedData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WebSocket] Failed to send message: {ex.Message}");
                }
            }
        }

        #endregion

        #region Encryption (AES-256)

        private void InitializeEncryption()
        {
            if (!_useEncryption) return;

            _aesProvider = Aes.Create();
            _aesProvider.KeySize = 256;
            _aesProvider.Mode = CipherMode.CBC;
            _aesProvider.Padding = PaddingMode.PKCS7;

            // In production, exchange keys securely with server during handshake
            // For now, using hardcoded keys (REPLACE IN PRODUCTION)
            _encryptionKey = Encoding.UTF8.GetBytes("YOUR_32_BYTE_SECRET_KEY_HERE!");
            _encryptionIV = Encoding.UTF8.GetBytes("YOUR_16_BYTE_IV!!");
        }

        private byte[] Encrypt(byte[] data)
        {
            using (var encryptor = _aesProvider.CreateEncryptor(_encryptionKey, _encryptionIV))
            using (var ms = new System.IO.MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                }
                return ms.ToArray();
            }
        }

        private byte[] Decrypt(byte[] data)
        {
            using (var decryptor = _aesProvider.CreateDecryptor(_encryptionKey, _encryptionIV))
            using (var ms = new System.IO.MemoryStream(data))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var result = new System.IO.MemoryStream())
            {
                cs.CopyTo(result);
                return result.ToArray();
            }
        }

        #endregion

        #region Utility

        public void RegisterMessageHandler<T>(MessageType type, Action<T> handler) where T : NetworkMessage
        {
            OnMessageReceived += (msg) =>
            {
                if (msg.messageType == type && msg is T typedMsg)
                {
                    handler(typedMsg);
                }
            };
        }

        #endregion
    }

    #region Message Definitions

    public enum MessageType
    {
        Connect,
        Disconnect,
        PlayerSpawn,
        PlayerMovement,
        PlayerAction,
        WorldState,
        Damage,
        ResourceUpdate,
        ChatMessage
    }

    [Serializable]
    public class NetworkMessage
    {
        public MessageType messageType;
        public string senderId;
        public long timestamp;
        public string payload; // JSON serialized data
    }

    [Serializable]
    public class PlayerMovementMessage : NetworkMessage
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public bool isGrounded;
    }

    [Serializable]
    public class WorldStateMessage : NetworkMessage
    {
        public List<PlayerState> players;
    }

    [Serializable]
    public class PlayerState
    {
        public string playerId;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public float mana;
    }

    #endregion
}