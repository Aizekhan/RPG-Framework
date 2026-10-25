using System;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Event emitted when a player selects a dialogue choice.
    /// </summary>
    public struct ChoiceSelectedEvent
    {
        /// <summary>
        /// The entity that made the choice.
        /// </summary>
        public int EntityId;

        /// <summary>
        /// The ID of the dialogue in which the choice was made.
        /// </summary>
        public string DialogueId;

        /// <summary>
        /// The ID of the node from which the choice was made.
        /// </summary>
        public string NodeId;

        /// <summary>
        /// The ID of the choice that was selected.
        /// </summary>
        public string ChoiceId;

        /// <summary>
        /// The text of the selected choice.
        /// </summary>
        public string ChoiceText;

        /// <summary>
        /// The ID of the target node that the choice leads to.
        /// </summary>
        public string TargetNodeId;

        /// <summary>
        /// Timestamp when the choice was selected.
        /// </summary>
        public DateTime SelectedAt;
    }
}
