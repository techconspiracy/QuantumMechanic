using System;
using System.Collections.Generic;
using UnityEngine;
using QuantumMechanic.Quests;
using QuantumMechanic.Save;

namespace QuantumMechanic.Dialogue
{
    /// <summary>
    /// Represents a single node in a dialogue tree with text, speaker, and branching choices.
    /// </summary>
    [Serializable]
    public class DialogueNode
    {
        public string nodeId;
        public string speakerName;
        public string dialogueText;
        public string portraitId;
        public string voiceClipId;
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public List<DialogueAction> actions = new List<DialogueAction>();
        public float textSpeed = 0.05f;
        public bool canSkip = true;
        public string nextNodeId; // Auto-continue if no choices
        
        /// <summary>
        /// Checks if this node can be displayed based on conditions.
        /// </summary>
        public bool MeetsConditions()
        {
            foreach (var condition in conditions)
            {
                if (!condition.Evaluate()) return false;
            }
            return true;
        }

        /// <summary>
        /// Executes all actions associated with this node.
        /// </summary>
        public void ExecuteActions()
        {
            foreach (var action in actions)
            {
                action.Execute();
            }
        }
    }

    /// <summary>
    /// Represents a player choice in dialogue with conditions and consequences.
    /// </summary>
    [Serializable]
    public class DialogueChoice
    {
        public string choiceText;
        public string targetNodeId;
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public List<DialogueAction> actions = new List<DialogueAction>();
        public int relationshipChange;
        public string skillCheckType; // "strength", "intelligence", etc.
        public int skillCheckDifficulty;
        public bool isBackOption;
        public bool endsDialogue;
        
        /// <summary>
        /// Checks if this choice is available to the player.
        /// </summary>
        public bool IsAvailable()
        {
            foreach (var condition in conditions)
            {
                if (!condition.Evaluate()) return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the display text with skill check indicators.
        /// </summary>
        public string GetDisplayText()
        {
            string text = choiceText;
            if (!string.IsNullOrEmpty(skillCheckType))
            {
                text += $" [{skillCheckType.ToUpper()} {skillCheckDifficulty}]";
            }
            return text;
        }
    }

    /// <summary>
    /// Defines a condition that must be met for dialogue nodes or choices.
    /// </summary>
    [Serializable]
    public class DialogueCondition
    {
        public enum ConditionType { QuestActive, QuestCompleted, HasItem, StatCheck, 
            VariableCheck, RelationshipLevel, TimeOfDay, PlayerLevel }
        
        public ConditionType type;
        public string targetId; // Quest ID, item ID, stat name, variable name, etc.
        public int requiredValue;
        public ComparisonOperator comparison = ComparisonOperator.GreaterOrEqual;
        
        public bool Evaluate()
        {
            switch (type)
            {
                case ConditionType.QuestActive:
                    return QuestManager.Instance.IsQuestActive(targetId);
                case ConditionType.QuestCompleted:
                    return QuestManager.Instance.IsQuestCompleted(targetId);
                case ConditionType.VariableCheck:
                    return DialogueManager.Instance.EvaluateVariable(targetId, requiredValue, comparison);
                case ConditionType.RelationshipLevel:
                    return DialogueManager.Instance.GetRelationship(targetId) >= requiredValue;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// Actions that occur when dialogue nodes are displayed or choices are selected.
    /// </summary>
    [Serializable]
    public class DialogueAction
    {
        public enum ActionType { SetVariable, StartQuest, CompleteQuest, GiveItem, 
            RemoveItem, ChangeRelationship, TriggerEvent, PlaySound }
        
        public ActionType type;
        public string targetId;
        public int value;
        
        public void Execute()
        {
            switch (type)
            {
                case ActionType.SetVariable:
                    DialogueManager.Instance.SetVariable(targetId, value);
                    break;
                case ActionType.StartQuest:
                    QuestManager.Instance.StartQuest(targetId);
                    break;
                case ActionType.CompleteQuest:
                    QuestManager.Instance.CompleteObjective(targetId);
                    break;
                case ActionType.ChangeRelationship:
                    DialogueManager.Instance.ModifyRelationship(targetId, value);
                    break;
            }
        }
    }

    /// <summary>
    /// Comparison operators for dialogue conditions.
    /// </summary>
    public enum ComparisonOperator { Equal, NotEqual, Greater, Less, GreaterOrEqual, LessOrEqual }

    /// <summary>
    /// Represents a complete dialogue conversation tree with multiple nodes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "Quantum Mechanic/Dialogue Tree")]
    public class DialogueTree : ScriptableObject
    {
        public string dialogueId;
        public string dialogueName;
        public string startNodeId;
        public List<DialogueNode> nodes = new List<DialogueNode>();
        
        /// <summary>
        /// Finds a node by its ID.
        /// </summary>
        public DialogueNode GetNode(string nodeId)
        {
            return nodes.Find(n => n.nodeId == nodeId);
        }

        /// <summary>
        /// Gets the starting node of this dialogue tree.
        /// </summary>
        public DialogueNode GetStartNode()
        {
            return GetNode(startNodeId);
        }
    }
}