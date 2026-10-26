using System;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Serializable dialogue choice available to the player.
    /// </summary>
    [Serializable]
    public struct DialogueChoiceData
    {
        /// <summary>
        /// Unique identifier for this choice within its node.
        /// </summary>
        public string Id;

        /// <summary>
        /// Text of the choice to display to the player.
        /// </summary>
        public string Text;

        /// <summary>
        /// ID of the target node to proceed to if this choice is selected.
        /// </summary>
        public string TargetNodeId;

        /// <summary>
        /// Optional condition that must be met for this choice to be available.
        /// </summary>
        public DialogueConditionData? Condition;
    }
}
