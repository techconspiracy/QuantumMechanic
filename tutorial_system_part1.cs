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