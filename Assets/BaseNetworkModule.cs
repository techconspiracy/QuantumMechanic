// File: Assets/Scripts/RPG/Core/BaseNetworkModule.cs
using Unity.Netcode;
using UnityEngine;
using RPG.Contracts;
using System;

namespace RPG.Core
{
    /// <summary>
    /// Abstract foundation for all network-synchronized RPG modules.
    /// Enforces Unity 6 NGO best practices and provides lifecycle hooks.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public abstract partial class BaseNetworkModule : NetworkBehaviour, INetworkModule
    {
        [Header("Module Identity")]
        [SerializeField] private string _moduleId;
        
        private NetworkObject _cachedNetworkObject;
        private bool _isInitialized;

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
            Debug.Log($"[{ModuleId}] Module initialized on {(IsServer ? "Server" : "Client")}", this);
        }

        public virtual void OnModuleShutdown()
        {
            _isInitialized = false;
            Debug.Log($"[{ModuleId}] Module shutdown.", this);
        }

        public bool ValidateOwnership()
        {
            if (!IsOwner)
            {
                Debug.LogWarning($"[{ModuleId}] Ownership validation failed. Owner: {OwnerClientId}, Local: {NetworkManager.LocalClientId}", this);
                return false;
            }
            return true;
        }

        #endregion

        #region Unity 6 NGO Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            _cachedNetworkObject = GetComponent<NetworkObject>();
            
            if (_cachedNetworkObject == null)
            {
                Debug.LogError($"[{ModuleId}] Missing NetworkObject component! Add [RequireComponent] attribute.", this);
                return;
            }

            // Assign unique ID if empty
            if (string.IsNullOrEmpty(_moduleId))
            {
                _moduleId = $"{GetType().Name}_{NetworkObjectId}";
            }

            OnModuleInitialized();
        }

        public override void OnNetworkDespawn()
        {
            OnModuleShutdown();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Validates if this module can safely execute server-side logic.
        /// Call this before invoking [ServerRpc] methods.
        /// </summary>
        protected bool IsServerCallValid()
        {
            if (!IsSpawned)
            {
                Debug.LogWarning($"[{ModuleId}] Cannot execute server call - NetworkObject not spawned.", this);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates if this module can safely execute client broadcast logic.
        /// Call this before invoking [ClientRpc] methods.
        /// </summary>
        protected bool IsClientCallValid()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[{ModuleId}] Cannot execute client broadcast - not running as server.", this);
                return false;
            }
            return true;
        }

        #endregion

        #region Debug Helpers

        protected void LogServer(string message) => Debug.Log($"[SERVER][{ModuleId}] {message}", this);
        protected void LogClient(string message) => Debug.Log($"[CLIENT][{ModuleId}] {message}", this);
        protected void LogWarning(string message) => Debug.LogWarning($"[{ModuleId}] {message}", this);
        protected void LogError(string message) => Debug.LogError($"[{ModuleId}] {message}", this);

        #endregion
    }
}