// File: Assets/Scripts/RPG/Core/BaseNetworkModule_WebSocket.cs
using UnityEngine;
using RPG.Contracts;
using RPG.Networking;
using System;

namespace RPG.Core
{
    /// <summary>
    /// Refactored base module for WebSocket networking.
    /// Replaces Unity NGO NetworkBehaviour with custom sync.
    /// </summary>
    public abstract class BaseNetworkModule : MonoBehaviour, INetworkModule
    {
        [Header("Module Identity")]
        [SerializeField] private string _moduleId;

        protected string _ownerId; // WebSocket client ID
        protected bool _isInitialized;
        protected bool _isOwner;

        #region INetworkModule Implementation

        public string ModuleId
        {
            get => string.IsNullOrEmpty(_moduleId) ? GetType().Name : _moduleId;
            private set => _moduleId = value;
        }

        public virtual void OnModuleInitialized()
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"[{ModuleId}] Already initialized. Skipping.", this);
                return;
            }

            _isInitialized = true;
            
            // Subscribe to network events
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnMessageReceived += HandleNetworkMessage;
            }

            Debug.Log($"[{ModuleId}] Module initialized for owner: {_ownerId}", this);
        }

        public virtual void OnModuleShutdown()
        {
            _isInitialized = false;

            // Unsubscribe from network events
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnMessageReceived -= HandleNetworkMessage;
            }

            Debug.Log($"[{ModuleId}] Module shutdown.", this);
        }

        public bool ValidateOwnership()
        {
            if (!_isOwner)
            {
                Debug.LogWarning($"[{ModuleId}] Ownership validation failed.", this);
                return false;
            }
            return true;
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Start()
        {
            // Auto-generate ID if needed
            if (string.IsNullOrEmpty(_moduleId))
            {
                _moduleId = $"{GetType().Name}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            OnModuleInitialized();
        }

        protected virtual void OnDestroy()
        {
            OnModuleShutdown();
        }

        #endregion

        #region Ownership Management

        public void SetOwner(string ownerId, bool isLocal)
        {
            _ownerId = ownerId;
            _isOwner = isLocal;

            Debug.Log($"[{ModuleId}] Owner set to {ownerId} (IsLocal: {isLocal})", this);
        }

        public bool IsOwner => _isOwner;
        public string OwnerId => _ownerId;

        #endregion

        #region Network Communication

        protected void SendNetworkMessage(MessageType type, string payload)
        {
            if (WebSocketNetworkManager.Instance == null)
            {
                Debug.LogWarning($"[{ModuleId}] Cannot send message - WebSocketNetworkManager not found!");
                return;
            }

            var message = new NetworkMessage
            {
                messageType = type,
                payload = payload
            };

            WebSocketNetworkManager.Instance.SendMessage(message);
        }

        protected virtual void HandleNetworkMessage(NetworkMessage message)
        {
            // Override in derived classes to handle specific messages
        }

        #endregion

        #region Utility Methods

        protected void LogInfo(string message) => Debug.Log($"[{ModuleId}] {message}", this);
        protected void LogWarning(string message) => Debug.LogWarning($"[{ModuleId}] {message}", this);
        protected void LogError(string message) => Debug.LogError($"[{ModuleId}] {message}", this);

        #endregion
    }
}