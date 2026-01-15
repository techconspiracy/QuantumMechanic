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
/// <summary>
        /// Starts a dialogue conversation
        /// </summary>
        public void StartDialogue(string treeId)
        {
            if (!treeCache.ContainsKey(treeId))
            {
                Debug.LogError($"Dialogue tree '{treeId}' not found!");
                return;
            }

            if (currentState != null && !currentState.tree.canInterrupt)
            {
                Debug.LogWarning("Current dialogue cannot be interrupted!");
                return;
            }

            DialogueTree tree = treeCache[treeId];
            currentState = new DialogueState
            {
                tree = tree,
                startTime = Time.time,
                isPaused = false
            };

            OnDialogueStarted.Invoke(tree);
            
            // Find and play start node
            DialogueNode startNode = tree.nodes.Find(n => n.id == tree.startNodeId);
            if (startNode != null)
            {
                PlayNode(startNode);
            }
            else
            {
                Debug.LogError($"Start node '{tree.startNodeId}' not found in tree '{treeId}'!");
            }
        }

        /// <summary>
        /// Plays a specific dialogue node
        /// </summary>
        public void PlayNode(DialogueNode node)
        {
            if (currentState == null) return;

            // Check conditions
            if (!CheckConditions(node.conditions))
            {
                // Skip to next node if conditions not met
                if (!string.IsNullOrEmpty(node.nextNodeId))
                {
                    DialogueNode nextNode = GetNodeById(node.nextNodeId);
                    if (nextNode != null) PlayNode(nextNode);
                }
                return;
            }

            currentState.currentNode = node;
            currentState.visitedNodes.Add(node.id);

            // Add to history if enabled
            if (currentState.tree.saveHistory)
            {
                dialogueHistory.Add(node);
            }

            // Process variable substitution
            string processedText = ProcessVariables(node.text);
            node.text = processedText;

            // Get speaker character
            DialogueCharacter speaker = GetCharacter(node.speakerId);

            // Trigger events
            ProcessNodeEvents(node.events);

            // Invoke line spoken event
            OnLineSpoken.Invoke(node, speaker);

            // Handle auto-advance
            if (allowAutoAdvance && node.autoAdvanceDelay > 0 && node.choices.Count == 0)
            {
                StartCoroutine(AutoAdvanceCoroutine(node));
            }
        }

        /// <summary>
        /// Processes a dialogue choice made by player
        /// </summary>
        public void ProcessChoice(DialogueChoice choice)
        {
            if (currentState == null || currentState.currentNode == null) return;

            OnChoiceMade.Invoke(choice);

            // Apply affinity changes
            if (choice.affinityChange != 0 && currentState.currentNode != null)
            {
                DialogueCharacter speaker = GetCharacter(currentState.currentNode.speakerId);
                if (speaker != null)
                {
                    ChangeAffinity(speaker.characterId, choice.affinityChange);
                }
            }

            // Move to target node
            if (!string.IsNullOrEmpty(choice.targetNodeId))
            {
                DialogueNode targetNode = GetNodeById(choice.targetNodeId);
                if (targetNode != null)
                {
                    PlayNode(targetNode);
                }
                else
                {
                    EndDialogue();
                }
            }
            else
            {
                EndDialogue();
            }
        }

        /// <summary>
        /// Advances to next dialogue node
        /// </summary>
        public void AdvanceDialogue()
        {
            if (currentState == null || currentState.currentNode == null) return;

            // If there are choices, don't auto-advance
            if (currentState.currentNode.choices.Count > 0) return;

            // Move to next node
            if (!string.IsNullOrEmpty(currentState.currentNode.nextNodeId))
            {
                DialogueNode nextNode = GetNodeById(currentState.currentNode.nextNodeId);
                if (nextNode != null)
                {
                    PlayNode(nextNode);
                }
                else
                {
                    EndDialogue();
                }
            }
            else
            {
                EndDialogue();
            }
        }

        /// <summary>
        /// Checks if all conditions are met
        /// </summary>
        private bool CheckConditions(List<DialogueCondition> conditions)
        {
            foreach (var condition in conditions)
            {
                switch (condition.type)
                {
                    case DialogueCondition.ConditionType.QuestActive:
                        // Integration point with QuestSystem
                        break;
                    case DialogueCondition.ConditionType.HasItem:
                        // Integration point with InventorySystem
                        break;
                    case DialogueCondition.ConditionType.AffinityGreaterThan:
                        DialogueCharacter character = GetCharacter(condition.key);
                        if (character == null || character.affinity <= condition.value)
                            return false;
                        break;
                    case DialogueCondition.ConditionType.FlagSet:
                        if (!currentState.variables.ContainsKey(condition.key))
                            return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Processes dialogue events (give items, start quests, etc.)
        /// </summary>
        private void ProcessNodeEvents(List<DialogueEvent> events)
        {
            foreach (var evt in events)
            {
                switch (evt.type)
                {
                    case DialogueEvent.EventType.GiveItem:
                        // Integration with InventorySystem
                        Debug.Log($"Event: Give item {evt.key} x{evt.value}");
                        break;
                    case DialogueEvent.EventType.StartQuest:
                        // Integration with QuestSystem
                        Debug.Log($"Event: Start quest {evt.key}");
                        break;
                    case DialogueEvent.EventType.SetFlag:
                        SetVariable(evt.key, evt.value);
                        break;
                    case DialogueEvent.EventType.ChangeAffinity:
                        ChangeAffinity(evt.key, evt.value);
                        break;
                }
            }
        }

        /// <summary>
        /// Processes variable substitution in dialogue text
        /// </summary>
        private string ProcessVariables(string text)
        {
            if (currentState == null) return text;

            foreach (var kvp in currentState.variables)
            {
                string placeholder = $"{{{kvp.Key}}}";
                if (text.Contains(placeholder))
                {
                    text = text.Replace(placeholder, kvp.Value.ToString());
                }
            }
            return text;
        }

        /// <summary>
        /// Auto-advance coroutine for timed dialogue
        /// </summary>
        private System.Collections.IEnumerator AutoAdvanceCoroutine(DialogueNode node)
        {
            yield return new WaitForSeconds(node.autoAdvanceDelay);
            
            if (currentState != null && currentState.currentNode == node && !currentState.isPaused)
            {
                AdvanceDialogue();
            }
        }
/// <summary>
        /// Changes character affinity/relationship value
        /// </summary>
        public void ChangeAffinity(string characterId, int amount)
        {
            if (characterCache.ContainsKey(characterId))
            {
                DialogueCharacter character = characterCache[characterId];
                character.affinity += amount;
                character.affinity = Mathf.Clamp(character.affinity, -100, 100);
                Debug.Log($"{character.displayName} affinity changed by {amount} (now {character.affinity})");
            }
        }

        /// <summary>
        /// Pauses current dialogue
        /// </summary>
        public void PauseDialogue()
        {
            if (currentState != null)
            {
                currentState.isPaused = true;
                StopAllCoroutines();
            }
        }

        /// <summary>
        /// Resumes paused dialogue
        /// </summary>
        public void ResumeDialogue()
        {
            if (currentState != null && currentState.isPaused)
            {
                currentState.isPaused = false;
                
                // Restart auto-advance if applicable
                var node = currentState.currentNode;
                if (node != null && node.autoAdvanceDelay > 0 && node.choices.Count == 0)
                {
                    StartCoroutine(AutoAdvanceCoroutine(node));
                }
            }
        }

        /// <summary>
        /// Ends current dialogue conversation
        /// </summary>
        public void EndDialogue()
        {
            if (currentState == null) return;

            DialogueTree tree = currentState.tree;
            OnDialogueEnded.Invoke(tree);

            StopAllCoroutines();
            currentState = null;
        }

        /// <summary>
        /// Skips current line if skip is enabled
        /// </summary>
        public void SkipCurrentLine()
        {
            if (!allowSkip || currentState == null) return;
            
            StopAllCoroutines();
            AdvanceDialogue();
        }

        /// <summary>
        /// Gets character by ID
        /// </summary>
        public DialogueCharacter GetCharacter(string characterId)
        {
            if (characterCache.ContainsKey(characterId))
            {
                return characterCache[characterId];
            }
            return null;
        }

        /// <summary>
        /// Gets node by ID from current tree
        /// </summary>
        private DialogueNode GetNodeById(string nodeId)
        {
            if (currentState == null || currentState.tree == null) return null;
            return currentState.tree.nodes.Find(n => n.id == nodeId);
        }

        /// <summary>
        /// Sets a dialogue variable
        /// </summary>
        public void SetVariable(string key, object value)
        {
            if (currentState == null) currentState = new DialogueState();
            
            currentState.variables[key] = value;
            OnVariableChanged.Invoke(key);
        }

        /// <summary>
        /// Gets a dialogue variable
        /// </summary>
        public T GetVariable<T>(string key, T defaultValue = default)
        {
            if (currentState?.variables != null && currentState.variables.ContainsKey(key))
            {
                return (T)currentState.variables[key];
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets portrait sprite for character emotion
        /// </summary>
        public Sprite GetPortrait(string characterId, string emotion)
        {
            DialogueCharacter character = GetCharacter(characterId);
            if (character == null) return null;

            return emotion.ToLower() switch
            {
                "happy" => character.portraitHappy ?? character.portraitNeutral,
                "angry" => character.portraitAngry ?? character.portraitNeutral,
                "sad" => character.portraitSad ?? character.portraitNeutral,
                "surprised" => character.portraitSurprised ?? character.portraitNeutral,
                _ => character.portraitNeutral
            };
        }

        /// <summary>
        /// Checks if player has seen a specific node before
        /// </summary>
        public bool HasSeenNode(string nodeId)
        {
            return dialogueHistory.Exists(n => n.id == nodeId);
        }

        /// <summary>
        /// Gets available choices for current node (filtered by conditions)
        /// </summary>
        public List<DialogueChoice> GetAvailableChoices()
        {
            if (currentState?.currentNode == null) return new List<DialogueChoice>();

            List<DialogueChoice> available = new List<DialogueChoice>();
            foreach (var choice in currentState.currentNode.choices)
            {
                if (CheckConditions(choice.conditions))
                {
                    available.Add(choice);
                }
            }
            return available;
        }

        /// <summary>
        /// Clears dialogue history
        /// </summary>
        public void ClearHistory()
        {
            dialogueHistory.Clear();
        }

        /// <summary>
        /// Checks if dialogue is currently active
        /// </summary>
        public bool IsDialogueActive()
        {
            return currentState != null;
        }

        /// <summary>
        /// Gets current dialogue state
        /// </summary>
        public DialogueState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// Adds a new dialogue tree at runtime
        /// </summary>
        public void AddDialogueTree(DialogueTree tree)
        {
            if (!treeCache.ContainsKey(tree.treeId))
            {
                dialogueTrees.Add(tree);
                treeCache.Add(tree.treeId, tree);
            }
        }

        /// <summary>
        /// Adds a new character at runtime
        /// </summary>
        public void AddCharacter(DialogueCharacter character)
        {
            if (!characterCache.ContainsKey(character.characterId))
            {
                characters.Add(character);
                characterCache.Add(character.characterId, character);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
                /// <summary>
        /// Get current dialogue state
        /// </summary>
        public DialogueState GetState() => currentState;
        
        /// <summary>
        /// Check if dialogue is active
        /// </summary>
        public bool IsDialogueActive() => currentState != DialogueState.Inactive;
        
    }
}
