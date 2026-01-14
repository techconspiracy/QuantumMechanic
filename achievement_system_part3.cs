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