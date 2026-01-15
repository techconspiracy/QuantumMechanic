using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Tutorial
{
    #region Enums
    
    /// <summary>
    /// Current state of a tutorial
    /// </summary>
    public enum TutorialState
    {
        NotStarted,
        Active,
        Paused,
        Completed,
        Skipped
    }
    
    /// <summary>
    /// Types of tutorial triggers
    /// </summary>
    public enum TriggerType
    {
        Manual,
        Automatic,
        LocationEnter,
        ActionPerformed,
        ItemAcquired,
        QuestStarted,
        TimeElapsed,
        Conditional
    }
    
    /// <summary>
    /// Conditions for completing a tutorial step
    /// </summary>
    public enum CompletionCondition
    {
        Manual,
        ActionPerformed,
        LocationReached,
        ItemCollected,
        DialogueCompleted,
        TimeElapsed,
        ButtonPressed,
        VariableEquals
    }
    
    /// <summary>
    /// Types of tutorial hints
    /// </summary>
    public enum HintType
    {
        Tooltip,
        FullScreen,
        Highlight,
        Popup,
        Notification
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Individual step in a tutorial sequence
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        public string stepID;
        public string title;
        public string description;
        public HintType hintType;
        public CompletionCondition completionCondition;
        public string completionValue;
        public bool blockInput;
        public bool isOptional;
        public float timeoutDuration;
        public string highlightTargetID;
        public Action onStepStart;
        public Action onStepComplete;
        
        public TutorialStep(string id, string stepTitle, string stepDescription)
        {
            stepID = id;
            title = stepTitle;
            description = stepDescription;
            hintType = HintType.Tooltip;
            completionCondition = CompletionCondition.Manual;
            blockInput = false;
            isOptional = false;
            timeoutDuration = 0f;
        }
    }
    
    /// <summary>
    /// Complete tutorial sequence
    /// </summary>
    [Serializable]
    public class TutorialSequence
    {
        public string sequenceID;
        public string sequenceName;
        public List<TutorialStep> steps;
        public TriggerType triggerType;
        public string triggerValue;
        public bool canSkip;
        public bool canReplay;
        public int priority;
        public List<string> prerequisiteTutorials;
        public string nextTutorialID;
        
        public TutorialSequence(string id, string name)
        {
            sequenceID = id;
            sequenceName = name;
            steps = new List<TutorialStep>();
            triggerType = TriggerType.Manual;
            canSkip = true;
            canReplay = true;
            priority = 0;
            prerequisiteTutorials = new List<string>();
        }
    }
    
    /// <summary>
    /// Tutorial trigger data
    /// </summary>
    [Serializable]
    public class TutorialTrigger
    {
        public string tutorialID;
        public TriggerType triggerType;
        public string triggerCondition;
        public bool hasTriggered;
        
        public TutorialTrigger(string id, TriggerType type, string condition = "")
        {
            tutorialID = id;
            triggerType = type;
            triggerCondition = condition;
            hasTriggered = false;
        }
    }
    
    #endregion
    
    /// <summary>
    /// Manages interactive tutorials, hints, and player guidance
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        #region Singleton
        
        private static TutorialSystem instance;
        public static TutorialSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<TutorialSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("TutorialSystem");
                        instance = go.AddComponent<TutorialSystem>();
                    }
                }
                return instance;
            }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Tutorial State")]
        private TutorialState currentState = TutorialState.NotStarted;
        private TutorialSequence activeTutorial;
        private TutorialStep currentStep;
        private int currentStepIndex;
        private float stepStartTime;
        
        [Header("Tutorial Data")]
        private Dictionary<string, TutorialSequence> tutorials = new Dictionary<string, TutorialSequence>();
        private HashSet<string> completedTutorials = new HashSet<string>();
        private List<TutorialTrigger> activeTriggers = new List<TutorialTrigger>();
        
        [Header("Settings")]
        [SerializeField] private bool enableTutorials = true;
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private float defaultStepTimeout = 0f;
        [SerializeField] private bool blockInputDuringTutorials = true;
        
        #endregion
        #region Events
        
        public event Action<TutorialSequence> OnTutorialStart;
        public event Action<TutorialSequence> OnTutorialComplete;
        public event Action<TutorialStep> OnStepStart;
        public event Action<TutorialStep> OnStepComplete;
        public event Action<string> OnHighlightTarget;
        public event Action OnClearHighlight;
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Initialize the tutorial system
        /// </summary>
        private void Initialize()
        {
            tutorials.Clear();
            completedTutorials.Clear();
            activeTriggers.Clear();
            currentState = TutorialState.NotStarted;
            
            Debug.Log("[TutorialSystem] Initialized");
        }
        
        private void Update()
        {
            if (currentState == TutorialState.Active && currentStep != null)
            {
                CheckStepCompletion();
                CheckStepTimeout();
            }
        }
        
        #endregion
        
        #region Tutorial Control
        
        /// <summary>
        /// Start a tutorial sequence
        /// </summary>
        public void StartTutorial(string tutorialID)
        {
            if (!enableTutorials)
            {
                Debug.Log("[TutorialSystem] Tutorials disabled");
                return;
            }
            
            if (!tutorials.ContainsKey(tutorialID))
            {
                Debug.LogError($"[TutorialSystem] Tutorial '{tutorialID}' not found");
                return;
            }
            
            if (completedTutorials.Contains(tutorialID))
            {
                Debug.Log($"[TutorialSystem] Tutorial '{tutorialID}' already completed");
                return;
            }
            
            TutorialSequence tutorial = tutorials[tutorialID];
            
            // Check prerequisites
            foreach (string prereq in tutorial.prerequisiteTutorials)
            {
                if (!completedTutorials.Contains(prereq))
                {
                    Debug.LogWarning($"[TutorialSystem] Prerequisite '{prereq}' not met");
                    return;
                }
            }
            
            if (currentState == TutorialState.Active)
            {
                Debug.LogWarning("[TutorialSystem] Another tutorial is active");
                return;
            }
            
            activeTutorial = tutorial;
            currentState = TutorialState.Active;
            currentStepIndex = 0;
            
            OnTutorialStart?.Invoke(activeTutorial);
            Debug.Log($"[TutorialSystem] Started tutorial: {tutorial.sequenceName}");
            
            if (tutorial.steps.Count > 0)
            {
                StartStep(tutorial.steps[0]);
            }
        }
        
        /// <summary>
        /// Complete the current tutorial
        /// </summary>
        public void CompleteTutorial()
        {
            if (currentState != TutorialState.Active) return;
            
            currentStep?.onStepComplete?.Invoke();
            
            string tutorialID = activeTutorial.sequenceID;
            completedTutorials.Add(tutorialID);
            
            OnTutorialComplete?.Invoke(activeTutorial);
            OnClearHighlight?.Invoke();
            
            Debug.Log($"[TutorialSystem] Completed tutorial: {activeTutorial.sequenceName}");
            
            // Chain to next tutorial if specified
            string nextID = activeTutorial.nextTutorialID;
            
            activeTutorial = null;
            currentStep = null;
            currentState = TutorialState.NotStarted;
            
            if (!string.IsNullOrEmpty(nextID))
            {
                StartTutorial(nextID);
            }
        }
        
        /// <summary>
        /// Skip the current tutorial
        /// </summary>
        public void SkipTutorial()
        {
            if (currentState != TutorialState.Active) return;
            if (activeTutorial != null && !activeTutorial.canSkip) return;
            
            Debug.Log($"[TutorialSystem] Skipped tutorial: {activeTutorial?.sequenceName}");
            
            currentState = TutorialState.Skipped;
            OnClearHighlight?.Invoke();
            
            activeTutorial = null;
            currentStep = null;
            currentState = TutorialState.NotStarted;
        }
        
        #endregion
        
        #region Step Processing
        
        /// <summary>
        /// Start a tutorial step
        /// </summary>
        private void StartStep(TutorialStep step)
        {
            currentStep = step;
            stepStartTime = Time.time;
            
            step.onStepStart?.Invoke();
            OnStepStart?.Invoke(step);
            
            // Highlight UI element if specified
            if (!string.IsNullOrEmpty(step.highlightTargetID))
            {
                OnHighlightTarget?.Invoke(step.highlightTargetID);
            }
            
            Debug.Log($"[TutorialSystem] Step started: {step.title}");
        }
        
        /// <summary>
        /// Complete current step and advance
        /// </summary>
        public void CompleteStep()
        {
            if (currentState != TutorialState.Active || currentStep == null) return;
            
            currentStep.onStepComplete?.Invoke();
            OnStepComplete?.Invoke(currentStep);
            OnClearHighlight?.Invoke();
            
            Debug.Log($"[TutorialSystem] Step completed: {currentStep.title}");
            
            currentStepIndex++;
            
            if (currentStepIndex < activeTutorial.steps.Count)
            {
                StartStep(activeTutorial.steps[currentStepIndex]);
            }
            else
            {
                CompleteTutorial();
            }
        }
        
        /// <summary>
        /// Check if step completion condition is met
        /// </summary>
        private void CheckStepCompletion()
        {
            if (currentStep.completionCondition == CompletionCondition.Manual)
                return;
            
            // Auto-completion logic would go here
            // Integrate with game systems to check conditions
        }
        
        /// <summary>
        /// Check if step has timed out
        /// </summary>
        private void CheckStepTimeout()
        {
            float timeout = currentStep.timeoutDuration > 0 
                ? currentStep.timeoutDuration 
                : defaultStepTimeout;
            
            if (timeout > 0 && Time.time - stepStartTime >= timeout)
            {
                if (currentStep.isOptional)
                {
                    CompleteStep();
                }
            }
        }
        
        #endregion
        #region Trigger System
        
        /// <summary>
        /// Register a tutorial trigger
        /// </summary>
        public void RegisterTrigger(string tutorialID, TriggerType type, string condition = "")
        {
            TutorialTrigger trigger = new TutorialTrigger(tutorialID, type, condition);
            activeTriggers.Add(trigger);
            
            Debug.Log($"[TutorialSystem] Registered trigger for: {tutorialID}");
        }
        
        /// <summary>
        /// Check and fire triggers based on action
        /// </summary>
        public void CheckTriggers(TriggerType type, string value = "")
        {
            for (int i = activeTriggers.Count - 1; i >= 0; i--)
            {
                TutorialTrigger trigger = activeTriggers[i];
                
                if (trigger.hasTriggered) continue;
                if (trigger.triggerType != type) continue;
                
                bool shouldTrigger = string.IsNullOrEmpty(trigger.triggerCondition) 
                    || trigger.triggerCondition == value;
                
                if (shouldTrigger)
                {
                    trigger.hasTriggered = true;
                    StartTutorial(trigger.tutorialID);
                    activeTriggers.RemoveAt(i);
                }
            }
        }
        
        #endregion
        
        #region Tutorial Management
        
        /// <summary>
        /// Register a tutorial sequence
        /// </summary>
        public void RegisterTutorial(TutorialSequence tutorial)
        {
            if (tutorials.ContainsKey(tutorial.sequenceID))
            {
                Debug.LogWarning($"[TutorialSystem] Overwriting tutorial: {tutorial.sequenceID}");
            }
            
            tutorials[tutorial.sequenceID] = tutorial;
            Debug.Log($"[TutorialSystem] Registered tutorial: {tutorial.sequenceName}");
        }
        
        /// <summary>
        /// Mark tutorial as completed
        /// </summary>
        public void MarkAsCompleted(string tutorialID)
        {
            completedTutorials.Add(tutorialID);
        }
        
        /// <summary>
        /// Reset tutorial to allow replay
        /// </summary>
        public void ResetTutorial(string tutorialID)
        {
            completedTutorials.Remove(tutorialID);
            Debug.Log($"[TutorialSystem] Reset tutorial: {tutorialID}");
        }
        
        /// <summary>
        /// Check if tutorial has been completed
        /// </summary>
        public bool IsTutorialCompleted(string tutorialID)
        {
            return completedTutorials.Contains(tutorialID);
        }
        
        #endregion
        
        #region Context-Sensitive Help
        
        /// <summary>
        /// Show contextual hint
        /// </summary>
        public void ShowHint(string message, HintType type = HintType.Tooltip, float duration = 3f)
        {
            // Integration with UI system for displaying hints
            Debug.Log($"[TutorialSystem] Hint: {message}");
        }
        
        /// <summary>
        /// Clear all active hints
        /// </summary>
        public void ClearHints()
        {
            OnClearHighlight?.Invoke();
        }
        
        #endregion
        
        #region Save/Load
        
        /// <summary>
        /// Get tutorial progress for saving
        /// </summary>
        public TutorialSaveData GetSaveData()
        {
            return new TutorialSaveData
            {
                completedTutorials = new List<string>(completedTutorials),
                tutorialsEnabled = enableTutorials,
                currentTutorialID = activeTutorial?.sequenceID ?? "",
                currentStepIndex = currentStepIndex
            };
        }
        
        /// <summary>
        /// Load tutorial progress
        /// </summary>
        public void LoadSaveData(TutorialSaveData data)
        {
            if (data == null) return;
            
            completedTutorials.Clear();
            foreach (string id in data.completedTutorials)
            {
                completedTutorials.Add(id);
            }
            
            enableTutorials = data.tutorialsEnabled;
            
            Debug.Log($"[TutorialSystem] Loaded {completedTutorials.Count} completed tutorials");
        }
        
        #endregion
        
        #region Analytics & Debugging
        
        /// <summary>
        /// Log tutorial analytics event
        /// </summary>
        private void LogAnalytics(string eventName, Dictionary<string, object> parameters = null)
        {
            // Integration point for analytics
            Debug.Log($"[TutorialSystem] Analytics: {eventName}");
        }
        
        /// <summary>
        /// Get tutorial statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                { "totalTutorials", tutorials.Count },
                { "completedTutorials", completedTutorials.Count },
                { "activeTriggers", activeTriggers.Count },
                { "currentState", currentState.ToString() }
            };
        }
        
        #endregion
        
        #region Utility
        
        /// <summary>
        /// Get current tutorial state
        /// </summary>
        public TutorialState GetState() => currentState;
        
        /// <summary>
        /// Check if any tutorial is active
        /// </summary>
        public bool IsTutorialActive() => currentState == TutorialState.Active;
        
        /// <summary>
        /// Enable or disable tutorials
        /// </summary>
        public void SetTutorialsEnabled(bool enabled)
        {
            enableTutorials = enabled;
            if (!enabled && currentState == TutorialState.Active)
            {
                SkipTutorial();
            }
        }
        
        /// <summary>
        /// Check if input should be blocked
        /// </summary>
        public bool ShouldBlockInput()
        {
            return blockInputDuringTutorials 
                && currentState == TutorialState.Active 
                && currentStep?.blockInput == true;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Serializable tutorial save data
    /// </summary>
    [Serializable]
    public class TutorialSaveData
    {
        public List<string> completedTutorials;
        public bool tutorialsEnabled;
        public string currentTutorialID;
        public int currentStepIndex;
    }
}