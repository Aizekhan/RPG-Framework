using System;
using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Serializable tracking data for an active dialogue conversation.
    /// Includes state, visited nodes, and dialogue history.
    /// </summary>
    [Serializable]
    public struct DialogueProgressData
    {
        /// <summary>
        /// The ID of the dialogue being played.
        /// </summary>
        public string DialogueId;

        /// <summary>
        /// The ID of the entity participating in the dialogue.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// Current state of the dialogue (NotStarted, Active, Completed, Interrupted).
        /// </summary>
        public DialogueState State;

        /// <summary>
        /// ID of the currently active node.
        /// </summary>
        public string CurrentNodeId;

        /// <summary>
        /// IDs of all nodes that have been visited during this dialogue.
        /// </summary>
        public List<string> VisitedNodeIds;

        /// <summary>
        /// When the dialogue started.
        /// </summary>
        public DateTime StartTime;

        /// <summary>
        /// When the dialogue ended (null if still active).
        /// </summary>
        public DateTime? EndTime;

        /// <summary>
        /// Number of times this dialogue has been completed by this entity.
        /// </summary>
        public int CompletionCount;
    }
}
