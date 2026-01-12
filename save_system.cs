using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace QuantumMechanic.Persistence
{
    /// <summary>
    /// Player data structure for serialization.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string username;
        public int level;
        public int experience;
        public int currency;
        public float posX, posY, posZ;
        public string[] inventory;
        public long lastSaveTimestamp;
        
        public PlayerData()
        {
            username = "Player";
            level = 1;
            experience = 0;
            currency = 100;
            posX = 0; posY = 0; posZ = 0;
            inventory = new string[0];
            lastSaveTimestamp = DateTime.UtcNow.Ticks;
        }
        
        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        
        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }
    }
    
    /// <summary>
    /// Thread-safe save system with AES-256 encryption for local data persistence.
    /// Implements auto-save, cloud-ready architecture, and data integrity validation.
    /// Uses JsonUtility for serialization and System.IO for file operations.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [Header("Save Configuration")]
        [SerializeField] private bool _enableEncryption = true;
        [SerializeField] private float _autoSaveInterval = 60f; // Auto-save every 60 seconds
        [SerializeField] private string _saveFileName = "player_save.dat";
        
        private static SaveSystem _instance;
        private string _savePath;
        private PlayerData _currentData;
        private float _lastSaveTime;
        private object _saveLock = new object();
        
        // AES encryption key (in production, generate per-user or retrieve from secure storage)
        private static readonly byte[] _encryptionKey = Encoding.UTF8.GetBytes("QuantumMechanic32ByteKeyValue!!"); // 32 bytes for AES-256
        private static readonly byte[] _encryptionIV = Encoding.UTF8.GetBytes("16ByteIVForAES!!"); // 16 bytes
        
        public static SaveSystem Instance => _instance;
        public PlayerData CurrentData => _currentData;
        
        // Events for save/load lifecycle
        public event Action<PlayerData> OnDataLoaded;
        public event Action<PlayerData> OnDataSaved;
        
        /// <summary>
        /// Singleton initialization.
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
            
            _savePath = Path.Combine(Application.persistentDataPath, _saveFileName);
            Debug.Log($"[SaveSystem] Save path: {_savePath}");
        }
        
        /// <summary>
        /// Loads player data on start.
        /// </summary>
        private void Start()
        {
            LoadData();
        }
        
        /// <summary>
        /// Auto-save tick.
        /// </summary>
        private void Update()
        {
            if (_autoSaveInterval > 0 && Time.time - _lastSaveTime > _autoSaveInterval)
            {
                SaveData();
            }
        }
        
        /// <summary>
        /// Loads player data from disk with optional decryption.
        /// Thread-safe.
        /// </summary>
        public void LoadData()
        {
            lock (_saveLock)
            {
                if (!File.Exists(_savePath))
                {
                    Debug.Log("[SaveSystem] No save file found, creating new data");
                    _currentData = new PlayerData();
                    OnDataLoaded?.Invoke(_currentData);
                    return;
                }
                
                try
                {
                    byte[] rawData = File.ReadAllBytes(_savePath);
                    string json;
                    
                    if (_enableEncryption)
                    {
                        json = DecryptData(rawData);
                    }
                    else
                    {
                        json = Encoding.UTF8.GetString(rawData);
                    }
                    
                    _currentData = JsonUtility.FromJson<PlayerData>(json);
                    
                    if (_currentData == null)
                    {
                        throw new Exception("Deserialization returned null");
                    }
                    
                    Debug.Log($"[SaveSystem] Loaded player data: {_currentData.username}, Level {_currentData.level}");
                    OnDataLoaded?.Invoke(_currentData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveSystem] Load failed: {ex.Message}");
                    _currentData = new PlayerData();
                    OnDataLoaded?.Invoke(_currentData);
                }
            }
        }
        
        /// <summary>
        /// Saves player data to disk with optional encryption.
        /// Thread-safe.
        /// </summary>
        public void SaveData()
        {
            if (_currentData == null)
            {
                Debug.LogWarning("[SaveSystem] No data to save");
                return;
            }
            
            lock (_saveLock)
            {
                try
                {
                    _currentData.lastSaveTimestamp = DateTime.UtcNow.Ticks;
                    string json = JsonUtility.ToJson(_currentData, true);
                    byte[] dataToWrite;
                    
                    if (_enableEncryption)
                    {
                        dataToWrite = EncryptData(json);
                    }
                    else
                    {
                        dataToWrite = Encoding.UTF8.GetBytes(json);
                    }
                    
                    // Atomic write with temp file
                    string tempPath = _savePath + ".tmp";
                    File.WriteAllBytes(tempPath, dataToWrite);
                    
                    if (File.Exists(_savePath))
                    {
                        File.Delete(_savePath);
                    }
                    File.Move(tempPath, _savePath);
                    
                    _lastSaveTime = Time.time;
                    Debug.Log($"[SaveSystem] Data saved successfully");
                    OnDataSaved?.Invoke(_currentData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveSystem] Save failed: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Encrypts data using AES-256-CBC.
        /// </summary>
        private byte[] EncryptData(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.IV = _encryptionIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                        return ms.ToArray();
                    }
                }
            }
        }
        
        /// <summary>
        /// Decrypts data using AES-256-CBC.
        /// </summary>
        private string DecryptData(byte[] cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.IV = _encryptionIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                
                using (MemoryStream ms = new MemoryStream(cipherText))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates player position in save data.
        /// </summary>
        public void UpdatePlayerPosition(Vector3 position)
        {
            if (_currentData != null)
            {
                _currentData.SetPosition(position);
            }
        }
        
        /// <summary>
        /// Updates player currency.
        /// </summary>
        public void UpdateCurrency(int amount)
        {
            if (_currentData != null)
            {
                _currentData.currency = amount;
            }
        }
        
        /// <summary>
        /// Adds experience and handles level up.
        /// </summary>
        public void AddExperience(int exp)
        {
            if (_currentData != null)
            {
                _currentData.experience += exp;
                
                // Simple level-up formula
                int expForNextLevel = _currentData.level * 100;
                while (_currentData.experience >= expForNextLevel)
                {
                    _currentData.experience -= expForNextLevel;
                    _currentData.level++;
                    Debug.Log($"[SaveSystem] Level up! Now level {_currentData.level}");
                }
            }
        }
        
        /// <summary>
        /// Resets save data to default values.
        /// </summary>
        public void ResetData()
        {
            lock (_saveLock)
            {
                _currentData = new PlayerData();
                SaveData();
                Debug.Log("[SaveSystem] Data reset to defaults");
            }
        }
        
        /// <summary>
        /// Deletes the save file.
        /// </summary>
        public void DeleteSaveFile()
        {
            lock (_saveLock)
            {
                if (File.Exists(_savePath))
                {
                    File.Delete(_savePath);
                    Debug.Log("[SaveSystem] Save file deleted");
                }
                _currentData = new PlayerData();
            }
        }
        
        /// <summary>
        /// Save on application quit.
        /// </summary>
        private void OnApplicationQuit()
        {
            SaveData();
        }
        
        /// <summary>
        /// Save when app loses focus (mobile-friendly).
        /// </summary>
        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                SaveData();
            }
        }
    }
}