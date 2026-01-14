using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuantumMechanic.Persistence
{
    /// <summary>
    /// Component-based save system for persisting game object data
    /// </summary>
    public interface ISaveable
    {
        string GetSaveID();
        object GetSaveData();
        void LoadSaveData(object data);
    }

    /// <summary>
    /// Manages component and scene persistence
    /// </summary>
    public partial class SaveManager
    {
        private Dictionary<string, ISaveable> _saveableObjects = new Dictionary<string, ISaveable>();
        private Dictionary<string, Action<SaveData>> _preSaveCallbacks = new Dictionary<string, Action<SaveData>>();
        private Dictionary<string, Action<SaveData>> _postLoadCallbacks = new Dictionary<string, Action<SaveData>>();
        private HashSet<string> _dirtyObjects = new HashSet<string>();
        private Dictionary<string, PlayerProfile> _playerProfiles = new Dictionary<string, PlayerProfile>();

        /// <summary>
        /// Register a saveable component
        /// </summary>
        public void RegisterSaveable(ISaveable saveable)
        {
            string id = saveable.GetSaveID();
            if (!_saveableObjects.ContainsKey(id))
            {
                _saveableObjects[id] = saveable;
                Debug.Log($"[SaveSystem] Registered saveable: {id}");
            }
        }

        /// <summary>
        /// Unregister a saveable component
        /// </summary>
        public void UnregisterSaveable(ISaveable saveable)
        {
            string id = saveable.GetSaveID();
            _saveableObjects.Remove(id);
            _dirtyObjects.Remove(id);
        }

        /// <summary>
        /// Mark object as dirty for incremental saves
        /// </summary>
        public void MarkDirty(string saveID)
        {
            _dirtyObjects.Add(saveID);
        }

        /// <summary>
        /// Gather all save data from registered components
        /// </summary>
        private SaveData GatherSaveData()
        {
            SaveData data = new SaveData
            {
                version = SAVE_VERSION,
                timestamp = DateTime.Now,
                playtime = Time.time,
                currentLevel = SceneManager.GetActiveScene().name,
                customData = new Dictionary<string, object>()
            };

            // Execute pre-save callbacks
            foreach (var callback in _preSaveCallbacks.Values)
            {
                callback?.Invoke(data);
            }

            // Gather player data
            data.playerData = GatherPlayerData();

            // Gather world data
            data.worldData = GatherWorldData();

            // Gather component data
            foreach (var kvp in _saveableObjects)
            {
                try
                {
                    object componentData = kvp.Value.GetSaveData();
                    data.customData[kvp.Key] = componentData;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Failed to save component {kvp.Key}: {e.Message}");
                }
            }

            // Calculate checksum
            data.checksum = CalculateChecksum(data);

            return data;
        }

        /// <summary>
        /// Apply loaded save data to game state
        /// </summary>
        private void ApplySaveData(SaveData data)
        {
            // Migrate old save formats if needed
            if (data.version < SAVE_VERSION)
            {
                data = MigrateSaveData(data);
            }

            // Apply player data
            if (data.playerData != null)
            {
                ApplyPlayerData(data.playerData);
            }

            // Apply world data
            if (data.worldData != null)
            {
                ApplyWorldData(data.worldData);
            }

            // Apply component data
            if (data.customData != null)
            {
                foreach (var kvp in data.customData)
                {
                    if (_saveableObjects.TryGetValue(kvp.Key, out ISaveable saveable))
                    {
                        try
                        {
                            saveable.LoadSaveData(kvp.Value);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SaveSystem] Failed to load component {kvp.Key}: {e.Message}");
                        }
                    }
                }
            }

            // Execute post-load callbacks
            foreach (var callback in _postLoadCallbacks.Values)
            {
                callback?.Invoke(data);
            }

            _dirtyObjects.Clear();
        }

        /// <summary>
        /// Gather player-specific save data
        /// </summary>
        private PlayerSaveData GatherPlayerData()
        {
            PlayerSaveData playerData = new PlayerSaveData
            {
                position = Vector3.zero,
                rotation = Quaternion.identity,
                health = 100f,
                maxHealth = 100f,
                inventory = new List<InventoryItemData>(),
                equippedItems = new Dictionary<string, string>(),
                stats = new Dictionary<string, float>(),
                abilities = new List<string>(),
                currency = new Dictionary<string, int>()
            };

            // TODO: Gather actual player data from PlayerController
            return playerData;
        }

        /// <summary>
        /// Apply player data to game
        /// </summary>
        private void ApplyPlayerData(PlayerSaveData data)
        {
            // TODO: Apply to PlayerController
            Debug.Log("[SaveSystem] Player data applied");
        }

        /// <summary>
        /// Gather world state save data
        /// </summary>
        private WorldSaveData GatherWorldData()
        {
            WorldSaveData worldData = new WorldSaveData
            {
                sceneName = SceneManager.GetActiveScene().name,
                sceneObjects = new List<SceneObjectData>(),
                questStates = new Dictionary<string, QuestState>(),
                worldFlags = new Dictionary<string, bool>(),
                dynamicObjects = new List<DynamicObjectData>(),
                destructibles = new List<DestructibleData>(),
                npcs = new List<NPCData>(),
                timeOfDay = 12f,
                weatherState = "Clear"
            };

            // Save scene object states
            SaveableObject[] saveables = FindObjectsOfType<SaveableObject>();
            foreach (var saveable in saveables)
            {
                worldData.sceneObjects.Add(saveable.GetObjectData());
            }

            return worldData;
        }

        /// <summary>
        /// Apply world state data to scene
        /// </summary>
        private void ApplyWorldData(WorldSaveData data)
        {
            // Restore scene objects
            SaveableObject[] saveables = FindObjectsOfType<SaveableObject>();
            foreach (var saveable in saveables)
            {
                SceneObjectData objData = data.sceneObjects.FirstOrDefault(o => o.objectID == saveable.ObjectID);
                if (objData != null)
                {
                    saveable.ApplyObjectData(objData);
                }
            }

            Debug.Log("[SaveSystem] World data applied");
        }

        /// <summary>
        /// Save only changed data (incremental save)
        /// </summary>
        public bool IncrementalSave(int slotIndex)
        {
            if (_dirtyObjects.Count == 0)
            {
                Debug.Log("[SaveSystem] No changes to save");
                return true;
            }

            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);

                SaveData existingData = File.Exists(filePath) ? ReadSaveFile(filePath) : new SaveData();
                
                // Update only dirty objects
                foreach (string dirtyID in _dirtyObjects)
                {
                    if (_saveableObjects.TryGetValue(dirtyID, out ISaveable saveable))
                    {
                        existingData.customData[dirtyID] = saveable.GetSaveData();
                    }
                }

                existingData.timestamp = DateTime.Now;
                existingData.checksum = CalculateChecksum(existingData);

                WriteSaveFile(filePath, existingData);
                _dirtyObjects.Clear();

                Debug.Log($"[SaveSystem] Incremental save completed: {_dirtyObjects.Count} objects updated");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Incremental save failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Register pre-save callback
        /// </summary>
        public void RegisterPreSaveCallback(string id, Action<SaveData> callback)
        {
            _preSaveCallbacks[id] = callback;
        }

        /// <summary>
        /// Register post-load callback
        /// </summary>
        public void RegisterPostLoadCallback(string id, Action<SaveData> callback)
        {
            _postLoadCallbacks[id] = callback;
        }

        /// <summary>
        /// Migrate old save format to current version
        /// </summary>
        private SaveData MigrateSaveData(SaveData oldData)
        {
            Debug.Log($"[SaveSystem] Migrating save data from version {oldData.version} to {SAVE_VERSION}");
            
            // Add migration logic here for different versions
            // Example: if (oldData.version == 0) { /* upgrade to v1 */ }
            
            oldData.version = SAVE_VERSION;
            return oldData;
        }

        /// <summary>
        /// Save player profile
        /// </summary>
        public void SavePlayerProfile(string profileID, PlayerProfile profile)
        {
            _playerProfiles[profileID] = profile;
            
            string profilePath = Path.Combine(_savePath, $"profile_{profileID}.json");
            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(profilePath, json);
            
            Debug.Log($"[SaveSystem] Player profile saved: {profileID}");
        }

        /// <summary>
        /// Load player profile
        /// </summary>
        public PlayerProfile LoadPlayerProfile(string profileID)
        {
            if (_playerProfiles.TryGetValue(profileID, out PlayerProfile profile))
            {
                return profile;
            }

            string profilePath = Path.Combine(_savePath, $"profile_{profileID}.json");
            if (File.Exists(profilePath))
            {
                string json = File.ReadAllText(profilePath);
                profile = JsonConvert.DeserializeObject<PlayerProfile>(json);
                _playerProfiles[profileID] = profile;
                return profile;
            }

            return null;
        }
    }

    /// <summary>
    /// Player save data structure
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public float maxHealth;
        public List<InventoryItemData> inventory;
        public Dictionary<string, string> equippedItems;
        public Dictionary<string, float> stats;
        public List<string> abilities;
        public Dictionary<string, int> currency;
    }

    /// <summary>
    /// World save data structure
    /// </summary>
    [Serializable]
    public class WorldSaveData
    {
        public string sceneName;
        public List<SceneObjectData> sceneObjects;
        public Dictionary<string, QuestState> questStates;
        public Dictionary<string, bool> worldFlags;
        public List<DynamicObjectData> dynamicObjects;
        public List<DestructibleData> destructibles;
        public List<NPCData> npcs;
        public float timeOfDay;
        public string weatherState;
    }

    [Serializable]
    public class SceneObjectData
    {
        public string objectID;
        public Vector3 position;
        public Quaternion rotation;
        public bool isActive;
    }

    [Serializable]
    public class InventoryItemData
    {
        public string itemID;
        public int quantity;
        public Dictionary<string, object> metadata;
    }

    [Serializable]
    public class QuestState
    {
        public string questID;
        public bool isCompleted;
        public int progress;
    }

    [Serializable]
    public class DynamicObjectData
    {
        public string prefabID;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class DestructibleData
    {
        public string objectID;
        public bool isDestroyed;
    }

    [Serializable]
    public class NPCData
    {
        public string npcID;
        public Vector3 position;
        public string state;
    }

    [Serializable]
    public class PlayerProfile
    {
        public string profileID;
        public string playerName;
        public DateTime createdDate;
        public float totalPlaytime;
        public Dictionary<string, bool> achievements;
        public Dictionary<string, int> statistics;
    }

    /// <summary>
    /// Component for marking GameObjects as saveable
    /// </summary>
    public class SaveableObject : MonoBehaviour, ISaveable
    {
        [SerializeField] private string objectID;
        public string ObjectID => objectID;

        private void Awake()
        {
            if (string.IsNullOrEmpty(objectID))
            {
                objectID = Guid.NewGuid().ToString();
            }
        }

        private void OnEnable()
        {
            SaveManager.Instance.RegisterSaveable(this);
        }

        private void OnDisable()
        {
            SaveManager.Instance.UnregisterSaveable(this);
        }

        public string GetSaveID() => objectID;

        public object GetSaveData()
        {
            return new SceneObjectData
            {
                objectID = objectID,
                position = transform.position,
                rotation = transform.rotation,
                isActive = gameObject.activeSelf
            };
        }

        public void LoadSaveData(object data)
        {
            if (data is SceneObjectData objData)
            {
                ApplyObjectData(objData);
            }
        }

        public SceneObjectData GetObjectData()
        {
            return (SceneObjectData)GetSaveData();
        }

        public void ApplyObjectData(SceneObjectData data)
        {
            transform.position = data.position;
            transform.rotation = data.rotation;
            gameObject.SetActive(data.isActive);
        }
    }
}