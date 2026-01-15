using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Achievements
{
    #region Enums
    
    /// <summary>
    /// Types of achievements available in the game
    /// </summary>
    public enum AchievementType
    {
        Standard,       // One-time unlock
        Progressive,    // Multiple tiers (10/50/100/500)
        Secret,         // Hidden until unlocked
        Platinum,       // Requires all other achievements
        TimeBased,      // Speedrun or time-limited
        Negative,       // Fail/die/lose conditions
        Daily,          // Resets daily
        Seasonal        // Limited time event
    }
    
    /// <summary>
    /// Rarity tiers affecting rewards and prestige
    /// </summary>
    public enum AchievementRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,
        Mythic = 4
    }
    
    /// <summary>
    /// Condition types for unlocking achievements
    /// </summary>
    public enum UnlockCondition
    {
        KillEnemies,
        CollectItems,
        CompleteQuests,
        DealDamage,
        TravelDistance,
        CraftItems,
        UseAbility,
        ReachLevel,
        SpendCurrency,
        Die,
        Fail,
        TimeElapsed,
        Custom
    }
    
    /// <summary>
    /// Types of rewards granted by achievements
    /// </summary>
    public enum RewardType
    {
        Currency,
        Item,
        Cosmetic,
        Title,
        UnlockFeature,
        ExperiencePoints,
        StatBoost
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Defines an achievement with requirements and rewards
    /// </summary>
    [Serializable]
    public class Achievement
    {
        public string id;
        public string name;
        public string description;
        public string secretDescription; // Shown before unlock for secret achievements
        public AchievementType type;
        public AchievementRarity rarity;
        public Sprite icon;
        
        public UnlockCondition condition;
        public int requiredProgress;
        public string customConditionKey; // For custom conditions
        
        public List<Achievement> dependencies; // Must unlock these first
        public List<Reward> rewards;
        
        public bool isSecret;
        public int pointValue;
        public string platformAchievementId; // For Steam/PSN/Xbox integration
        
        // Progressive achievement tiers
        public List<int> progressiveTiers;
        public int currentTier;
        
        // Time-based
        public float timeLimit; // For speedrun achievements (in seconds)
    }
    
    /// <summary>
    /// Tracks player progress toward an achievement
    /// </summary>
    [Serializable]
    public class AchievementProgress
    {
        public string achievementId;
        public int currentProgress;
        public bool isUnlocked;
        public DateTime unlockTime;
        public int currentTier; // For progressive achievements
        public bool notificationShown;
    }
    
    /// <summary>
    /// Groups achievements into categories for UI
    /// </summary>
    [Serializable]
    public class AchievementCategory
    {
        public string categoryName;
        public List<Achievement> achievements;
        public Sprite categoryIcon;
    }
    
    /// <summary>
    /// Represents a reward granted by an achievement
    /// </summary>
    [Serializable]
    public class Reward
    {
        public RewardType type;
        public string itemId;
        public int amount;
        public string description;
    }
    
    #endregion
    
    /// <summary>
    /// Manages achievement tracking, unlocking, and rewards
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        #region Singleton
        
        private static AchievementSystem instance;
        public static AchievementSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AchievementSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("AchievementSystem");
                        instance = go.AddComponent<AchievementSystem>();
                    }
                }
                return instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Achievement Database")]
        [SerializeField] private List<Achievement> allAchievements = new List<Achievement>();
        [SerializeField] private List<AchievementCategory> categories = new List<AchievementCategory>();
        
        private Dictionary<string, Achievement> achievementDatabase;
        private Dictionary<string, AchievementProgress> playerProgress;
        private Dictionary<string, int> statisticsTracker; // For tracking various stats
        
        [Header("Settings")]
        [SerializeField] private bool enableNotifications = true;
        [SerializeField] private float notificationDuration = 5f;
        [SerializeField] private bool enablePlatformSync = true;
        
        #endregion
        
        #region Events
        
        public event Action<Achievement> OnAchievementUnlocked;
        public event Action<Achievement, int> OnProgressUpdated;
        public event Action<Reward> OnRewardGranted;
        public event Action<Achievement, int> OnTierCompleted; // For progressive achievements
        
        #endregion
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