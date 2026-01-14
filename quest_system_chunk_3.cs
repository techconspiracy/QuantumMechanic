#region Validation
        
        /// <summary>
        /// Checks if all required objectives are complete.
        /// </summary>
        private bool AreAllRequiredObjectivesComplete(Quest quest)
        {
            foreach (var objective in quest.objectives)
            {
                if (!objective.isOptional && !objective.isCompleted)
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// Checks if prerequisites for a quest are met.
        /// </summary>
        private bool ArePrerequisitesMet(Quest quest)
        {
            foreach (string prereqId in quest.prerequisites)
            {
                if (!completedQuests.Contains(prereqId))
                {
                    return false;
                }
            }
            return true;
        }
        
        #endregion
        
        #region Reward Distribution
        
        /// <summary>
        /// Grants all rewards from a completed quest.
        /// </summary>
        private void GrantRewards(Quest quest)
        {
            foreach (var reward in quest.rewards)
            {
                switch (reward.type)
                {
                    case RewardType.Experience:
                        GrantExperience(reward.amount);
                        break;
                    case RewardType.Currency:
                        GrantCurrency(reward.amount);
                        break;
                    case RewardType.Item:
                        GrantItem(reward.rewardId, reward.amount);
                        break;
                    case RewardType.Unlock:
                        GrantUnlock(reward.rewardId);
                        break;
                }
                
                Debug.Log($"Granted reward: {reward.type} - {reward.description}");
            }
        }
        
        /// <summary>
        /// Grants experience points (integrate with your progression system).
        /// </summary>
        private void GrantExperience(int amount)
        {
            // TODO: Integrate with your progression/leveling system
            Debug.Log($"Granted {amount} experience");
        }
        
        /// <summary>
        /// Grants currency (integrate with your economy system).
        /// </summary>
        private void GrantCurrency(int amount)
        {
            // TODO: Integrate with your economy/inventory system
            Debug.Log($"Granted {amount} currency");
        }
        
        /// <summary>
        /// Grants an item (integrate with your inventory system).
        /// </summary>
        private void GrantItem(string itemId, int amount)
        {
            // TODO: Integrate with your inventory system
            Debug.Log($"Granted item: {itemId} x{amount}");
        }
        
        /// <summary>
        /// Grants an unlock (feature, area, ability, etc.).
        /// </summary>
        private void GrantUnlock(string unlockId)
        {
            // TODO: Integrate with your unlock/progression system
            Debug.Log($"Granted unlock: {unlockId}");
        }
        
        #endregion
        
        #region Quest Chain Management
        
        /// <summary>
        /// Unlocks a quest, making it available if prerequisites are met.
        /// </summary>
        public void UnlockQuest(string questId)
        {
            if (!questDatabase.TryGetValue(questId, out Quest quest))
            {
                return;
            }
            
            if (quest.state != QuestState.Locked)
            {
                return;
            }
            
            if (ArePrerequisitesMet(quest))
            {
                quest.state = QuestState.Available;
                Debug.Log($"Unlocked quest: {quest.questName}");
            }
        }
        
        /// <summary>
        /// Updates availability of all quests based on prerequisites.
        /// </summary>
        private void UpdateQuestAvailability()
        {
            foreach (var quest in questDatabase.Values)
            {
                if (quest.state == QuestState.Locked && ArePrerequisitesMet(quest))
                {
                    quest.state = QuestState.Available;
                }
            }
        }
        
        #endregion
        
        #region Query Methods
        
        /// <summary>
        /// Gets a quest by ID.
        /// </summary>
        public Quest GetQuest(string questId)
        {
            questDatabase.TryGetValue(questId, out Quest quest);
            return quest;
        }
        
        /// <summary>
        /// Gets all active quests.
        /// </summary>
        public List<Quest> GetActiveQuests()
        {
            return activeQuests.Values.Select(aq => aq.quest).ToList();
        }
        
        /// <summary>
        /// Gets all available quests.
        /// </summary>
        public List<Quest> GetAvailableQuests()
        {
            return questDatabase.Values.Where(q => q.state == QuestState.Available).ToList();
        }
        
        /// <summary>
        /// Gets all completed quests.
        /// </summary>
        public List<Quest> GetCompletedQuests()
        {
            return questDatabase.Values.Where(q => completedQuests.Contains(q.questId)).ToList();
        }
        
        /// <summary>
        /// Checks if a quest is active.
        /// </summary>
        public bool IsQuestActive(string questId)
        {
            return activeQuests.ContainsKey(questId);
        }
        
        /// <summary>
        /// Checks if a quest is completed.
        /// </summary>
        public bool IsQuestCompleted(string questId)
        {
            return completedQuests.Contains(questId);
        }
        
        #endregion
        
        #region Timer Management
        
        /// <summary>
        /// Updates timed quests.
        /// </summary>
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            
            List<string> questsToFail = new List<string>();
            
            foreach (var kvp in activeQuests)
            {
                Quest quest = kvp.Value.quest;
                
                if (quest.timeLimit > 0)
                {
                    quest.timeRemaining -= deltaTime;
                    
                    if (quest.timeRemaining <= 0)
                    {
                        questsToFail.Add(quest.questId);
                    }
                }
            }
            
            // Fail quests that ran out of time
            foreach (string questId in questsToFail)
            {
                FailQuest(questId);
            }
        }
        
        #endregion
        
        #region Save/Load System
        
        /// <summary>
        /// Serializable save data for quest system.
        /// </summary>
        [Serializable]
        public class QuestSaveData
        {
            public List<string> activeQuestIds = new List<string>();
            public List<string> completedQuestIds = new List<string>();
            public Dictionary<string, QuestState> questStates = new Dictionary<string, QuestState>();
            public Dictionary<string, List<ObjectiveProgress>> objectiveProgress = new Dictionary<string, List<ObjectiveProgress>>();
        }
        
        [Serializable]
        public class ObjectiveProgress
        {
            public string objectiveId;
            public int currentAmount;
            public bool isCompleted;
        }
        
        /// <summary>
        /// Saves the current quest state.
        /// </summary>
        public QuestSaveData SaveQuestData()
        {
            QuestSaveData saveData = new QuestSaveData();
            
            saveData.activeQuestIds = new List<string>(activeQuests.Keys);
            saveData.completedQuestIds = new List<string>(completedQuests);
            
            foreach (var quest in questDatabase.Values)
            {
                saveData.questStates[quest.questId] = quest.state;
                
                if (quest.state == QuestState.Active)
                {
                    List<ObjectiveProgress> progressList = new List<ObjectiveProgress>();
                    foreach (var objective in quest.objectives)
                    {
                        progressList.Add(new ObjectiveProgress
                        {
                            objectiveId = objective.objectiveId,
                            currentAmount = objective.currentAmount,
                            isCompleted = objective.isCompleted
                        });
                    }
                    saveData.objectiveProgress[quest.questId] = progressList;
                }
            }
            
            return saveData;
        }
        
        /// <summary>
        /// Loads quest state from save data.
        /// </summary>
        public void LoadQuestData(QuestSaveData saveData)
        {
            if (saveData == null) return;
            
            // Clear current state
            activeQuests.Clear();
            completedQuests.Clear();
            
            // Restore quest states
            foreach (var kvp in saveData.questStates)
            {
                if (questDatabase.TryGetValue(kvp.Key, out Quest quest))
                {
                    quest.state = kvp.Value;
                }
            }
            
            // Restore completed quests
            completedQuests = new HashSet<string>(saveData.completedQuestIds);
            
            // Restore active quests and objectives
            foreach (string questId in saveData.activeQuestIds)
            {
                if (questDatabase.TryGetValue(questId, out Quest quest))
                {
                    activeQuests[questId] = new ActiveQuest(quest);
                    
                    if (saveData.objectiveProgress.TryGetValue(questId, out var progressList))
                    {
                        foreach (var progress in progressList)
                        {
                            var objective = quest.objectives.Find(o => o.objectiveId == progress.objectiveId);
                            if (objective != null)
                            {
                                objective.currentAmount = progress.currentAmount;
                                objective.isCompleted = progress.isCompleted;
                            }
                        }
                    }
                }
            }
            
            Debug.Log($"Loaded quest data: {activeQuests.Count} active, {completedQuests.Count} completed");
        }
        
        #endregion
    }
}