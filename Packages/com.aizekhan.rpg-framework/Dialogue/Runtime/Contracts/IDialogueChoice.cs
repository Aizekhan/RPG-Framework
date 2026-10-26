namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Represents a choice available to the player in a dialogue node.
    /// </summary>
    public interface IDialogueChoice
    {
        /// <summary>
        /// Unique identifier for this choice within its node.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Text of the choice to display to the player.
        /// </summary>
        string Text { get; }

        /// <summary>
        /// ID of the target node to proceed to if this choice is selected.
        /// </summary>
        string TargetNodeId { get; }

        /// <summary>
        /// Optional condition that must be met for this choice to be available.
        /// </summary>
        IDialogueCondition? Condition { get; }

        /// <summary>
        /// Whether this choice is currently enabled (visible to the player).
        /// </summary>
        bool IsEnabled { get; }
    }
}
