using System;
using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Tracks the progress of an active dialogue conversation.
    /// Includes state, visited nodes, and dialogue history.
    /// </summary>
    public interface IDialogueProgress
    {
        /// <summary>
        /// The ID of the dialogue being played.
        /// </summary>
        string DialogueId { get; }

        /// <summary>
        /// The ID of the entity participating in the dialogue.
        /// </summary>
        int EntityId { get; }

        /// <summary>
        /// Current state of the dialogue (NotStarted, Active, Completed, Interrupted).
        /// </summary>
        DialogueState State { get; }

        /// <summary>
        /// ID of the currently active node.
        /// </summary>
        string CurrentNodeId { get; }

        /// <summary>
        /// IDs of all nodes that have been visited during this dialogue.
        /// </summary>
        IReadOnlyList<string> VisitedNodeIds { get; }

        /// <summary>
        /// When the dialogue started.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// When the dialogue ended (null if still active).
        /// </summary>
        DateTime? EndTime { get; }

        /// <summary>
        /// Number of times this dialogue has been completed by this entity.
        /// </summary>
        int CompletionCount { get; }
    }
}
