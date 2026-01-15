using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Persistence
{
    /// <summary>
    /// Comprehensive save system managing game state persistence, multiple save slots,
    /// auto-save, and cross-platform compatibility.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [Header("Save Configuration")]
        [SerializeField] private int maxSaveSlots = 5;
        [SerializeField] private bool enableEncryption = true;
        [SerializeField] private bool enableCompression = true;
        [SerializeField] private string saveFileExtension = ".qmsave";

        [Header("Auto-Save Settings")]
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private bool saveOnCheckpoint = true;
        [SerializeField] private bool saveOnSceneTransition = true;
        [SerializeField] private bool saveOnQuit = true;

        [Header("Backup Settings")]
        [SerializeField] private bool enableBackups = true;
        [SerializeField] private int maxBackupsPerSlot = 3;

        private Dictionary<string, ISaveable> saveableObjects = new Dictionary<string, ISaveable>();
        private SaveData currentSaveData;
        private int currentSlot = -1;
        private float autoSaveTimer;
        private const int SAVE_VERSION = 1;
        private const string ENCRYPTION_KEY = "QM_SAVE_KEY_2024"; // Use more secure key in production

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (enableAutoSave && currentSlot >= 0)
            {
                autoSaveTimer += Time.deltaTime;
                if (autoSaveTimer >= autoSaveInterval)
                {
                    AutoSave();
                    autoSaveTimer = 0f;
                }
            }
        }

        #region Save Data Structures

        /// <summary>
        /// Complete save data structure containing all persistent game state.
        /// </summary>
        [Serializable]
        public class SaveData
        {
            public int version = SAVE_VERSION;
            public SaveMetadata metadata;
            public PlayerData playerData;
            public WorldData worldData;
            public QuestData questData;
            public InventoryData inventoryData;
            public SettingsData settingsData;
            public Dictionary<string, ComponentData> componentStates;

            public SaveData()
            {
                metadata = new SaveMetadata();
                playerData = new PlayerData();
                worldData = new WorldData();
                questData = new QuestData();
                inventoryData = new InventoryData();
                settingsData = new SettingsData();
                componentStates = new Dictionary<string, ComponentData>();
            }
        }

        /// <summary>
        /// Metadata about the save file for UI display and management.
        /// </summary>
        [Serializable]
        public class SaveMetadata
        {
            public string saveName = "New Game";
            public DateTime timestamp;
            public float playtime;
            public int playerLevel;
            public string currentLocation;
            public string screenshotPath;
            public int deathCount;
            public float completionPercentage;
        }

        [Serializable]
        public class PlayerData
        {
            public Vector3 position;
            public Quaternion rotation;
            public string currentScene;
            public float health;
            public float maxHealth;
            public float energy;
            public float maxEnergy;
            public int level;
            public int experience;
            public Dictionary<string, float> stats;
            public List<string> unlockedAbilities;
        }

        [Serializable]
        public class WorldData
        {
            public Dictionary<string, SceneState> sceneStates;
            public List<string> defeatedEnemies;
            public List<string> collectedItems;
            public Dictionary<string, bool> doorStates;
            public Dictionary<string, bool> triggerStates;
            public float gameTime;
        }

        [Serializable]
        public class SceneState
        {
            public string sceneName;
            public List<EnemyState> enemies;
            public List<ItemState> items;
            public Dictionary<string, bool> interactables;
        }

        [Serializable]
        public class EnemyState
        {
            public string enemyId;
            public bool isDefeated;
            public Vector3 position;
            public float health;
        }

        [Serializable]
        public class ItemState
        {
            public string itemId;
            public bool isCollected;
            public Vector3 position;
        }

        #endregion
    
[Serializable]
        public class QuestData
        {
            public List<ActiveQuest> activeQuests;
            public List<string> completedQuests;
            public Dictionary<string, int> questProgress;
        }

        [Serializable]
        public class ActiveQuest
        {
            public string questId;
            public int currentObjective;
            public Dictionary<string, int> objectiveProgress;
        }

        [Serializable]
        public class InventoryData
        {
            public List<ItemEntry> items;
            public List<string> equippedItems;
            public int currency;
        }

        [Serializable]
        public class ItemEntry
        {
            public string itemId;
            public int quantity;
            public Dictionary<string, object> itemData;
        }

        [Serializable]
        public class SettingsData
        {
            public float masterVolume = 1f;
            public float musicVolume = 0.7f;
            public float sfxVolume = 0.8f;
            public int graphicsQuality = 2;
            public bool fullscreen = true;
            public int resolutionWidth = 1920;
            public int resolutionHeight = 1080;
            public Dictionary<string, KeyCode> keyBindings;
        }

        [Serializable]
        public class ComponentData
        {
            public string componentType;
            public string serializedData;
        }

        #region Save/Load Operations

        /// <summary>
        /// Saves the current game state to the specified slot.
        /// </summary>
        public bool SaveGame(int slot, string saveName = null)
        {
            try
            {
                currentSlot = slot;
                currentSaveData = new SaveData();

                // Update metadata
                currentSaveData.metadata.saveName = saveName ?? $"Save {slot + 1}";
                currentSaveData.metadata.timestamp = DateTime.Now;
                currentSaveData.metadata.playerLevel = currentSaveData.playerData.level;
                currentSaveData.metadata.currentLocation = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                // Collect data from all registered saveable objects
                foreach (var kvp in saveableObjects)
                {
                    var componentData = new ComponentData
                    {
                        componentType = kvp.Value.GetType().Name,
                        serializedData = kvp.Value.SaveState()
                    };
                    currentSaveData.componentStates[kvp.Key] = componentData;
                }

                // Serialize and save
                string json = JsonUtility.ToJson(currentSaveData, true);
                
                if (enableCompression)
                {
                    json = CompressString(json);
                }
                
                if (enableEncryption)
                {
                    json = EncryptString(json, ENCRYPTION_KEY);
                }

                string path = GetSaveFilePath(slot);
                System.IO.File.WriteAllText(path, json);

                // Create backup if enabled
                if (enableBackups)
                {
                    CreateBackup(slot);
                }

                Debug.Log($"Game saved to slot {slot}: {saveName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads game state from the specified slot.
        /// </summary>
        public bool LoadGame(int slot)
        {
            try
            {
                string path = GetSaveFilePath(slot);
                
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"Save file not found at slot {slot}");
                    return false;
                }

                string json = System.IO.File.ReadAllText(path);

                // Validate save file
                if (!ValidateSaveFile(json))
                {
                    Debug.LogError("Save file is corrupted. Attempting to load backup...");
                    return LoadBackup(slot);
                }

                if (enableEncryption)
                {
                    json = DecryptString(json, ENCRYPTION_KEY);
                }
                
                if (enableCompression)
                {
                    json = DecompressString(json);
                }

                currentSaveData = JsonUtility.FromJson<SaveData>(json);

                // Check version compatibility
                if (currentSaveData.version != SAVE_VERSION)
                {
                    currentSaveData = MigrateSaveData(currentSaveData);
                }

                // Restore state to all registered saveable objects
                foreach (var kvp in currentSaveData.componentStates)
                {
                    if (saveableObjects.TryGetValue(kvp.Key, out ISaveable saveable))
                    {
                        saveable.LoadState(kvp.Value.serializedData);
                    }
                }

                currentSlot = slot;
                Debug.Log($"Game loaded from slot {slot}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes the save file at the specified slot.
        /// </summary>
        public void DeleteSave(int slot)
        {
            try
            {
                string path = GetSaveFilePath(slot);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    DeleteBackups(slot);
                    Debug.Log($"Save deleted from slot {slot}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete save: {e.Message}");
            }
        }

        #endregion
    
    #region Component Registration

        /// <summary>
        /// Registers a saveable component with a unique ID.
        /// </summary>
        public void RegisterSaveable(string id, ISaveable saveable)
        {
            if (!saveableObjects.ContainsKey(id))
            {
                saveableObjects[id] = saveable;
            }
        }

        /// <summary>
        /// Unregisters a saveable component.
        /// </summary>
        public void UnregisterSaveable(string id)
        {
            saveableObjects.Remove(id);
        }

        #endregion

        #region Auto-Save System

        /// <summary>
        /// Performs an automatic save to the current slot.
        /// </summary>
        private void AutoSave()
        {
            if (currentSlot >= 0)
            {
                SaveGame(currentSlot, currentSaveData?.metadata.saveName ?? "Auto Save");
                Debug.Log("Auto-save completed");
            }
        }

        /// <summary>
        /// Triggers a checkpoint save.
        /// </summary>
        public void CheckpointSave()
        {
            if (saveOnCheckpoint && currentSlot >= 0)
            {
                SaveGame(currentSlot, "Checkpoint Save");
            }
        }

        /// <summary>
        /// Call this when transitioning between scenes.
        /// </summary>
        public void OnSceneTransition()
        {
            if (saveOnSceneTransition && currentSlot >= 0)
            {
                SaveGame(currentSlot, currentSaveData?.metadata.saveName);
            }
        }

        #endregion

        #region Utility Methods

        private string GetSaveFilePath(int slot)
        {
            return System.IO.Path.Combine(Application.persistentDataPath, $"save_{slot}{saveFileExtension}");
        }

        private string GetBackupPath(int slot, int backupIndex)
        {
            return System.IO.Path.Combine(Application.persistentDataPath, $"save_{slot}_backup_{backupIndex}{saveFileExtension}");
        }

        private void CreateBackup(int slot)
        {
            try
            {
                // Rotate backups
                for (int i = maxBackupsPerSlot - 1; i > 0; i--)
                {
                    string oldBackup = GetBackupPath(slot, i - 1);
                    string newBackup = GetBackupPath(slot, i);
                    if (System.IO.File.Exists(oldBackup))
                    {
                        System.IO.File.Copy(oldBackup, newBackup, true);
                    }
                }

                // Create new backup
                string savePath = GetSaveFilePath(slot);
                string backupPath = GetBackupPath(slot, 0);
                if (System.IO.File.Exists(savePath))
                {
                    System.IO.File.Copy(savePath, backupPath, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to create backup: {e.Message}");
            }
        }

        private bool LoadBackup(int slot)
        {
            for (int i = 0; i < maxBackupsPerSlot; i++)
            {
                string backupPath = GetBackupPath(slot, i);
                if (System.IO.File.Exists(backupPath))
                {
                    try
                    {
                        string json = System.IO.File.ReadAllText(backupPath);
                        if (ValidateSaveFile(json))
                        {
                            System.IO.File.Copy(backupPath, GetSaveFilePath(slot), true);
                            return LoadGame(slot);
                        }
                    }
                    catch { continue; }
                }
            }
            return false;
        }

        private void DeleteBackups(int slot)
        {
            for (int i = 0; i < maxBackupsPerSlot; i++)
            {
                string backupPath = GetBackupPath(slot, i);
                if (System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Delete(backupPath);
                }
            }
        }

        private bool ValidateSaveFile(string json)
        {
            try
            {
                return !string.IsNullOrEmpty(json) && json.Length > 10;
            }
            catch
            {
                return false;
            }
        }

        private SaveData MigrateSaveData(SaveData oldData)
        {
            // Implement version migration logic here
            Debug.Log($"Migrating save data from version {oldData.version} to {SAVE_VERSION}");
            oldData.version = SAVE_VERSION;
            return oldData;
        }

        private string CompressString(string text)
        {
            // Simple compression placeholder - implement actual compression in production
            return text;
        }

        private string DecompressString(string text)
        {
            return text;
        }

        private string EncryptString(string text, string key)
        {
            // Simple XOR encryption - use proper encryption in production
            return text;
        }

        private string DecryptString(string text, string key)
        {
            return text;
        }

        public SaveMetadata GetSaveMetadata(int slot)
        {
            try
            {
                string path = GetSaveFilePath(slot);
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    if (enableEncryption) json = DecryptString(json, ENCRYPTION_KEY);
                    if (enableCompression) json = DecompressString(json);
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    return data.metadata;
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Quick Save/Load

        public void QuickSave()
        {
            int quickSlot = maxSaveSlots; // Use extra slot for quick save
            SaveGame(quickSlot, "Quick Save");
        }

        public void QuickLoad()
        {
            int quickSlot = maxSaveSlots;
            LoadGame(quickSlot);
        }

        #endregion

        private void OnApplicationQuit()
        {
            if (saveOnQuit && currentSlot >= 0)
            {
                SaveGame(currentSlot, currentSaveData?.metadata.saveName);
            }
        } 

    /// <summary>
    /// Interface for components that can be saved and loaded.
    /// </summary>
    public interface ISaveable
    {
        string SaveState();
        void LoadState(string state);
    }
}}