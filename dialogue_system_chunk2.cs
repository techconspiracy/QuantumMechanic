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
