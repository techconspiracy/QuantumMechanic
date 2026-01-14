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
