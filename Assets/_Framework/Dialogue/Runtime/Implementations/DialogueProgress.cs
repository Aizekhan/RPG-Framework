using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueProgress.
    /// Tracks the progress of an active dialogue conversation.
    /// Includes state, visited nodes, variables, and dialogue history.
    /// </summary>
    public class DialogueProgress : IDialogueProgress
    {
        public string DialogueId { get; }
        public int EntityId { get; }
        public DialogueState State { get; private set; }
        public string CurrentNodeId { get; private set; }
        public IReadOnlyList<string> VisitedNodeIds => _visitedNodeIds.AsReadOnly();
        public DateTime StartTime { get; }
        public DateTime? EndTime { get; private set; }
        public int CompletionCount { get; private set; }

        private readonly List<string> _visitedNodeIds;
        private readonly Dictionary<string, int> _variables;

        /// <summary>
        /// Create a new DialogueProgress for starting a dialogue.
        /// </summary>
        public DialogueProgress(string dialogueId, int entityId, string rootNodeId)
        {
            DialogueId = dialogueId;
            EntityId = entityId;
            State = DialogueState.Active;
            CurrentNodeId = rootNodeId;
            StartTime = DateTime.UtcNow;
            EndTime = null;
            CompletionCount = 0;
            _visitedNodeIds = new List<string> { rootNodeId };
            _variables = new Dictionary<string, int>();
        }

        /// <summary>
        /// Create a DialogueProgress from DialogueProgressData (deserialization).
        /// </summary>
        public DialogueProgress(DialogueProgressData data)
        {
            DialogueId = data.DialogueId;
            EntityId = data.EntityId;
            State = data.State;
            CurrentNodeId = data.CurrentNodeId;
            StartTime = data.StartTime;
            EndTime = data.EndTime;
            CompletionCount = data.CompletionCount;
            _visitedNodeIds = data.VisitedNodeIds ?? new List<string>();
            _variables = new Dictionary<string, int>();
        }

        /// <summary>
        /// Set the current node.
        /// </summary>
        public void SetCurrentNode(string nodeId)
        {
            CurrentNodeId = nodeId;
            AddVisitedNode(nodeId);
        }

        /// <summary>
        /// Add a visited node (only adds if not already visited).
        /// </summary>
        public void AddVisitedNode(string nodeId)
        {
            if (!_visitedNodeIds.Contains(nodeId))
            {
                _visitedNodeIds.Add(nodeId);
            }
        }

        /// <summary>
        /// Set a dialogue variable.
        /// </summary>
        public void SetVariable(string name, int value)
        {
            _variables[name] = value;
        }

        /// <summary>
        /// Get a dialogue variable value.
        /// </summary>
        public int GetVariable(string name)
        {
            return _variables.TryGetValue(name, out var value) ? value : 0;
        }

        /// <summary>
        /// Mark the dialogue as completed.
        /// </summary>
        public void Complete()
        {
            State = DialogueState.Completed;
            EndTime = DateTime.UtcNow;
            CompletionCount++;
        }

        /// <summary>
        /// Mark the dialogue as interrupted.
        /// </summary>
        public void Interrupt()
        {
            State = DialogueState.Interrupted;
            EndTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Convert to DialogueProgressData for storage.
        /// </summary>
        public DialogueProgressData ToData()
        {
            return new DialogueProgressData
            {
                DialogueId = DialogueId,
                EntityId = EntityId,
                State = State,
                CurrentNodeId = CurrentNodeId,
                VisitedNodeIds = new List<string>(_visitedNodeIds),
                StartTime = StartTime,
                EndTime = EndTime,
                CompletionCount = CompletionCount
            };
        }

        /// <summary>
        /// Factory method to create DialogueProgress from DialogueProgressData.
        /// </summary>
        public static DialogueProgress FromData(DialogueProgressData data)
        {
            return new DialogueProgress(data);
        }
    }
}
