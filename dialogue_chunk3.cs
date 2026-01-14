using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace QuantumMechanic.Dialogue
{
    /// <summary>
    /// Triggers dialogue based on proximity, interaction, or game events.
    /// </summary>
    public class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueTree dialogueTree;
        [SerializeField] private TriggerType triggerType;
        [SerializeField] private float proximityRadius = 3f;
        [SerializeField] private string triggerEventId;
        [SerializeField] private bool triggerOnce = false;
        private bool hasTriggered = false;

        public enum TriggerType { Proximity, Interaction, Event, AutoStart }

        private void Start()
        {
            if (triggerType == TriggerType.AutoStart && !hasTriggered)
            {
                TriggerDialogue();
            }
        }

        private void Update()
        {
            if (triggerType == TriggerType.Proximity && !hasTriggered)
            {
                CheckProximity();
            }
        }

        private void CheckProximity()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && Vector3.Distance(transform.position, player.transform.position) <= proximityRadius)
            {
                TriggerDialogue();
            }
        }

        public void OnInteract()
        {
            if (triggerType == TriggerType.Interaction && (!triggerOnce || !hasTriggered))
            {
                TriggerDialogue();
            }
        }

        private void TriggerDialogue()
        {
            if (dialogueTree != null)
            {
                DialogueManager.Instance.StartDialogue(dialogueTree);
                hasTriggered = true;
            }
        }
    }

    /// <summary>
    /// Handles cutscene dialogue sequences with multiple speakers and timing.
    /// </summary>
    public class CutsceneDialogue : MonoBehaviour
    {
        [Serializable]
        public class CutsceneLine
        {
            public string speakerName;
            public string text;
            public float duration = 3f;
            public string cameraTarget;
            public string animation;
        }

        [SerializeField] private List<CutsceneLine> lines = new List<CutsceneLine>();
        private int currentLineIndex = 0;
        private bool isPlaying = false;

        public void PlayCutscene()
        {
            if (!isPlaying && lines.Count > 0)
            {
                isPlaying = true;
                currentLineIndex = 0;
                StartCoroutine(PlaySequence());
            }
        }

        private System.Collections.IEnumerator PlaySequence()
        {
            foreach (var line in lines)
            {
                DialogueManager.Instance.OnDialogueTextUpdate?.Invoke(line.speakerName, line.text);
                yield return new WaitForSeconds(line.duration);
            }
            isPlaying = false;
        }
    }

    /// <summary>
    /// Parses simple dialogue markup language for dynamic text.
    /// Supports: {var:name}, [color:red]text[/color], [pause:2], [speed:fast]
    /// </summary>
    public static class DialogueParser
    {
        public static string ParseMarkup(string text)
        {
            // Replace variable references
            text = Regex.Replace(text, @"\{var:(\w+)\}", match =>
            {
                string varName = match.Groups[1].Value;
                return DialogueManager.Instance.GetVariable(varName).ToString();
            });

            // Remove color tags for plain text (UI handles colors separately)
            text = Regex.Replace(text, @"\[color:\w+\](.*?)\[/color\]", "$1");
            
            // Remove control tags
            text = Regex.Replace(text, @"\[pause:\d+\]", "");
            text = Regex.Replace(text, @"\[speed:\w+\]", "");
            
            return text;
        }

        public static float ParseSpeed(string text)
        {
            Match match = Regex.Match(text, @"\[speed:(\w+)\]");
            if (match.Success)
            {
                string speed = match.Groups[1].Value.ToLower();
                switch (speed)
                {
                    case "fast": return 0.02f;
                    case "slow": return 0.1f;
                    default: return 0.05f;
                }
            }
            return 0.05f;
        }
    }

    /// <summary>
    /// Manages dialogue save/load for persistence across game sessions.
    /// </summary>
    [Serializable]
    public class DialogueSaveData
    {
        public Dictionary<string, int> variables = new Dictionary<string, int>();
        public Dictionary<string, int> relationships = new Dictionary<string, int>();
        public Dictionary<string, List<string>> choiceHistory = new Dictionary<string, List<string>>();
        public List<string> completedDialogues = new List<string>();
    }

    public static class DialogueSaveSystem
    {
        public static DialogueSaveData SaveDialogueState()
        {
            return new DialogueSaveData
            {
                // Save implementation depends on save system
            };
        }

        public static void LoadDialogueState(DialogueSaveData data)
        {
            if (data == null) return;
            // Load implementation
        }
    }

    /// <summary>
    /// Localization support for dialogue text in multiple languages.
    /// </summary>
    public class DialogueLocalizer
    {
        private static Dictionary<string, Dictionary<string, string>> localizedText = 
            new Dictionary<string, Dictionary<string, string>>();
        private static string currentLanguage = "en";

        public static void LoadLanguage(string languageCode)
        {
            currentLanguage = languageCode;
            // Load localized text from resources or files
        }

        public static string GetLocalizedText(string textKey)
        {
            if (localizedText.ContainsKey(currentLanguage) && 
                localizedText[currentLanguage].ContainsKey(textKey))
            {
                return localizedText[currentLanguage][textKey];
            }
            return textKey; // Fallback to key if not found
        }
    }

    /// <summary>
    /// Debug viewer for visualizing dialogue trees in editor.
    /// </summary>
    #if UNITY_EDITOR
    public class DialogueTreeDebugger
    {
        public static void VisualizeTree(DialogueTree tree)
        {
            Debug.Log($"=== Dialogue Tree: {tree.dialogueName} ===");
            Debug.Log($"Start Node: {tree.startNodeId}");
            Debug.Log($"Total Nodes: {tree.nodes.Count}");
            
            foreach (var node in tree.nodes)
            {
                Debug.Log($"  Node [{node.nodeId}]: {node.speakerName}");
                Debug.Log($"    Text: {node.dialogueText.Substring(0, Mathf.Min(50, node.dialogueText.Length))}...");
                Debug.Log($"    Choices: {node.choices.Count}");
                foreach (var choice in node.choices)
                {
                    Debug.Log($"      -> {choice.choiceText} (to: {choice.targetNodeId})");
                }
            }
        }
    }
    #endif
}