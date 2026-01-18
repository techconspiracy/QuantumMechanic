using System;
using QuantumMechanic;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace QuantumMechanic.Persistence
{
    /// <summary>
    /// Core save system managing all save/load operations, file management, and persistence
    /// </summary>
    [QuantumSystem(InitializationPhase.GameLogic, priority: 100)]
    public class SaveManager : BaseGameSystem, IUpdateableSystem
    {
    protected override async Awaitable OnInitialize()
    {
        // TODO: Move Awake/Start logic here
        // Initialization code goes here
        
        await Awaitable.NextFrameAsync();
        
        Log("Initialized");
    }

        private static SaveManager _instance;
        public static SaveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<SaveManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("SaveManager");
                        _instance = obj.AddComponent<SaveManager>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }

        [Header("Save Configuration")]
        [SerializeField] private int maxSaveSlots = 10;
        [SerializeField] private bool useEncryption = true;
        [SerializeField] private bool useCompression = true;
        [SerializeField] private string saveFileExtension = ".qmsave";
        
        private const string SAVE_FOLDER = "Saves";
        private const string QUICK_SAVE_NAME = "quicksave";
        private const string AUTO_SAVE_PREFIX = "autosave";
        private const int SAVE_VERSION = 1;
        
        private string _savePath;
        private Dictionary<int, SaveSlot> _saveSlots = new Dictionary<int, SaveSlot>();

        /// <summary>
        /// Initialize save system and scan for existing saves
        /// </summary>
        private // Converted to OnInitialize
    // void Awake() {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeSavePath();
            ScanSaveFiles();
        }

        /// <summary>
        /// Initialize platform-specific save directory path
        /// </summary>
        private void InitializeSavePath()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SAVE_FOLDER);
            
            if (!Directory.Exists(_savePath))
            {
                Directory.CreateDirectory(_savePath);
            }
            
            Debug.Log($"[SaveSystem] Save path initialized: {_savePath}");
        }

        /// <summary>
        /// Create a new save in the specified slot
        /// </summary>
        public bool SaveGame(int slotIndex, string saveName = null)
        {
            try
            {
                SaveData data = GatherSaveData();
                data.slotIndex = slotIndex;
                data.saveName = saveName ?? $"Save {slotIndex + 1}";
                data.timestamp = DateTime.Now;
                data.version = SAVE_VERSION;
                
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);
                
                WriteSaveFile(filePath, data);
                
                SaveSlot slot = new SaveSlot
                {
                    slotIndex = slotIndex,
                    saveName = data.saveName,
                    timestamp = data.timestamp,
                    playtime = data.playtime,
                    levelName = data.currentLevel,
                    filePath = filePath
                };
                
                _saveSlots[slotIndex] = slot;
                
                Debug.Log($"[SaveSystem] Game saved to slot {slotIndex}: {saveName}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to save game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load game from specified slot
        /// </summary>
        public bool LoadGame(int slotIndex)
        {
            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);
                
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"[SaveSystem] Save file not found: {filePath}");
                    return false;
                }
                
                SaveData data = ReadSaveFile(filePath);
                
                if (!ValidateSaveData(data))
                {
                    Debug.LogError("[SaveSystem] Save data validation failed");
                    return false;
                }
                
                ApplySaveData(data);
                
                Debug.Log($"[SaveSystem] Game loaded from slot {slotIndex}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to load game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete save from specified slot
        /// </summary>
        public bool DeleteSave(int slotIndex)
        {
            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _saveSlots.Remove(slotIndex);
                    Debug.Log($"[SaveSystem] Deleted save from slot {slotIndex}");
                    return true;
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to delete save: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all available save slots
        /// </summary>
        public SaveSlot[] GetSaveSlots()
        {
            List<SaveSlot> slots = new List<SaveSlot>(_saveSlots.Values);
            slots.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
            return slots.ToArray();
        }

        /// <summary>
        /// Write save data to file with compression and encryption
        /// </summary>
        private void WriteSaveFile(string filePath, SaveData data)
        {
            string json = JsonConvert.SerializeObject(data, Formatting.None);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            
            if (useCompression)
            {
                bytes = CompressData(bytes);
            }
            
            if (useEncryption)
            {
                bytes = EncryptData(bytes);
            }
            
            File.WriteAllBytes(filePath, bytes);
        }

        /// <summary>
        /// Read and decrypt/decompress save file
        /// </summary>
        private SaveData ReadSaveFile(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            
            if (useEncryption)
            {
                bytes = DecryptData(bytes);
            }
            
            if (useCompression)
            {
                bytes = DecompressData(bytes);
            }
            
            string json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<SaveData>(json);
        }

        /// <summary>
        /// Compress data using GZip
        /// </summary>
        private byte[] CompressData(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>
        /// Decompress GZip data
        /// </summary>
        private byte[] DecompressData(byte[] data)
        {
            using (MemoryStream input = new MemoryStream(data))
            using (MemoryStream output = new MemoryStream())
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// Encrypt data using AES
        /// </summary>
        private byte[] EncryptData(byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = GetEncryptionKey();
                aes.IV = new byte[16];
                
                using (MemoryStream output = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(data, 0, data.Length);
                    }
                    return output.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypt AES encrypted data
        /// </summary>
        private byte[] DecryptData(byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = GetEncryptionKey();
                aes.IV = new byte[16];
                
                using (MemoryStream input = new MemoryStream(data))
                using (CryptoStream cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (MemoryStream output = new MemoryStream())
                {
                    cryptoStream.CopyTo(output);
                    return output.ToArray();
                }
            }
        }

        /// <summary>
        /// Get encryption key (should be secured properly in production)
        /// </summary>
        private byte[] GetEncryptionKey()
        {
            string key = SystemInfo.deviceUniqueIdentifier + "QuantumMechanic";
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            }
        }

        /// <summary>
        /// Validate save data integrity
        /// </summary>
        private bool ValidateSaveData(SaveData data)
        {
            if (data == null) return false;
            if (data.version > SAVE_VERSION) return false;
            
            string checksum = CalculateChecksum(data);
            return checksum == data.checksum;
        }

        /// <summary>
        /// Calculate checksum for save data validation
        /// </summary>
        private string CalculateChecksum(SaveData data)
        {
            string temp = data.checksum;
            data.checksum = string.Empty;
            
            string json = JsonConvert.SerializeObject(data);
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(json));
                string checksum = BitConverter.ToString(hash).Replace("-", "");
                data.checksum = temp;
                return checksum;
            }
        }

        /// <summary>
        /// Scan directory for existing save files
        /// </summary>
        private void ScanSaveFiles()
        {
            _saveSlots.Clear();
            
            if (!Directory.Exists(_savePath)) return;
            
            string[] files = Directory.GetFiles(_savePath, $"*{saveFileExtension}");
            foreach (string file in files)
            {
                try
                {
                    SaveData data = ReadSaveFile(file);
                    SaveSlot slot = new SaveSlot
                    {
                        slotIndex = data.slotIndex,
                        saveName = data.saveName,
                        timestamp = data.timestamp,
                        playtime = data.playtime,
                        levelName = data.currentLevel,
                        filePath = file
                    };
                    _saveSlots[data.slotIndex] = slot;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveSystem] Failed to read save file {file}: {e.Message}");
                }
            }
        }

        private string GetSaveFileName(int slotIndex) => $"save_{slotIndex}{saveFileExtension}";
        // GatherSaveData and ApplySaveData implemented below (from chunk2)
        // private SaveData GatherSaveData() => new SaveData(); // replaced
        // private void ApplySaveData(SaveData data) { } // replaced

        // Additional members from component-based system (chunk2)
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

        // Advanced features from chunk3

        [Header("Auto-Save Settings")]
        [SerializeField] private bool enableAutoSave = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
        [SerializeField] private int maxBackups = 3;
        
        [Header("Cloud Settings")]
        [SerializeField] private bool enableCloudSync = false;
        [SerializeField] private CloudProvider cloudProvider = CloudProvider.None;

        private float _autoSaveTimer;
        private bool _isAutoSaving;
        private List<string> _checkpointHistory = new List<string>();
        private Dictionary<string, Achievement> _achievements = new Dictionary<string, Achievement>();
        private Stopwatch _performanceTimer = new Stopwatch();

        private public void OnUpdate(float deltaTime
    // Note: Use deltaTime parameter instead of Time.deltaTime
    // Original signature: void Update()
        {
            if (enableAutoSave)
            {
                _autoSaveTimer += Time.deltaTime;
                if (_autoSaveTimer >= autoSaveInterval)
                {
                    StartCoroutine(AutoSaveCoroutine());
                    _autoSaveTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Quick save to dedicated slot
        /// </summary>
        public bool QuickSave()
        {
            const int QUICK_SAVE_SLOT = -1;
            bool success = SaveGame(QUICK_SAVE_SLOT, QUICK_SAVE_NAME);
            
            if (success)
            {
                Debug.Log("[SaveSystem] Quick save completed");
            }
            
            return success;
        }

        /// <summary>
        /// Load quick save
        /// </summary>
        public bool QuickLoad()
        {
            const int QUICK_SAVE_SLOT = -1;
            return LoadGame(QUICK_SAVE_SLOT);
        }

        /// <summary>
        /// Auto-save coroutine with performance tracking
        /// </summary>
        private IEnumerator AutoSaveCoroutine()
        {
            if (_isAutoSaving) yield break;
            
            _isAutoSaving = true;
            _performanceTimer.Restart();
            
            Debug.Log("[SaveSystem] Auto-save started...");
            
            // Wait for end of frame to avoid interrupting gameplay
            yield return new WaitForEndOfFrame();
            
            int autoSaveSlot = -2;
            bool success = SaveGame(autoSaveSlot, $"{AUTO_SAVE_PREFIX}_{DateTime.Now:yyyyMMdd_HHmmss}");
            
            _performanceTimer.Stop();
            
            if (success)
            {
                Debug.Log($"[SaveSystem] Auto-save completed in {_performanceTimer.ElapsedMilliseconds}ms");
                CreateBackup(autoSaveSlot);
            }
            
            _isAutoSaving = false;
        }

        /// <summary>
        /// Save at checkpoint with identifier
        /// </summary>
        public bool AutoSave(string checkpointID)
        {
            int checkpointSlot = -3;
            bool success = SaveGame(checkpointSlot, $"checkpoint_{checkpointID}");
            
            if (success)
            {
                _checkpointHistory.Add(checkpointID);
                Debug.Log($"[SaveSystem] Checkpoint saved: {checkpointID}");
            }
            
            return success;
        }

        /// <summary>
        /// Create backup copy of save file
        /// </summary>
        private void CreateBackup(int slotIndex)
        {
            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);
                
                if (!File.Exists(filePath)) return;
                
                // Manage backup rotation
                string backupFolder = Path.Combine(_savePath, "Backups");
                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }
                
                // Rotate old backups
                for (int i = maxBackups - 1; i > 0; i--)
                {
                    string oldBackup = Path.Combine(backupFolder, $"{fileName}.bak{i}");
                    string newBackup = Path.Combine(backupFolder, $"{fileName}.bak{i + 1}");
                    
                    if (File.Exists(oldBackup))
                    {
                        if (File.Exists(newBackup))
                        {
                            File.Delete(newBackup);
                        }
                        File.Move(oldBackup, newBackup);
                    }
                }
                
                // Create new backup
                string latestBackup = Path.Combine(backupFolder, $"{fileName}.bak1");
                File.Copy(filePath, latestBackup, true);
                
                Debug.Log($"[SaveSystem] Backup created: {latestBackup}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to create backup: {e.Message}");
            }
        }

        /// <summary>
        /// Restore from backup
        /// </summary>
        public bool RestoreFromBackup(int slotIndex, int backupIndex = 1)
        {
            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string backupPath = Path.Combine(_savePath, "Backups", $"{fileName}.bak{backupIndex}");
                string filePath = Path.Combine(_savePath, fileName);
                
                if (!File.Exists(backupPath))
                {
                    Debug.LogWarning($"[SaveSystem] Backup not found: {backupPath}");
                    return false;
                }
                
                File.Copy(backupPath, filePath, true);
                Debug.Log($"[SaveSystem] Restored from backup {backupIndex}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to restore backup: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unlock achievement
        /// </summary>
        public void UnlockAchievement(string achievementID)
        {
            if (_achievements.TryGetValue(achievementID, out Achievement achievement))
            {
                if (!achievement.isUnlocked)
                {
                    achievement.isUnlocked = true;
                    achievement.unlockDate = DateTime.Now;
                    
                    Debug.Log($"[SaveSystem] Achievement unlocked: {achievementID}");
                    
                    if (enableCloudSync)
                    {
                        SyncAchievementToCloud(achievement);
                    }
                }
            }
        }

        /// <summary>
        /// Get achievement progress
        /// </summary>
        public Achievement GetAchievement(string achievementID)
        {
            return _achievements.TryGetValue(achievementID, out Achievement achievement) ? achievement : null;
        }

        /// <summary>
        /// Track analytics event
        /// </summary>
        public void TrackAnalyticsEvent(string eventName, Dictionary<string, object> parameters)
        {
            Debug.Log($"[SaveSystem] Analytics: {eventName}");
            // TODO: Integrate with analytics provider (Unity Analytics, Firebase, etc.)
        }

        /// <summary>
        /// Enable or disable cloud synchronization
        /// </summary>
        public void EnableCloudSync(bool enable)
        {
            enableCloudSync = enable;
            
            if (enable)
            {
                SyncWithCloud();
            }
        }

        /// <summary>
        /// Sync save data with cloud service
        /// </summary>
        public void SyncWithCloud()
        {
            if (!enableCloudSync)
            {
                Debug.LogWarning("[SaveSystem] Cloud sync is disabled");
                return;
            }

            switch (cloudProvider)
            {
                case CloudProvider.Steam:
                    SyncWithSteamCloud();
                    break;
                case CloudProvider.PlayStationNetwork:
                    SyncWithPSN();
                    break;
                case CloudProvider.XboxLive:
                    SyncWithXboxLive();
                    break;
                case CloudProvider.NintendoSwitch:
                    SyncWithNintendoCloud();
                    break;
                default:
                    Debug.LogWarning("[SaveSystem] No cloud provider configured");
                    break;
            }
        }

        private void SyncWithSteamCloud()
        {
            Debug.Log("[SaveSystem] Syncing with Steam Cloud...");
            // TODO: Implement Steamworks API integration
        }

        private void SyncWithPSN()
        {
            Debug.Log("[SaveSystem] Syncing with PlayStation Network...");
            // TODO: Implement PS SDK integration
        }

        private void SyncWithXboxLive()
        {
            Debug.Log("[SaveSystem] Syncing with Xbox Live...");
            // TODO: Implement Xbox Live SDK integration
        }

        private void SyncWithNintendoCloud()
        {
            Debug.Log("[SaveSystem] Syncing with Nintendo Cloud...");
            // TODO: Implement Nintendo SDK integration
        }

        private void SyncAchievementToCloud(Achievement achievement)
        {
            Debug.Log($"[SaveSystem] Syncing achievement to cloud: {achievement.achievementID}");
            // TODO: Platform-specific achievement sync
        }

        /// <summary>
        /// Get detailed save file information for debugging
        /// </summary>
        public SaveFileInfo GetSaveFileInfo(int slotIndex)
        {
            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);
                
                if (!File.Exists(filePath))
                {
                    return null;
                }

                FileInfo fileInfo = new FileInfo(filePath);
                SaveData data = ReadSaveFile(filePath);

                return new SaveFileInfo
                {
                    slotIndex = slotIndex,
                    filePath = filePath,
                    fileSize = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTime,
                    version = data.version,
                    isValid = ValidateSaveData(data),
                    componentCount = data.customData?.Count ?? 0
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to get save file info: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Export save data for debugging
        /// </summary>
        public string ExportSaveDataAsJSON(int slotIndex)
        {
            try
            {
                string fileName = GetSaveFileName(slotIndex);
                string filePath = Path.Combine(_savePath, fileName);
                
                SaveData data = ReadSaveFile(filePath);
                return JsonConvert.SerializeObject(data, Formatting.Indented);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to export save data: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Measure save/load performance
        /// </summary>
        public PerformanceMetrics MeasureSavePerformance(int slotIndex)
        {
            PerformanceMetrics metrics = new PerformanceMetrics();
            
            _performanceTimer.Restart();
            SaveGame(slotIndex, "performance_test");
            _performanceTimer.Stop();
            metrics.saveTime = _performanceTimer.ElapsedMilliseconds;
            
            _performanceTimer.Restart();
            LoadGame(slotIndex);
            _performanceTimer.Stop();
            metrics.loadTime = _performanceTimer.ElapsedMilliseconds;
            
            string filePath = Path.Combine(_savePath, GetSaveFileName(slotIndex));
            FileInfo fileInfo = new FileInfo(filePath);
            metrics.fileSize = fileInfo.Length;
            
            Debug.Log($"[SaveSystem] Performance: Save={metrics.saveTime}ms, Load={metrics.loadTime}ms, Size={metrics.fileSize} bytes");
            
            return metrics;
        }
    }

    /// <summary>
    /// Save slot metadata for UI display
    /// </summary>
    [Serializable]
    public class SaveSlot
    {
        public int slotIndex;
        public string saveName;
        public DateTime timestamp;
        public float playtime;
        public string levelName;
        public string filePath;
    }

    /// <summary>
    /// Main save data container
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version;
        public int slotIndex;
        public string saveName;
        public DateTime timestamp;
        public float playtime;
        public string currentLevel;
        public string checksum;
        
        public PlayerSaveData playerData;
        public WorldSaveData worldData;
        public Dictionary<string, object> customData;
    }

    public interface ISaveable
    {
        string GetSaveID();
        object GetSaveData();
        void LoadSaveData(object data);
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

        private // Converted to OnInitialize
    // void Awake() {
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

    public enum CloudProvider
    {
        None,
        Steam,
        PlayStationNetwork,
        XboxLive,
        NintendoSwitch,
        Custom
    }

    [Serializable]
    public class Achievement
    {
        public string achievementID;
        public string name;
        public string description;
        public bool isUnlocked;
        public DateTime unlockDate;
        public int progress;
        public int maxProgress;
    }

    public class SaveFileInfo
    {
        public int slotIndex;
        public string filePath;
        public long fileSize;
        public DateTime lastModified;
        public int version;
        public bool isValid;
        public int componentCount;
    }

    public class PerformanceMetrics
    {
        public long saveTime;
        public long loadTime;
        public long fileSize;
    }
}