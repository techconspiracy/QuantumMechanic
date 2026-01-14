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
    }
}