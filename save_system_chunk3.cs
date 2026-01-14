using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace QuantumMechanic.Persistence
{
    /// <summary>
    /// Advanced save features including auto-save, cloud sync, and platform integration
    /// </summary>
    public partial class SaveManager
    {
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

        private void Update()
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