using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantumMechanic.Achievements
{
    /// <summary>
    /// Achievement rarity levels affecting rewards and display
    /// </summary>
    public enum AchievementRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Achievement category for organization
    /// </summary>
    public enum AchievementCategory
    {
        Combat,
        Exploration,
        Story,
        Collection,
        Secrets,
        Speedrun,
        Mastery
    }

    /// <summary>
    /// Achievement tier for progressive challenges
    /// </summary>
    public enum AchievementTier
    {
        Bronze,
        Silver,
        Gold,
        Platinum,
        Diamond
    }

    /// <summary>
    /// Represents a single achievement definition
    /// </summary>
    [Serializable]
    public class Achievement
    {
        public string id;
        public string name;
        public string description;
        public string iconPath;
        public AchievementRarity rarity;
        public AchievementCategory category;
        public AchievementTier tier;
        public int points;
        public bool isSecret;
        public string secretDescription;
        public List<UnlockCondition> unlockConditions;
        public AchievementReward reward;

        public Achievement(string id, string name, string description, AchievementCategory category, 
                          AchievementRarity rarity = AchievementRarity.Common, AchievementTier tier = AchievementTier.Bronze)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.category = category;
            this.rarity = rarity;
            this.tier = tier;
            this.points = CalculatePoints(rarity, tier);
            this.unlockConditions = new List<UnlockCondition>();
        }

        private int CalculatePoints(AchievementRarity rarity, AchievementTier tier)
        {
            int basePoints = rarity switch
            {
                AchievementRarity.Common => 10,
                AchievementRarity.Uncommon => 25,
                AchievementRarity.Rare => 50,
                AchievementRarity.Epic => 100,
                AchievementRarity.Legendary => 250,
                _ => 10
            };

            float tierMultiplier = tier switch
            {
                AchievementTier.Bronze => 1f,
                AchievementTier.Silver => 1.5f,
                AchievementTier.Gold => 2f,
                AchievementTier.Platinum => 3f,
                AchievementTier.Diamond => 5f,
                _ => 1f
            };

            return Mathf.RoundToInt(basePoints * tierMultiplier);
        }
    }

    /// <summary>
    /// Tracks progress toward unlocking an achievement
    /// </summary>
    [Serializable]
    public class AchievementProgress
    {
        public string achievementId;
        public bool isUnlocked;
        public DateTime unlockTime;
        public float progress; // 0.0 to 1.0 for progressive achievements
        public Dictionary<string, float> conditionProgress;

        public AchievementProgress(string achievementId)
        {
            this.achievementId = achievementId;
            this.isUnlocked = false;
            this.progress = 0f;
            this.conditionProgress = new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Defines conditions required to unlock an achievement
    /// </summary>
    [Serializable]
    public class UnlockCondition
    {
        public string conditionId;
        public string statKey;
        public float targetValue;
        public ComparisonType comparison;
        public bool requiresAllConditions;

        public enum ComparisonType
        {
            GreaterThan,
            LessThan,
            Equal,
            GreaterOrEqual,
            LessOrEqual
        }

        public bool IsMet(float currentValue)
        {
            return comparison switch
            {
                ComparisonType.GreaterThan => currentValue > targetValue,
                ComparisonType.LessThan => currentValue < targetValue,
                ComparisonType.Equal => Mathf.Approximately(currentValue, targetValue),
                ComparisonType.GreaterOrEqual => currentValue >= targetValue,
                ComparisonType.LessOrEqual => currentValue <= targetValue,
                _ => false
            };
        }
    }

    /// <summary>
    /// Rewards granted upon achievement unlock
    /// </summary>
    [Serializable]
    public class AchievementReward
    {
        public int currency;
        public List<string> itemIds;
        public List<string> cosmeticIds;
        public string titleUnlock;
    }
/// <summary>
    /// Comprehensive player statistics tracking
    /// </summary>
    [Serializable]
    public class PlayerStatistics
    {
        // Combat Statistics
        public int totalKills;
        public int totalDeaths;
        public float totalDamageDealt;
        public float totalDamageTaken;
        public int shotsHit;
        public int shotsFired;
        public int headshots;
        public int meleeKills;
        public int longestKillStreak;
        public int currentKillStreak;
        public Dictionary<string, int> killsByWeapon;
        public Dictionary<string, int> killsByEnemy;

        // Exploration Statistics
        public float distanceTraveled;
        public float distanceSprinted;
        public float distanceDashed;
        public int areasDiscovered;
        public int secretsFound;
        public int checkpointsReached;
        public int roomsCleared;
        public float timeInCombat;
        public float timeExploring;

        // Economy Statistics
        public int currencyEarned;
        public int currencySpent;
        public int itemsCrafted;
        public int itemsPurchased;
        public int upgradesApplied;
        public int totalLoot;

        // Time Statistics
        public float totalPlaytime;
        public float fastestLevelCompletion;
        public float currentRunTime;
        public DateTime firstPlayDate;
        public DateTime lastPlayDate;

        // Interaction Statistics
        public int dialoguesCompleted;
        public int puzzlesSolved;
        public int bossesDefeated;
        public int chestsOpened;
        public int platformsActivated;

        // Performance Statistics
        public int perfectCombos;
        public int perfectDodges;
        public float highestDamageInOneHit;
        public float healthRestoredTotal;

        public PlayerStatistics()
        {
            killsByWeapon = new Dictionary<string, int>();
            killsByEnemy = new Dictionary<string, int>();
            firstPlayDate = DateTime.Now;
            lastPlayDate = DateTime.Now;
        }

        /// <summary>
        /// Calculate combat accuracy percentage
        /// </summary>
        public float GetAccuracy()
        {
            return shotsFired > 0 ? (float)shotsHit / shotsFired * 100f : 0f;
        }

        /// <summary>
        /// Calculate kill/death ratio
        /// </summary>
        public float GetKDRatio()
        {
            return totalDeaths > 0 ? (float)totalKills / totalDeaths : totalKills;
        }

        /// <summary>
        /// Get formatted playtime string
        /// </summary>
        public string GetPlaytimeFormatted()
        {
            TimeSpan time = TimeSpan.FromSeconds(totalPlaytime);
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        /// <summary>
        /// Increment kill count for specific weapon
        /// </summary>
        public void AddWeaponKill(string weaponId)
        {
            if (!killsByWeapon.ContainsKey(weaponId))
                killsByWeapon[weaponId] = 0;
            killsByWeapon[weaponId]++;
        }

        /// <summary>
        /// Increment kill count for specific enemy
        /// </summary>
        public void AddEnemyKill(string enemyId)
        {
            if (!killsByEnemy.ContainsKey(enemyId))
                killsByEnemy[enemyId] = 0;
            killsByEnemy[enemyId]++;
        }

        /// <summary>
        /// Update kill streak tracking
        /// </summary>
        public void UpdateKillStreak(bool killed)
        {
            if (killed)
            {
                currentKillStreak++;
                if (currentKillStreak > longestKillStreak)
                    longestKillStreak = currentKillStreak;
            }
            else
            {
                currentKillStreak = 0;
            }
        }

        /// <summary>
        /// Get most used weapon
        /// </summary>
        public string GetFavoriteWeapon()
        {
            if (killsByWeapon.Count == 0) return "None";
            return killsByWeapon.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Get most killed enemy type
        /// </summary>
        public string GetMostKilledEnemy()
        {
            if (killsByEnemy.Count == 0) return "None";
            return killsByEnemy.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Update playtime tracking
        /// </summary>
        public void UpdatePlaytime(float deltaTime)
        {
            totalPlaytime += deltaTime;
            currentRunTime += deltaTime;
            lastPlayDate = DateTime.Now;
        }
    }
/// <summary>
    /// Core achievement system managing unlocks, tracking, and rewards
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private bool enableNotifications = true;
        [SerializeField] private float notificationDuration = 5f;
        [SerializeField] private AudioClip achievementUnlockSound;

        // Data
        private Dictionary<string, Achievement> achievements;
        private Dictionary<string, AchievementProgress> progressData;
        private PlayerStatistics statistics;

        // Events
        public event Action<Achievement> OnAchievementUnlocked;
        public event Action<string, float> OnProgressUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            achievements = new Dictionary<string, Achievement>();
            progressData = new Dictionary<string, AchievementProgress>();
            statistics = new PlayerStatistics();

            InitializeAchievements();
        }

        /// <summary>
        /// Initialize all achievement definitions
        /// </summary>
        private void InitializeAchievements()
        {
            // Combat Achievements
            RegisterAchievement(new Achievement("first_kill", "First Blood", "Defeat your first enemy", 
                AchievementCategory.Combat, AchievementRarity.Common));
            
            RegisterAchievement(new Achievement("kill_100", "Centurion", "Defeat 100 enemies", 
                AchievementCategory.Combat, AchievementRarity.Uncommon, AchievementTier.Bronze));
            
            RegisterAchievement(new Achievement("kill_1000", "Legendary Warrior", "Defeat 1000 enemies", 
                AchievementCategory.Combat, AchievementRarity.Epic, AchievementTier.Gold));

            RegisterAchievement(new Achievement("flawless_boss", "Untouchable", "Defeat a boss without taking damage", 
                AchievementCategory.Combat, AchievementRarity.Rare, AchievementTier.Silver));

            RegisterAchievement(new Achievement("streak_50", "Unstoppable", "Achieve a 50 kill streak", 
                AchievementCategory.Combat, AchievementRarity.Epic, AchievementTier.Gold));

            // Exploration Achievements
            RegisterAchievement(new Achievement("explorer_1", "Curious Explorer", "Discover 10 areas", 
                AchievementCategory.Exploration, AchievementRarity.Common));

            RegisterAchievement(new Achievement("all_secrets", "Treasure Hunter", "Find all secret areas", 
                AchievementCategory.Secrets, AchievementRarity.Legendary, AchievementTier.Platinum));

            // Speedrun Achievements
            RegisterAchievement(new Achievement("speed_10min", "Speed Demon", "Complete a level in under 10 minutes", 
                AchievementCategory.Speedrun, AchievementRarity.Rare, AchievementTier.Gold));
        }

        /// <summary>
        /// Register a new achievement in the system
        /// </summary>
        public void RegisterAchievement(Achievement achievement)
        {
            if (!achievements.ContainsKey(achievement.id))
            {
                achievements[achievement.id] = achievement;
                progressData[achievement.id] = new AchievementProgress(achievement.id);
            }
        }

        /// <summary>
        /// Update a statistic and check for achievement unlocks
        /// </summary>
        public void UpdateStat(string statKey, float value, bool isIncrement = true)
        {
            // Update the actual statistic
            UpdateStatisticValue(statKey, value, isIncrement);

            // Check all achievements for unlock conditions
            CheckAchievementConditions(statKey);
        }

        /// <summary>
        /// Update internal statistics tracking
        /// </summary>
        private void UpdateStatisticValue(string statKey, float value, bool isIncrement)
        {
            switch (statKey)
            {
                case "kills":
                    if (isIncrement) statistics.totalKills += (int)value;
                    else statistics.totalKills = (int)value;
                    break;
                case "deaths":
                    if (isIncrement) statistics.totalDeaths += (int)value;
                    else statistics.totalDeaths = (int)value;
                    break;
                case "damage_dealt":
                    if (isIncrement) statistics.totalDamageDealt += value;
                    else statistics.totalDamageDealt = value;
                    break;
                case "distance_traveled":
                    if (isIncrement) statistics.distanceTraveled += value;
                    else statistics.distanceTraveled = value;
                    break;
                case "secrets_found":
                    if (isIncrement) statistics.secretsFound += (int)value;
                    else statistics.secretsFound = (int)value;
                    break;
                case "kill_streak":
                    statistics.currentKillStreak = (int)value;
                    if (statistics.currentKillStreak > statistics.longestKillStreak)
                        statistics.longestKillStreak = statistics.currentKillStreak;
                    break;
            }
        }

        /// <summary>
        /// Check if any achievements should be unlocked based on stat changes
        /// </summary>
        private void CheckAchievementConditions(string statKey)
        {
            foreach (var achievement in achievements.Values)
            {
                if (progressData[achievement.id].isUnlocked) continue;

                bool shouldUnlock = true;
                float totalProgress = 0f;
                int conditionsChecked = 0;

                foreach (var condition in achievement.unlockConditions)
                {
                    if (condition.statKey != statKey && statKey != "all") continue;

                    float currentValue = GetStatValue(condition.statKey);
                    bool conditionMet = condition.IsMet(currentValue);

                    if (condition.requiresAllConditions && !conditionMet)
                    {
                        shouldUnlock = false;
                    }

                    float conditionProgress = Mathf.Clamp01(currentValue / condition.targetValue);
                    totalProgress += conditionProgress;
                    conditionsChecked++;
                }

                if (conditionsChecked > 0)
                {
                    float averageProgress = totalProgress / conditionsChecked;
                    progressData[achievement.id].progress = averageProgress;
                    OnProgressUpdated?.Invoke(achievement.id, averageProgress);

                    if (shouldUnlock && averageProgress >= 1f)
                    {
                        UnlockAchievement(achievement.id);
                    }
                }
            }
        }

        /// <summary>
        /// Get current value of a tracked statistic
        /// </summary>
        private float GetStatValue(string statKey)
        {
            return statKey switch
            {
                "kills" => statistics.totalKills,
                "deaths" => statistics.totalDeaths,
                "damage_dealt" => statistics.totalDamageDealt,
                "distance_traveled" => statistics.distanceTraveled,
                "secrets_found" => statistics.secretsFound,
                "kill_streak" => statistics.longestKillStreak,
                _ => 0f
            };
        }

        /// <summary>
        /// Unlock an achievement and grant rewards
        /// </summary>
        public void UnlockAchievement(string achievementId)
        {
            if (!achievements.ContainsKey(achievementId)) return;
            if (progressData[achievementId].isUnlocked) return;

            Achievement achievement = achievements[achievementId];
            progressData[achievementId].isUnlocked = true;
            progressData[achievementId].unlockTime = DateTime.Now;
            progressData[achievementId].progress = 1f;

            // Grant rewards
            if (achievement.reward != null)
            {
                GrantRewards(achievement.reward);
            }

            // Show notification
            if (enableNotifications)
            {
                ShowAchievementNotification(achievement);
            }

            // Play sound
            if (achievementUnlockSound != null)
            {
                AudioSource.PlayClipAtPoint(achievementUnlockSound, Camera.main.transform.position);
            }

            OnAchievementUnlocked?.Invoke(achievement);
            Debug.Log($"Achievement Unlocked: {achievement.name} (+{achievement.points} points)");
        }

        /// <summary>
        /// Grant achievement rewards to player
        /// </summary>
        private void GrantRewards(AchievementReward reward)
        {
            if (reward.currency > 0)
            {
                // Integrate with economy system
                Debug.Log($"Granted {reward.currency} currency");
            }

            foreach (string itemId in reward.itemIds)
            {
                Debug.Log($"Granted item: {itemId}");
            }

            foreach (string cosmeticId in reward.cosmeticIds)
            {
                Debug.Log($"Unlocked cosmetic: {cosmeticId}");
            }
        }

        /// <summary>
        /// Display achievement unlock notification
        /// </summary>
        private void ShowAchievementNotification(Achievement achievement)
        {
            // Integration with UI system
            Debug.Log($"[ACHIEVEMENT] {achievement.name} - {achievement.description}");
        }

        /// <summary>
        /// Get total achievement points earned
        /// </summary>
        public int GetTotalPoints()
        {
            return progressData.Values
                .Where(p => p.isUnlocked)
                .Sum(p => achievements[p.achievementId].points);
        }

        /// <summary>
        /// Get completion percentage
        /// </summary>
        public float GetCompletionPercentage()
        {
            int total = achievements.Count;
            int unlocked = progressData.Values.Count(p => p.isUnlocked);
            return total > 0 ? (float)unlocked / total * 100f : 0f;
        }

        public PlayerStatistics GetStatistics() => statistics;
        public Dictionary<string, Achievement> GetAllAchievements() => achievements;
        public Dictionary<string, AchievementProgress> GetProgress() => progressData;
    }
}