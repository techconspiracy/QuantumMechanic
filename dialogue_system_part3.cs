#region Choice Handling
        
        /// <summary>
        /// Show available choices to player
        /// </summary>
        private void ShowChoices()
        {
            currentState = DialogueState.WaitingForChoice;
            
            List<DialogueChoice> availableChoices = new List<DialogueChoice>();
            foreach (var choice in currentNode.choices)
            {
                if (CheckChoiceRequirement(choice))
                {
                    availableChoices.Add(choice);
                }
            }
            
            OnChoicesDisplay?.Invoke(availableChoices);
        }
        
        /// <summary>
        /// Handle player choice selection
        /// </summary>
        public void SelectChoice(int choiceIndex)
        {
            if (currentState != DialogueState.WaitingForChoice) return;
            if (choiceIndex < 0 || choiceIndex >= currentNode.choices.Count) return;
            
            DialogueChoice choice = currentNode.choices[choiceIndex];
            if (!CheckChoiceRequirement(choice)) return;
            
            Debug.Log($"[DialogueSystem] Choice selected: {choice.choiceText}");
            
            if (!string.IsNullOrEmpty(choice.nextNodeID))
            {
                ProcessNode(choice.nextNodeID);
            }
            else
            {
                EndDialogue();
            }
        }
        
        /// <summary>
        /// Check if choice requirements are met
        /// </summary>
        private bool CheckChoiceRequirement(DialogueChoice choice)
        {
            if (choice.requirement == ChoiceRequirement.None) return true;
            
            switch (choice.requirement)
            {
                case ChoiceRequirement.VariableEquals:
                    return GetVariable<int>(choice.requirementValue, 0) == choice.requirementAmount;
                    
                case ChoiceRequirement.VariableGreaterThan:
                    return GetVariable<int>(choice.requirementValue, 0) > choice.requirementAmount;
                    
                case ChoiceRequirement.QuestActive:
                    // Integration with QuestSystem
                    return false; // Implement with QuestSystem
                    
                case ChoiceRequirement.QuestComplete:
                    // Integration with QuestSystem
                    return false; // Implement with QuestSystem
                    
                default:
                    return true;
            }
        }
        
        #endregion
        
        #region Variable System
        
        /// <summary>
        /// Set a dialogue variable
        /// </summary>
        public void SetVariable(string key, object value)
        {
            dialogueVariables[key] = value;
            OnVariableChanged?.Invoke(key);
            Debug.Log($"[DialogueSystem] Variable set: {key} = {value}");
        }
        
        /// <summary>
        /// Get a dialogue variable
        /// </summary>
        public T GetVariable<T>(string key, T defaultValue = default)
        {
            if (dialogueVariables.ContainsKey(key))
            {
                try
                {
                    return (T)dialogueVariables[key];
                }
                catch
                {
                    Debug.LogWarning($"[DialogueSystem] Variable type mismatch: {key}");
                }
            }
            return defaultValue;
        }
        
        /// <summary>
        /// Check if variable exists
        /// </summary>
        public bool HasVariable(string key)
        {
            return dialogueVariables.ContainsKey(key);
        }
        
        /// <summary>
        /// Clear all dialogue variables
        /// </summary>
        public void ClearVariables()
        {
            dialogueVariables.Clear();
        }
        
        #endregion
        
        #region Dialogue Tree Management
        
        /// <summary>
        /// Register a dialogue tree
        /// </summary>
        public void RegisterDialogueTree(DialogueTree tree)
        {
            if (dialogueTrees.ContainsKey(tree.treeID))
            {
                Debug.LogWarning($"[DialogueSystem] Overwriting dialogue tree: {tree.treeID}");
            }
            dialogueTrees[tree.treeID] = tree;
            Debug.Log($"[DialogueSystem] Registered dialogue tree: {tree.treeName}");
        }
        
        /// <summary>
        /// Remove a dialogue tree
        /// </summary>
        public void UnregisterDialogueTree(string treeID)
        {
            if (dialogueTrees.ContainsKey(treeID))
            {
                dialogueTrees.Remove(treeID);
                Debug.Log($"[DialogueSystem] Unregistered dialogue tree: {treeID}");
            }
        }
        
        #endregion
        
        #region Save/Load
        
        /// <summary>
        /// Get current dialogue state for saving
        /// </summary>
        public Dictionary<string, object> GetSaveData()
        {
            return new Dictionary<string, object>
            {
                { "variables", new Dictionary<string, object>(dialogueVariables) },
                { "activeTreeID", activeTree?.treeID ?? "" },
                { "currentNodeID", currentNode?.nodeID ?? "" }
            };
        }
        
        /// <summary>
        /// Load dialogue state
        /// </summary>
        public void LoadSaveData(Dictionary<string, object> saveData)
        {
            if (saveData.ContainsKey("variables"))
            {
                dialogueVariables = saveData["variables"] as Dictionary<string, object> ?? new Dictionary<string, object>();
            }
            
            Debug.Log("[DialogueSystem] Save data loaded");
        }
        
        #endregion
        
        #region Utility
        
        /// <summary>
        /// Get current dialogue state
        /// </summary>
        public DialogueState GetState() => currentState;
        
        /// <summary>
        /// Check if dialogue is active
        /// </summary>
        public bool IsDialogueActive() => currentState != DialogueState.Inactive;
        
        #endregion
    }
}