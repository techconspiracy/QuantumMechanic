using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuantumMechanic.Quests
{
    #region Enums and Constants
    
    /// <summary>
    /// Defines the current state of a quest.
    /// </summary>
    public enum QuestState
    {
        Locked,      // Prerequisites not met
        Available,   // Can be started
        Active,      // Currently in progress
        Completed,   // Successfully finished
        Failed,      // Failed (if applicable)
        Abandoned    // Player abandoned the quest
    }
    
    /// <summary>
    /// Types of quest objectives.
    /// </summary>
    public enum ObjectiveType
    {
        Collect,     // Collect X items
        Defeat,      // Defeat X enemies
        Interact,    // Interact with X objects
        Reach,       // Reach a location
        Dialogue,    // Talk to NPC
        Custom       // Custom scripted objective
    }
    
    /// <summary>
    /// Types of quest rewards.
    /// </summary>
    public enum RewardType
    {
        Experience,
        Currency,
        Item,
        Unlock
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Defines a single quest objective.
    /// </summary>
    [Serializable]
    public class QuestObjective
    {
        public string objectiveId;
        public string description;
        public ObjectiveType type;
        public string targetId;          // Item ID, Enemy ID, etc.
        public int requiredAmount;
        public int currentAmount;
        public bool isCompleted;
        public bool isOptional;
        
        /// <summary>
        /// Updates the objective progress.
        /// </summary>
        public void UpdateProgress(int amount)
        {
            currentAmount = Mathf.Min(currentAmount + amount, requiredAmount);
            isCompleted = currentAmount >= requiredAmount;
        }
    }
    
    /// <summary>
    /// Defines a quest reward.
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        public RewardType type;
        public string rewardId;          // Item ID, unlock key, etc.
        public int amount;
        public string description;
    }
    
    /// <summary>
    /// Complete quest definition.
    /// </summary>
    [Serializable]
    public class Quest
    {
        public string questId;
        public string questName;
        public string description;
        public string questGiver;        // NPC or source
        
        public List<string> prerequisites = new List<string>();
        public List<QuestObjective> objectives = new List<QuestObjective>();
        public List<QuestReward> rewards = new List<QuestReward>();
        
        public QuestState state = QuestState.Locked;
        public float timeLimit;          // 0 = no limit
        public float timeRemaining;
        public bool isSideQuest;
        public int recommendedLevel;
        
        public string nextQuestInChain;  // For quest chains
    }
    
    /// <summary>
    /// Runtime quest instance data.
    /// </summary>
    [Serializable]
    public class ActiveQuest
    {
        public Quest quest;
        public float startTime;
        public Dictionary<string, int> objectiveProgress = new Dictionary<string, int>();
        
        public ActiveQuest(Quest q)
        {
            quest = q;
            startTime = Time.time;
        }
    }
    
    #endregion
    
    /// <summary>
    /// Central quest management system.
    /// Handles quest tracking, objectives, and rewards.
    /// </summary>
    public class QuestSystem : MonoBehaviour
    {
        #region Singleton
        
        private static QuestSystem _instance;
        public static QuestSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<QuestSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("QuestSystem");
                        _instance = go.AddComponent<QuestSystem>();
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        #endregion
        
        #region Fields
        
        [Header("Quest Database")]
        [SerializeField] private List<Quest> allQuests = new List<Quest>();
        
        private Dictionary<string, Quest> questDatabase = new Dictionary<string, Quest>();
        private Dictionary<string, ActiveQuest> activeQuests = new Dictionary<string, ActiveQuest>();
        private HashSet<string> completedQuests = new HashSet<string>();
        
        [Header("Settings")]
        [SerializeField] private int maxActiveQuests = 10;
        [SerializeField] private bool autoTrackNewQuests = true;
        
        #endregion
        
        #region Events
        
        public event Action<Quest> OnQuestStarted;
        public event Action<Quest> OnQuestCompleted;
        public event Action<Quest> OnQuestFailed;
        public event Action<Quest> OnQuestAbandoned;
        public event Action<Quest, QuestObjective> OnObjectiveUpdated;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initializes the quest system and loads all quests.
        /// </summary>
        private void Start()
        {
            LoadQuestDatabase();
            UpdateQuestAvailability();
        }
        
        /// <summary>
        /// Loads all quests into the database.
        /// </summary>
        private void LoadQuestDatabase()
        {
            questDatabase.Clear();
            foreach (var quest in allQuests)
            {
                if (!questDatabase.ContainsKey(quest.questId))
                {
                    questDatabase.Add(quest.questId, quest);
                }
            }
            Debug.Log($"QuestSystem: Loaded {questDatabase.Count} quests");
        }
        
        #endregion
        
        #region Quest Management
        
        /// <summary>
        /// Starts a quest by ID.
        /// </summary>
        public bool StartQuest(string questId)
        {
            if (!questDatabase.TryGetValue(questId, out Quest quest))
            {
                Debug.LogWarning($"Quest {questId} not found in database");
                return false;
            }
            
            // Validate quest can be started
            if (quest.state != QuestState.Available)
            {
                Debug.LogWarning($"Quest {questId} is not available (current state: {quest.state})");
                return false;
            }
            
            if (activeQuests.Count >= maxActiveQuests)
            {
                Debug.LogWarning($"Maximum active quests reached ({maxActiveQuests})");
                return false;
            }
            
            if (activeQuests.ContainsKey(questId))
            {
                Debug.LogWarning($"Quest {questId} is already active");
                return false;
            }
            
            // Start the quest
            quest.state = QuestState.Active;
            ActiveQuest activeQuest = new ActiveQuest(quest);
            activeQuests.Add(questId, activeQuest);
            
            // Initialize objectives
            foreach (var objective in quest.objectives)
            {
                objective.currentAmount = 0;
                objective.isCompleted = false;
            }
            
            // Start timer if applicable
            if (quest.timeLimit > 0)
            {
                quest.timeRemaining = quest.timeLimit;
            }
            
            OnQuestStarted?.Invoke(quest);
            Debug.Log($"Started quest: {quest.questName}");
            return true;
        }
        
        /// <summary>
        /// Completes a quest and grants rewards.
        /// </summary>
        public bool CompleteQuest(string questId)
        {
            if (!activeQuests.TryGetValue(questId, out ActiveQuest activeQuest))
            {
                Debug.LogWarning($"Quest {questId} is not active");
                return false;
            }
            
            Quest quest = activeQuest.quest;
            
            // Validate all required objectives are complete
            if (!AreAllRequiredObjectivesComplete(quest))
            {
                Debug.LogWarning($"Cannot complete {questId}: objectives not met");
                return false;
            }
            
            // Grant rewards
            GrantRewards(quest);
            
            // Update state
            quest.state = QuestState.Completed;
            activeQuests.Remove(questId);
            completedQuests.Add(questId);
            
            // Unlock next quest in chain
            if (!string.IsNullOrEmpty(quest.nextQuestInChain))
            {
                UnlockQuest(quest.nextQuestInChain);
            }
            
            OnQuestCompleted?.Invoke(quest);
            UpdateQuestAvailability();
            
            Debug.Log($"Completed quest: {quest.questName}");
            return true;
        }
        
        /// <summary>
        /// Abandons an active quest.
        /// </summary>
        public bool AbandonQuest(string questId)
        {
            if (!activeQuests.TryGetValue(questId, out ActiveQuest activeQuest))
            {
                Debug.LogWarning($"Quest {questId} is not active");
                return false;
            }
            
            Quest quest = activeQuest.quest;
            quest.state = QuestState.Available; // Make available again
            activeQuests.Remove(questId);
            
            OnQuestAbandoned?.Invoke(quest);
            Debug.Log($"Abandoned quest: {quest.questName}");
            return true;
        }
        
        /// <summary>
        /// Fails an active quest.
        /// </summary>
        public bool FailQuest(string questId)
        {
            if (!activeQuests.TryGetValue(questId, out ActiveQuest activeQuest))
            {
                return false;
            }
            
            Quest quest = activeQuest.quest;
            quest.state = QuestState.Failed;
            activeQuests.Remove(questId);
            
            OnQuestFailed?.Invoke(quest);
            Debug.Log($"Failed quest: {quest.questName}");
            return true;
        }
        
        #endregion
        
        #region Objective Tracking
        
        /// <summary>
        /// Updates progress on a quest objective.
        /// </summary>
        public void UpdateObjective(string questId, string objectiveId, int amount = 1)
        {
            if (!activeQuests.TryGetValue(questId, out ActiveQuest activeQuest))
            {
                return;
            }
            
            Quest quest = activeQuest.quest;
            QuestObjective objective = quest.objectives.Find(o => o.objectiveId == objectiveId);
            
            if (objective == null || objective.isCompleted)
            {
                return;
            }
            
            objective.UpdateProgress(amount);
            OnObjectiveUpdated?.Invoke(quest, objective);
            
            // Check if quest is now complete
            if (AreAllRequiredObjectivesComplete(quest))
            {
                Debug.Log($"All objectives complete for quest: {quest.questName}");
            }
        }
        
        /// <summary>
        /// Updates objectives by target ID (e.g., when killing an enemy).
        /// </summary>
        public void UpdateObjectiveByTarget(string targetId, ObjectiveType type, int amount = 1)
        {
            foreach (var kvp in activeQuests)
            {
                Quest quest = kvp.Value.quest;
                
                foreach (var objective in quest.objectives)
                {
                    if (objective.type == type && 
                        objective.targetId == targetId && 
                        !objective.isCompleted)
                    {
                        UpdateObjective(quest.questId, objective.objectiveId, amount);
                    }
                }
            }
        }
        
        #endregion
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