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
    }

    /// <summary>
    /// Interface for components that can be saved and loaded.
    /// </summary>
    public interface ISaveable
    {
        string SaveState();
        void LoadState(string state);
    }
}