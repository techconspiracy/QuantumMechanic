using UnityEngine;
using System;

namespace QuantumMechanic.Networking
{
    /// <summary>
    /// Lightweight network identity system for tracking objects across client-server architecture.
    /// Implements a deterministic ID generation strategy based on scene hierarchy and spawn order.
    /// </summary>
    public class NetworkIdentity : MonoBehaviour
    {
        [SerializeField] private uint _networkId;
        [SerializeField] private bool _isLocalPlayer;
        [SerializeField] private bool _hasAuthority;
        
        private static uint _nextNetworkId = 1000;
        
        public uint NetworkId => _networkId;
        public bool IsLocalPlayer => _isLocalPlayer;
        public bool HasAuthority => _hasAuthority;
        
        // Event fired when this object's authority changes
        public event Action<bool> OnAuthorityChanged;
        
        /// <summary>
        /// Initializes network identity on awake. Server-authoritative by default.
        /// </summary>
        private void Awake()
        {
            if (_networkId == 0)
            {
                AssignNetworkId();
            }
        }
        
        /// <summary>
        /// Assigns a unique network ID. Called by server during spawn.
        /// </summary>
        public void AssignNetworkId()
        {
            _networkId = _nextNetworkId++;
            Debug.Log($"[NetworkIdentity] Assigned ID {_networkId} to {gameObject.name}");
        }
        
        /// <summary>
        /// Sets the network ID from server spawn packet.
        /// </summary>
        public void SetNetworkId(uint id)
        {
            _networkId = id;
            if (id >= _nextNetworkId)
            {
                _nextNetworkId = id + 1;
            }
        }
        
        /// <summary>
        /// Marks this identity as the local player's controlled object.
        /// </summary>
        public void SetAsLocalPlayer()
        {
            _isLocalPlayer = true;
            _hasAuthority = true;
            OnAuthorityChanged?.Invoke(true);
            Debug.Log($"[NetworkIdentity] {gameObject.name} set as local player");
        }
        
        /// <summary>
        /// Transfers authority to/from this identity.
        /// </summary>
        public void SetAuthority(bool authority)
        {
            if (_hasAuthority != authority)
            {
                _hasAuthority = authority;
                OnAuthorityChanged?.Invoke(authority);
            }
        }
        
        /// <summary>
        /// Validates that this identity has authority to perform actions.
        /// </summary>
        public bool ValidateAuthority()
        {
            return _hasAuthority || _isLocalPlayer;
        }
        
        /// <summary>
        /// Retrieves the network transform component for position synchronization.
        /// </summary>
        public Vector3 GetNetworkPosition()
        {
            return transform.position;
        }
        
        /// <summary>
        /// Retrieves the network rotation for synchronization.
        /// </summary>
        public Quaternion GetNetworkRotation()
        {
            return transform.rotation;
        }
        
        /// <summary>
        /// Server-side method to apply position from client input.
        /// </summary>
        public void ApplyNetworkTransform(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
        }
        
        /// <summary>
        /// Cleanup when identity is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            OnAuthorityChanged = null;
        }
    }
}