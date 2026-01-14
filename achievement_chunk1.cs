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