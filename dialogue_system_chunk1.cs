using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuantumMechanic.Dialogue
{
    /// <summary>
    /// Represents a single node in a dialogue tree
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public string id;
        public string speakerId;
        public string text;
        public string emotion = "neutral";
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public List<DialogueEvent> events = new List<DialogueEvent>();
        public float autoAdvanceDelay = -1f;
        public string nextNodeId;
    }

    /// <summary>
    /// Represents a choice option in dialogue
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        public string text;
        public string targetNodeId;
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public int affinityChange;
        public float timeLimit = -1f;
    }

    /// <summary>
    /// Condition that must be met for dialogue/choice to be available
    /// </summary>
    [Serializable]
    public class DialogueCondition
    {
        public enum ConditionType { QuestActive, QuestComplete, HasItem, StatGreaterThan, 
                                   AffinityGreaterThan, FlagSet, CustomCondition }
        public ConditionType type;
        public string key;
        public float value;
    }

    /// <summary>
    /// Event triggered when a dialogue node is played
    /// </summary>
    [Serializable]
    public class DialogueEvent
    {
        public enum EventType { GiveItem, StartQuest, CompleteQuest, SetFlag, 
                               PlayAnimation, TriggerCutscene, ChangeAffinity, GiveReward }
        public EventType type;
        public string key;
        public int value;
    }

    /// <summary>
    /// Complete dialogue tree data structure
    /// </summary>
    [Serializable]
    public class DialogueTree
    {
        public string treeId;
        public string startNodeId;
        public List<DialogueNode> nodes = new List<DialogueNode>();
        public bool canInterrupt = true;
        public bool saveHistory = true;
    }

    /// <summary>
    /// Character information for dialogue
    /// </summary>
    [Serializable]
    public class DialogueCharacter
    {
        public string characterId;
        public string displayName;
        public Sprite portraitNeutral;
        public Sprite portraitHappy;
        public Sprite portraitAngry;
        public Sprite portraitSad;
        public Sprite portraitSurprised;
        public AudioClip voiceClip;
        public Color nameColor = Color.white;
        public int affinity;
    }

    /// <summary>
    /// Current state of active dialogue
    /// </summary>
    public class DialogueState
    {
        public DialogueTree tree;
        public DialogueNode currentNode;
        public List<string> visitedNodes = new List<string>();
        public Dictionary<string, object> variables = new Dictionary<string, object>();
        public float startTime;
        public bool isPaused;
    }

    /// <summary>
    /// Complete dialogue system - manages conversations, branching, and NPC interactions
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        private static DialogueSystem _instance;
        public static DialogueSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<DialogueSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("DialogueSystem");
                        _instance = go.AddComponent<DialogueSystem>();
                    }
                }
                return _instance;
            }
        }

        [Header("Dialogue Data")]
        [SerializeField] private List<DialogueTree> dialogueTrees = new List<DialogueTree>();
        [SerializeField] private List<DialogueCharacter> characters = new List<DialogueCharacter>();
        
        [Header("Settings")]
        [SerializeField] private float defaultTypewriterSpeed = 0.05f;
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private bool allowAutoAdvance = true;

        // Events
        public UnityEvent<DialogueTree> OnDialogueStarted = new UnityEvent<DialogueTree>();
        public UnityEvent<DialogueNode, DialogueCharacter> OnLineSpoken = new UnityEvent<DialogueNode, DialogueCharacter>();
        public UnityEvent<DialogueChoice> OnChoiceMade = new UnityEvent<DialogueChoice>();
        public UnityEvent<DialogueTree> OnDialogueEnded = new UnityEvent<DialogueTree>();
        public UnityEvent<string> OnVariableChanged = new UnityEvent<string>();

        // State
        private DialogueState currentState;
        private Dictionary<string, DialogueTree> treeCache = new Dictionary<string, DialogueTree>();
        private Dictionary<string, DialogueCharacter> characterCache = new Dictionary<string, DialogueCharacter>();
        private List<DialogueNode> dialogueHistory = new List<DialogueNode>();
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeSystem();
        }

        /// <summary>
        /// Initializes the dialogue system and caches data
        /// </summary>
        private void InitializeSystem()
        {
            // Cache dialogue trees
            foreach (var tree in dialogueTrees)
            {
                if (!treeCache.ContainsKey(tree.treeId))
                {
                    treeCache.Add(tree.treeId, tree);
                }
            }

            // Cache characters
            foreach (var character in characters)
            {
                if (!characterCache.ContainsKey(character.characterId))
                {
                    characterCache.Add(character.characterId, character);
                }
            }

            Debug.Log($"DialogueSystem initialized: {treeCache.Count} trees, {characterCache.Count} characters");
        }
