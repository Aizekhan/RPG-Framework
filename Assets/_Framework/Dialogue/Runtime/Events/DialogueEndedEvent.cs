using System;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Event emitted when a dialogue is concluded.
    /// </summary>
    public struct DialogueEndedEvent
    {
        /// <summary>
        /// The entity that ended the dialogue.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the dialogue that ended.
        /// </summary>
        public string DialogueId;

        /// <summary>
        /// The final state of the dialogue (Completed or Interrupted).
        /// </summary>
        public DialogueState EndState;

        /// <summary>
        /// Timestamp when the dialogue ended.
        /// </summary>
        public DateTime EndedAt;
    }
}
