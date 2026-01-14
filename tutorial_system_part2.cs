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