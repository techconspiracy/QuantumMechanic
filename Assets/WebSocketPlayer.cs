// File: Assets/Scripts/RPG/Networking/WebSocketPlayerSpawner.cs
using UnityEngine;
using System.Collections.Generic;
using RPG.Player;

namespace RPG.Networking
{
    /// <summary>
    /// Manages player spawning and tracking for WebSocket multiplayer.
    /// Spawns local player on connect, remote players on world state updates.
    /// </summary>
    public class WebSocketPlayerSpawner : MonoBehaviour
    {
        [Header("Player Prefab")]
        [SerializeField] private GameObject _playerPrefab;

        [Header("Spawn Points")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
        [SerializeField] private bool _randomizeSpawnPoint = true;

        private Dictionary<string, GameObject> _activePlayers = new Dictionary<string, GameObject>();
        private GameObject _localPlayer;

        private void Start()
        {
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnConnected += HandleConnected;
                WebSocketNetworkManager.Instance.OnMessageReceived += HandleMessage;
                WebSocketNetworkManager.Instance.OnDisconnected += HandleDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (WebSocketNetworkManager.Instance != null)
            {
                WebSocketNetworkManager.Instance.OnConnected -= HandleConnected;
                WebSocketNetworkManager.Instance.OnMessageReceived -= HandleMessage;
                WebSocketNetworkManager.Instance.OnDisconnected -= HandleDisconnected;
            }
        }

        private void HandleConnected()
        {
            // Spawn local player
            SpawnLocalPlayer(WebSocketNetworkManager.Instance.ClientId);

            // Request current world state from server
            RequestWorldState();
        }

        private void HandleDisconnected(string reason)
        {
            // Clean up all players
            foreach (var player in _activePlayers.Values)
            {
                if (player != null)
                {
                    Destroy(player);
                }
            }
            _activePlayers.Clear();
            _localPlayer = null;
        }

        private void HandleMessage(NetworkMessage message)
        {
            switch (message.messageType)
            {
                case MessageType.PlayerSpawn:
                    HandlePlayerSpawn(message);
                    break;

                case MessageType.WorldState:
                    HandleWorldState(message);
                    break;

                case MessageType.Disconnect:
                    HandlePlayerDisconnect(message);
                    break;
            }
        }

        #region Player Spawning

        private void SpawnLocalPlayer(string playerId)
        {
            if (_localPlayer != null)
            {
                Debug.LogWarning("[PlayerSpawner] Local player already spawned!");
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition();
            Quaternion spawnRotation = Quaternion.identity;

            _localPlayer = Instantiate(_playerPrefab, spawnPosition, spawnRotation);
            _localPlayer.name = $"Player_Local_{playerId}";

            // Initialize as local player
            var controller = _localPlayer.GetComponent<NetworkPlayerController>();
            if (controller != null)
            {
                controller.Initialize(playerId, true);
            }

            // Initialize modules
            var modules = _localPlayer.GetComponents<BaseNetworkModule>();
            foreach (var module in modules)
            {
                module.SetOwner(playerId, true);
            }

            _activePlayers[playerId] = _localPlayer;

            Debug.Log($"[PlayerSpawner] Local player spawned: {playerId}");

            // Notify server
            SendPlayerSpawnMessage(playerId, spawnPosition, spawnRotation);
        }

        private void SpawnRemotePlayer(PlayerState state)
        {
            if (_activePlayers.ContainsKey(state.playerId))
            {
                // Player already exists, just update state
                UpdateRemotePlayer(state);
                return;
            }

            GameObject remotePlayer = Instantiate(
                _playerPrefab,
                state.position,
                state.rotation
            );
            remotePlayer.name = $"Player_Remote_{state.playerId}";

            // Initialize as remote player
            var controller = remotePlayer.GetComponent<NetworkPlayerController>();
            if (controller != null)
            {
                controller.Initialize(state.playerId, false);
            }

            // Initialize modules
            var modules = remotePlayer.GetComponents<BaseNetworkModule>();
            foreach (var module in modules)
            {
                module.SetOwner(state.playerId, false);
            }

            _activePlayers[state.playerId] = remotePlayer;

            Debug.Log($"[PlayerSpawner] Remote player spawned: {state.playerId}");
        }

        private void UpdateRemotePlayer(PlayerState state)
        {
            if (!_activePlayers.TryGetValue(state.playerId, out GameObject player))
            {
                return;
            }

            // Position and rotation will be interpolated by NetworkPlayerController
            // Just update module states here if needed
            var healthModule = player.GetComponent<HealthModule>();
            if (healthModule != null)
            {
                // Update health (this would need to be refactored to use synced variables)
                // For now, we'll handle this in the module itself
            }
        }

        #endregion

        #region Network Messages

        private void SendPlayerSpawnMessage(string playerId, Vector3 position, Quaternion rotation)
        {
            var spawnData = new PlayerSpawnData
            {
                playerId = playerId,
                position = position,
                rotation = rotation
            };

            var message = new NetworkMessage
            {
                messageType = MessageType.PlayerSpawn,
                payload = JsonUtility.ToJson(spawnData)
            };

            WebSocketNetworkManager.Instance?.SendMessage(message);
        }

        private void RequestWorldState()
        {
            var message = new NetworkMessage
            {
                messageType = MessageType.WorldState,
                payload = "{\"request\":true}"
            };

            WebSocketNetworkManager.Instance?.SendMessage(message);
        }

        private void HandlePlayerSpawn(NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.payload)) return;

            PlayerSpawnData data = JsonUtility.FromJson<PlayerSpawnData>(message.payload);

            // Don't spawn ourselves twice
            if (data.playerId == WebSocketNetworkManager.Instance.ClientId)
            {
                return;
            }

            // Spawn remote player
            PlayerState state = new PlayerState
            {
                playerId = data.playerId,
                position = data.position,
                rotation = data.rotation,
                health = 100f,
                mana = 100f
            };

            SpawnRemotePlayer(state);
        }

        private void HandleWorldState(NetworkMessage message)
        {
            if (string.IsNullOrEmpty(message.payload)) return;

            WorldStateMessage worldState = JsonUtility.FromJson<WorldStateMessage>(message.payload);

            if (worldState.players == null) return;

            foreach (var playerState in worldState.players)
            {
                // Skip our own player
                if (playerState.playerId == WebSocketNetworkManager.Instance.ClientId)
                {
                    continue;
                }

                SpawnRemotePlayer(playerState);
            }
        }

        private void HandlePlayerDisconnect(NetworkMessage message)
        {
            string disconnectedPlayerId = message.senderId;

            if (_activePlayers.TryGetValue(disconnectedPlayerId, out GameObject player))
            {
                Destroy(player);
                _activePlayers.Remove(disconnectedPlayerId);
                Debug.Log($"[PlayerSpawner] Player disconnected: {disconnectedPlayerId}");
            }
        }

        #endregion

        #region Utility

        private Vector3 GetSpawnPosition()
        {
            if (_spawnPoints.Count == 0)
            {
                return Vector3.zero;
            }

            if (_randomizeSpawnPoint)
            {
                int index = Random.Range(0, _spawnPoints.Count);
                return _spawnPoints[index].position;
            }
            else
            {
                int index = _activePlayers.Count % _spawnPoints.Count;
                return _spawnPoints[index].position;
            }
        }

        public GameObject GetLocalPlayer() => _localPlayer;

        public GameObject GetPlayer(string playerId)
        {
            _activePlayers.TryGetValue(playerId, out GameObject player);
            return player;
        }

        #endregion
    }

    [System.Serializable]
    public class PlayerSpawnData
    {
        public string playerId;
        public Vector3 position;
        public Quaternion rotation;
    }
}