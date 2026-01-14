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
    }
}
