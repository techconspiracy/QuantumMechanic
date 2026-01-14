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