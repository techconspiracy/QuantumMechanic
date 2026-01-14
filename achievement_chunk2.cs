#region Initialization
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeSystem();
        }
        
        /// <summary>
        /// Initializes achievement database and player progress
        /// </summary>
        private void InitializeSystem()
        {
            achievementDatabase = new Dictionary<string, Achievement>();
            playerProgress = new Dictionary<string, AchievementProgress>();
            statisticsTracker = new Dictionary<string, int>();
            
            // Build achievement database
            foreach (var achievement in allAchievements)
            {
                achievementDatabase[achievement.id] = achievement;
            }
            
            LoadProgress();
            CheckRetroactiveProgress();
        }
        
        #endregion
        
        #region Progress Tracking
        
        /// <summary>
        /// Updates progress for a specific achievement condition
        /// </summary>
        public void TrackProgress(UnlockCondition condition, int amount = 1, string customKey = "")
        {
            // Update statistics
            string statKey = customKey != "" ? customKey : condition.ToString();
            if (!statisticsTracker.ContainsKey(statKey))
                statisticsTracker[statKey] = 0;
            statisticsTracker[statKey] += amount;
            
            // Check all achievements that match this condition
            foreach (var achievement in allAchievements)
            {
                if (achievement.condition == condition && 
                    (customKey == "" || achievement.customConditionKey == customKey))
                {
                    CheckProgress(achievement.id, amount);
                }
            }
        }
        
        /// <summary>
        /// Checks and updates progress for a specific achievement
        /// </summary>
        public void CheckProgress(string achievementId, int progressAmount = 1)
        {
            if (!achievementDatabase.ContainsKey(achievementId))
            {
                Debug.LogWarning($"Achievement {achievementId} not found");
                return;
            }
            
            Achievement achievement = achievementDatabase[achievementId];
            
            // Check dependencies first
            if (!AreDependenciesMet(achievement))
                return;
            
            // Get or create progress entry
            if (!playerProgress.ContainsKey(achievementId))
            {
                playerProgress[achievementId] = new AchievementProgress
                {
                    achievementId = achievementId,
                    currentProgress = 0,
                    isUnlocked = false,
                    currentTier = 0
                };
            }
            
            AchievementProgress progress = playerProgress[achievementId];
            
            // Skip if already unlocked (unless progressive)
            if (progress.isUnlocked && achievement.type != AchievementType.Progressive)
                return;
            
            // Update progress based on achievement type
            switch (achievement.type)
            {
                case AchievementType.Standard:
                case AchievementType.Secret:
                case AchievementType.Negative:
                    UpdateStandardProgress(achievement, progress, progressAmount);
                    break;
                    
                case AchievementType.Progressive:
                    UpdateProgressiveProgress(achievement, progress, progressAmount);
                    break;
                    
                case AchievementType.TimeBased:
                    UpdateTimeBasedProgress(achievement, progress);
                    break;
                    
                case AchievementType.Daily:
                    UpdateDailyProgress(achievement, progress, progressAmount);
                    break;
            }
            
            OnProgressUpdated?.Invoke(achievement, progress.currentProgress);
            SaveProgress();
        }
        
        /// <summary>
        /// Updates progress for standard one-time achievements
        /// </summary>
        private void UpdateStandardProgress(Achievement achievement, AchievementProgress progress, int amount)
        {
            progress.currentProgress += amount;
            
            if (progress.currentProgress >= achievement.requiredProgress)
            {
                UnlockAchievement(achievement.id);
            }
        }
        
        /// <summary>
        /// Updates progress for multi-tier progressive achievements
        /// </summary>
        private void UpdateProgressiveProgress(Achievement achievement, AchievementProgress progress, int amount)
        {
            progress.currentProgress += amount;
            
            // Check if we've reached a new tier
            if (achievement.progressiveTiers != null && progress.currentTier < achievement.progressiveTiers.Count)
            {
                int nextTierRequirement = achievement.progressiveTiers[progress.currentTier];
                
                if (progress.currentProgress >= nextTierRequirement)
                {
                    progress.currentTier++;
                    OnTierCompleted?.Invoke(achievement, progress.currentTier);
                    
                    // Grant tier rewards
                    if (achievement.rewards != null && progress.currentTier <= achievement.rewards.Count)
                    {
                        GrantReward(achievement.rewards[progress.currentTier - 1]);
                    }
                    
                    if (enableNotifications)
                    {
                        ShowNotification(achievement, $"Tier {progress.currentTier} Complete!");
                    }
                    
                    // Check if all tiers completed
                    if (progress.currentTier >= achievement.progressiveTiers.Count)
                    {
                        UnlockAchievement(achievement.id);
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates time-based achievements (speedruns)
        /// </summary>
        private void UpdateTimeBasedProgress(Achievement achievement, AchievementProgress progress)
        {
            // Time tracking handled externally, this just validates completion
            progress.currentProgress = 1;
            UnlockAchievement(achievement.id);
        }
        
        /// <summary>
        /// Updates daily repeating achievements
        /// </summary>
        private void UpdateDailyProgress(Achievement achievement, AchievementProgress progress, int amount)
        {
            // Check if we need to reset (new day)
            if (progress.unlockTime.Date < DateTime.Now.Date)
            {
                progress.currentProgress = 0;
                progress.isUnlocked = false;
            }
            
            UpdateStandardProgress(achievement, progress, amount);
        }
        
        #endregion
        
        #region Achievement Unlocking
        
        /// <summary>
        /// Unlocks an achievement and grants rewards
        /// </summary>
        public void UnlockAchievement(string achievementId)
        {
            if (!achievementDatabase.ContainsKey(achievementId))
                return;
            
            Achievement achievement = achievementDatabase[achievementId];
            AchievementProgress progress = playerProgress[achievementId];
            
            if (progress.isUnlocked)
                return;
            
            progress.isUnlocked = true;
            progress.unlockTime = DateTime.Now;
            
            // Grant rewards
            if (achievement.rewards != null)
            {
                foreach (var reward in achievement.rewards)
                {
                    GrantReward(reward);
                }
            }
            
            // Platform integration
            if (enablePlatformSync && !string.IsNullOrEmpty(achievement.platformAchievementId))
            {
                SyncPlatformAchievement(achievement.platformAchievementId);
            }
            
            // Show notification
            if (enableNotifications && !progress.notificationShown)
            {
                ShowNotification(achievement, "Achievement Unlocked!");
                progress.notificationShown = true;
            }
            
            OnAchievementUnlocked?.Invoke(achievement);
            SaveProgress();
            
            Debug.Log($"Achievement Unlocked: {achievement.name}");
        }
        
        #endregion