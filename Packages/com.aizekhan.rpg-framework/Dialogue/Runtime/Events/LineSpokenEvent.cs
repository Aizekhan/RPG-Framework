using System;
using RPGFramework.Events;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Event emitted when a dialogue line is displayed.
    /// </summary>
    public struct LineSpokenEvent : IEvent
    {
        /// <summary>
        /// The entity engaged in the dialogue.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the dialogue being played.
        /// </summary>
        public string DialogueId;

        /// <summary>
        /// The ID of the node being displayed.
        /// </summary>
        public string NodeId;

        /// <summary>
        /// The ID of the speaker.
        /// </summary>
        public string SpeakerId;

        /// <summary>
        /// The text of the line being displayed.
        /// </summary>
        public string Text;

        /// <summary>
        /// Timestamp when the line was spoken.
        /// </summary>
        public DateTime SpokenAt;
    }
}
