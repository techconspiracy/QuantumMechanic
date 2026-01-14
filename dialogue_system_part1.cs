using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumMechanic.Dialogue
{
    #region Enums
    
    /// <summary>
    /// Current state of the dialogue system
    /// </summary>
    public enum DialogueState
    {
        Inactive,
        Speaking,
        WaitingForChoice,
        Processing,
        Completed
    }
    
    /// <summary>
    /// Type of speaker in dialogue
    /// </summary>
    public enum SpeakerType
    {
        Player,
        NPC,
        System,
        Narrator
    }
    
    /// <summary>
    /// Requirement types for dialogue choices
    /// </summary>
    public enum ChoiceRequirement
    {
        None,
        QuestComplete,
        QuestActive,
        ItemInInventory,
        StatMinimum,
        VariableEquals,
        VariableGreaterThan
    }
    
    /// <summary>
    /// Emotion states for character portraits
    /// </summary>
    public enum EmotionState
    {
        Neutral,
        Happy,
        Sad,
        Angry,
        Surprised,
        Worried,
        Excited
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Single line of dialogue
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        public string speakerName;
        public SpeakerType speakerType;
        public string text;
        public EmotionState emotion;
        public float displayDuration;
        public AudioClip voiceClip;
        
        public DialogueLine(string speaker, string dialogueText, 
                          SpeakerType type = SpeakerType.NPC,
                          EmotionState emotionState = EmotionState.Neutral)
        {
            speakerName = speaker;
            text = dialogueText;
            speakerType = type;
            emotion = emotionState;
            displayDuration = 0f; // Auto-calculate
        }
    }
    
    /// <summary>
    /// Player choice in dialogue
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        public string choiceText;
        public string nextNodeID;
        public ChoiceRequirement requirement;
        public string requirementValue;
        public int requirementAmount;
        public bool isEnabled;
        
        public DialogueChoice(string text, string nextNode)
        {
            choiceText = text;
            nextNodeID = nextNode;
            requirement = ChoiceRequirement.None;
            isEnabled = true;
        }
    }
    
    /// <summary>
    /// Node in dialogue tree
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public string nodeID;
        public List<DialogueLine> lines;
        public List<DialogueChoice> choices;
        public string autoNextNodeID;
        public bool endDialogue;
        public Action onNodeEnter;
        public Action onNodeExit;
        
        public DialogueNode(string id)
        {
            nodeID = id;
            lines = new List<DialogueLine>();
            choices = new List<DialogueChoice>();
            endDialogue = false;
        }
    }
    
    /// <summary>
    /// Complete dialogue tree
    /// </summary>
    [Serializable]
    public class DialogueTree
    {
        public string treeID;
        public string treeName;
        public Dictionary<string, DialogueNode> nodes;
        public string startNodeID;
        
        public DialogueTree(string id, string name)
        {
            treeID = id;
            treeName = name;
            nodes = new Dictionary<string, DialogueNode>();
            startNodeID = "start";
        }
    }
    
    #endregion
    
    /// <summary>
    /// Manages NPC dialogue trees, branching conversations, and player choices
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        #region Singleton
        
        private static DialogueSystem instance;
        public static DialogueSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<DialogueSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("DialogueSystem");
                        instance = go.AddComponent<DialogueSystem>();
                    }
                }
                return instance;
            }
        }
        
        #endregion