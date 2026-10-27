using System;
using RPGFramework.Events;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Event emitted when a dialogue is initiated.
    /// </summary>
    public struct DialogueStartedEvent : IEvent
    {
        /// <summary>
        /// The entity that started the dialogue.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the dialogue that started.
        /// </summary>
        public string DialogueId;

        /// <summary>
        /// Timestamp when the dialogue started.
        /// </summary>
        public DateTime StartedAt;
    }
}
