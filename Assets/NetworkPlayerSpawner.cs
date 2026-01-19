using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace RPG.Core
{
    /// <summary>
    /// Server-authoritative spawner that handles player instantiation.
    /// </summary>
    public class NetworkPlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private List<Transform> _spawnPoints;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // Listen for new clients connecting to spawn their player object
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
            
            // Spawn the host (local player) if they are already connected
            if (IsServer && NetworkManager.Singleton.IsHost)
            {
                SpawnPlayer(NetworkManager.Singleton.LocalClientId);
            }
        }

        private void SpawnPlayer(ulong clientId)
        {
            if (!IsServer) return;

            Transform spawnPoint = _spawnPoints[Mathf.Clamp((int)clientId, 0, _spawnPoints.Count - 1)];
            
            GameObject playerInstance = Instantiate(_playerPrefab, spawnPoint.position, spawnPoint.rotation);
            
            // Critical: Pass ownership to the specific client
            var networkObject = playerInstance.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(clientId);
            
            Debug.Log($"[Spawner] Player spawned for ClientID: {clientId}");
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
        }
    }
}