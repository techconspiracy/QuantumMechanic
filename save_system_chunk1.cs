using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using System.IO.Compression;

namespace QuantumMechanic.Persistence
{
    /// <summary>
    /// Core save system managing all save/load operations, file management, and persistence
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
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
        private void Awake()
        {
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
        private SaveData GatherSaveData() => new SaveData(); // Implemented in Chunk 2
        private void ApplySaveData(SaveData data) { } // Implemented in Chunk 2
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

    [Serializable]
    public class PlayerSaveData { }
    
    [Serializable]
    public class WorldSaveData { }
}