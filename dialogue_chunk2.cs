using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Dialogue
{
    /// <summary>
    /// Manages active dialogue conversations, flow control, and player interactions.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("Dialogue State")]
        private DialogueTree currentDialogue;
        private DialogueNode currentNode;
        private bool isDialogueActive;
        private bool isTyping;
        private Coroutine typewriterCoroutine;
        
        [Header("Dialogue Memory")]
        private Dictionary<string, int> dialogueVariables = new Dictionary<string, int>();
        private Dictionary<string, int> npcRelationships = new Dictionary<string, int>();
        private List<string> conversationHistory = new List<string>();
        private Dictionary<string, List<string>> choicesMade = new Dictionary<string, List<string>>();
        
        [Header("Settings")]
        [SerializeField] private float defaultTypeSpeed = 0.05f;
        [SerializeField] private int maxHistoryEntries = 100;
        [SerializeField] private AudioSource voiceSource;
        
        public event Action<DialogueNode> OnNodeDisplayed;
        public event Action<string, string> OnDialogueTextUpdate; // speaker, text
        public event Action<List<DialogueChoice>> OnChoicesPresented;
        public event Action OnDialogueEnded;
        public event Action<string> OnChoiceSelected;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        /// <summary>
        /// Starts a new dialogue conversation from a dialogue tree.
        /// </summary>
        public void StartDialogue(DialogueTree dialogue)
        {
            if (isDialogueActive) EndDialogue();
            
            currentDialogue = dialogue;
            currentNode = dialogue.GetStartNode();
            isDialogueActive = true;
            
            if (currentNode != null && currentNode.MeetsConditions())
            {
                DisplayNode(currentNode);
            }
            else
            {
                Debug.LogWarning($"Starting node for {dialogue.dialogueName} failed conditions!");
                EndDialogue();
            }
        }

        /// <summary>
        /// Displays a dialogue node with typewriter effect and choices.
        /// </summary>
        private void DisplayNode(DialogueNode node)
        {
            currentNode = node;
            node.ExecuteActions();
            OnNodeDisplayed?.Invoke(node);
            
            AddToHistory(node.speakerName, node.dialogueText);
            
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = StartCoroutine(TypewriterEffect(node));
            
            PlayVoiceLine(node.voiceClipId);
        }

        /// <summary>
        /// Typewriter text animation effect for dialogue.
        /// </summary>
        private IEnumerator TypewriterEffect(DialogueNode node)
        {
            isTyping = true;
            string fullText = node.dialogueText;
            float speed = node.textSpeed > 0 ? node.textSpeed : defaultTypeSpeed;
            
            for (int i = 0; i <= fullText.Length; i++)
            {
                string currentText = fullText.Substring(0, i);
                OnDialogueTextUpdate?.Invoke(node.speakerName, currentText);
                yield return new WaitForSeconds(speed);
            }
            
            isTyping = false;
            PresentChoices(node);
        }

        /// <summary>
        /// Presents available dialogue choices to the player.
        /// </summary>
        private void PresentChoices(DialogueNode node)
        {
            List<DialogueChoice> availableChoices = new List<DialogueChoice>();
            
            foreach (var choice in node.choices)
            {
                if (choice.IsAvailable())
                {
                    availableChoices.Add(choice);
                }
            }
            
            // Auto-continue if no choices available
            if (availableChoices.Count == 0 && !string.IsNullOrEmpty(node.nextNodeId))
            {
                StartCoroutine(AutoContinue(node.nextNodeId));
            }
            else
            {
                OnChoicesPresented?.Invoke(availableChoices);
            }
        }

        /// <summary>
        /// Auto-continues to next node after a delay.
        /// </summary>
        private IEnumerator AutoContinue(string nextNodeId)
        {
            yield return new WaitForSeconds(0.5f);
            MakeChoice(new DialogueChoice { targetNodeId = nextNodeId });
        }

        /// <summary>
        /// Processes player choice selection and branches dialogue.
        /// </summary>
        public void MakeChoice(DialogueChoice choice)
        {
            if (!isDialogueActive) return;
            
            OnChoiceSelected?.Invoke(choice.choiceText);
            RecordChoice(currentDialogue.dialogueId, choice.choiceText);
            
            choice.actions.ForEach(a => a.Execute());
            
            if (choice.relationshipChange != 0 && currentNode != null)
            {
                ModifyRelationship(currentNode.speakerName, choice.relationshipChange);
            }
            
            if (choice.endsDialogue || string.IsNullOrEmpty(choice.targetNodeId))
            {
                EndDialogue();
                return;
            }
            
            DialogueNode nextNode = currentDialogue.GetNode(choice.targetNodeId);
            if (nextNode != null && nextNode.MeetsConditions())
            {
                DisplayNode(nextNode);
            }
            else
            {
                Debug.LogWarning($"Target node {choice.targetNodeId} not found or failed conditions!");
                EndDialogue();
            }
        }

        /// <summary>
        /// Skips typewriter effect and shows full text immediately.
        /// </summary>
        public void SkipTypewriter()
        {
            if (isTyping && currentNode != null && currentNode.canSkip)
            {
                if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
                isTyping = false;
                OnDialogueTextUpdate?.Invoke(currentNode.speakerName, currentNode.dialogueText);
                PresentChoices(currentNode);
            }
        }

        /// <summary>
        /// Ends the current dialogue conversation.
        /// </summary>
        public void EndDialogue()
        {
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            isDialogueActive = false;
            currentDialogue = null;
            currentNode = null;
            OnDialogueEnded?.Invoke();
        }

        /// <summary>
        /// Plays voice line audio clip if available.
        /// </summary>
        private void PlayVoiceLine(string clipId)
        {
            if (voiceSource != null && !string.IsNullOrEmpty(clipId))
            {
                // Load and play voice clip - implementation depends on audio system
                // voiceSource.PlayOneShot(LoadVoiceClip(clipId));
            }
        }

        // Variable and relationship management methods
        public void SetVariable(string varName, int value) => dialogueVariables[varName] = value;
        public int GetVariable(string varName) => dialogueVariables.ContainsKey(varName) ? dialogueVariables[varName] : 0;
        public bool EvaluateVariable(string varName, int value, ComparisonOperator op)
        {
            int current = GetVariable(varName);
            switch (op)
            {
                case ComparisonOperator.Equal: return current == value;
                case ComparisonOperator.Greater: return current > value;
                case ComparisonOperator.GreaterOrEqual: return current >= value;
                default: return false;
            }
        }

        public void ModifyRelationship(string npcId, int change)
        {
            int current = GetRelationship(npcId);
            npcRelationships[npcId] = Mathf.Clamp(current + change, -100, 100);
        }

        public int GetRelationship(string npcId) => npcRelationships.ContainsKey(npcId) ? npcRelationships[npcId] : 0;

        private void AddToHistory(string speaker, string text)
        {
            conversationHistory.Add($"{speaker}: {text}");
            if (conversationHistory.Count > maxHistoryEntries)
                conversationHistory.RemoveAt(0);
        }

        private void RecordChoice(string dialogueId, string choiceText)
        {
            if (!choicesMade.ContainsKey(dialogueId))
                choicesMade[dialogueId] = new List<string>();
            choicesMade[dialogueId].Add(choiceText);
        }

        public bool IsDialogueActive() => isDialogueActive;
        public List<string> GetConversationHistory() => new List<string>(conversationHistory);
    }
}