#region Reward System
        
        /// <summary>
        /// Grants a reward to the player
        /// </summary>
        private void GrantReward(Reward reward)
        {
            switch (reward.type)
            {
                case RewardType.Currency:
                    // Integration point: Add to player currency
                    Debug.Log($"Granted {reward.amount} currency");
                    break;
                    
                case RewardType.Item:
                    // Integration point: Add item to inventory
                    Debug.Log($"Granted item: {reward.itemId} x{reward.amount}");
                    break;
                    
                case RewardType.Cosmetic:
                    // Integration point: Unlock cosmetic item
                    Debug.Log($"Unlocked cosmetic: {reward.itemId}");
                    break;
                    
                case RewardType.Title:
                    // Integration point: Unlock player title
                    Debug.Log($"Unlocked title: {reward.description}");
                    break;
                    
                case RewardType.UnlockFeature:
                    // Integration point: Unlock game feature
                    Debug.Log($"Unlocked feature: {reward.itemId}");
                    break;
                    
                case RewardType.ExperiencePoints:
                    // Integration point: Grant XP
                    Debug.Log($"Granted {reward.amount} XP");
                    break;
                    
                case RewardType.StatBoost:
                    // Integration point: Apply permanent stat boost
                    Debug.Log($"Applied stat boost: {reward.description}");
                    break;
            }
            
            OnRewardGranted?.Invoke(reward);
        }
        
        #endregion
        
        #region Dependencies & Conditions
        
        /// <summary>
        /// Checks if all achievement dependencies are met
        /// </summary>
        private bool AreDependenciesMet(Achievement achievement)
        {
            if (achievement.dependencies == null || achievement.dependencies.Count == 0)
                return true;
            
            foreach (var dependency in achievement.dependencies)
            {
                if (!playerProgress.ContainsKey(dependency.id) || !playerProgress[dependency.id].isUnlocked)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Checks all achievements retroactively on load (for new achievements)
        /// </summary>
        private void CheckRetroactiveProgress()
        {
            foreach (var achievement in allAchievements)
            {
                if (!playerProgress.ContainsKey(achievement.id) || !playerProgress[achievement.id].isUnlocked)
                {
                    // Check if statistics already meet requirement
                    string statKey = achievement.customConditionKey != "" ? 
                        achievement.customConditionKey : achievement.condition.ToString();
                    
                    if (statisticsTracker.ContainsKey(statKey))
                    {
                        int currentStat = statisticsTracker[statKey];
                        if (currentStat > 0)
                        {
                            CheckProgress(achievement.id, currentStat);
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Platform Integration
        
        /// <summary>
        /// Syncs achievement with platform service (Steam, PSN, Xbox)
        /// </summary>
        private void SyncPlatformAchievement(string platformId)
        {
            // Integration point: Call platform-specific achievement unlock
            // Example: Steamworks.SteamUserStats.SetAchievement(platformId);
            Debug.Log($"Syncing platform achievement: {platformId}");
        }
        
        #endregion
        
        #region Statistics & Leaderboards
        
        /// <summary>
        /// Gets current value of a tracked statistic
        /// </summary>
        public int GetStatistic(string statKey)
        {
            return statisticsTracker.ContainsKey(statKey) ? statisticsTracker[statKey] : 0;
        }
        
        /// <summary>
        /// Gets total achievement points earned
        /// </summary>
        public int GetTotalPoints()
        {
            int total = 0;
            foreach (var progress in playerProgress.Values)
            {
                if (progress.isUnlocked && achievementDatabase.ContainsKey(progress.achievementId))
                {
                    total += achievementDatabase[progress.achievementId].pointValue;
                }
            }
            return total;
        }
        
        /// <summary>
        /// Gets completion percentage for all achievements
        /// </summary>
        public float GetCompletionPercentage()
        {
            if (allAchievements.Count == 0) return 0f;
            
            int unlockedCount = 0;
            foreach (var progress in playerProgress.Values)
            {
                if (progress.isUnlocked) unlockedCount++;
            }
            
            return (float)unlockedCount / allAchievements.Count * 100f;
        }
        
        #endregion
        
        #region UI Data Providers
        
        /// <summary>
        /// Gets all achievements for UI display
        /// </summary>
        public List<Achievement> GetAllAchievements(bool includeSecret = false)
        {
            if (includeSecret)
                return allAchievements;
            
            return allAchievements.FindAll(a => !a.isSecret || IsAchievementUnlocked(a.id));
        }
        
        /// <summary>
        /// Gets achievements by category
        /// </summary>
        public List<Achievement> GetAchievementsByCategory(string categoryName)
        {
            var category = categories.Find(c => c.categoryName == categoryName);
            return category?.achievements ?? new List<Achievement>();
        }
        
        /// <summary>
        /// Gets progress for a specific achievement
        /// </summary>
        public AchievementProgress GetProgress(string achievementId)
        {
            return playerProgress.ContainsKey(achievementId) ? playerProgress[achievementId] : null;
        }
        
        /// <summary>
        /// Checks if achievement is unlocked
        /// </summary>
        public bool IsAchievementUnlocked(string achievementId)
        {
            return playerProgress.ContainsKey(achievementId) && playerProgress[achievementId].isUnlocked;
        }
        
        /// <summary>
        /// Gets showcase achievements (highest rarity unlocked)
        /// </summary>
        public List<Achievement> GetShowcaseAchievements(int maxCount = 3)
        {
            var unlocked = new List<Achievement>();
            
            foreach (var progress in playerProgress.Values)
            {
                if (progress.isUnlocked && achievementDatabase.ContainsKey(progress.achievementId))
                {
                    unlocked.Add(achievementDatabase[progress.achievementId]);
                }
            }
            
            // Sort by rarity (highest first)
            unlocked.Sort((a, b) => b.rarity.CompareTo(a.rarity));
            
            return unlocked.GetRange(0, Mathf.Min(maxCount, unlocked.Count));
        }
        
        #endregion
        
        #region Notifications
        
        /// <summary>
        /// Shows achievement unlock notification
        /// </summary>
        private void ShowNotification(Achievement achievement, string message)
        {
            // Integration point: Show UI notification
            Debug.Log($"[ACHIEVEMENT] {message} - {achievement.name}");
            // Example: UIManager.Instance.ShowAchievementNotification(achievement, message, notificationDuration);
        }
        
        #endregion
        
        #region Save/Load
        
        /// <summary>
        /// Saves achievement progress to persistent storage
        /// </summary>
        private void SaveProgress()
        {
            var saveData = new AchievementSaveData
            {
                progress = new List<AchievementProgress>(playerProgress.Values),
                statistics = statisticsTracker
            };
            
            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString("AchievementProgress", json);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Loads achievement progress from persistent storage
        /// </summary>
        private void LoadProgress()
        {
            if (PlayerPrefs.HasKey("AchievementProgress"))
            {
                string json = PlayerPrefs.GetString("AchievementProgress");
                var saveData = JsonUtility.FromJson<AchievementSaveData>(json);
                
                foreach (var progress in saveData.progress)
                {
                    playerProgress[progress.achievementId] = progress;
                }
                
                statisticsTracker = saveData.statistics ?? new Dictionary<string, int>();
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Serializable container for achievement save data
    /// </summary>
    [Serializable]
    public class AchievementSaveData
    {
        public List<AchievementProgress> progress;
        public Dictionary<string, int> statistics;
    }
}