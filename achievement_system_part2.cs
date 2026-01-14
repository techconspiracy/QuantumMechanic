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
