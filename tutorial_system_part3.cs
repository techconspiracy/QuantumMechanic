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