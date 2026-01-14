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
    }