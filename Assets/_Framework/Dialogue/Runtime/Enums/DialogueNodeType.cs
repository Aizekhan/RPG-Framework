namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Type of dialogue node in conversation tree.
    /// </summary>
    public enum DialogueNodeType
    {
        /// <summary>
        /// Single line spoken by an NPC or player.
        /// Automatically advances to next node.
        /// </summary>
        Line,

        /// <summary>
        /// Player choice node with multiple options.
        /// Requires player input to select a choice.
        /// </summary>
        Choice,

        /// <summary>
        /// Conditional branch node that evaluates conditions.
        /// Redirects conversation based on quest/stat/item checks.
        /// </summary>
        Branch,

        /// <summary>
        /// End of dialogue tree.
        /// Terminates the conversation.
        /// </summary>
        End
    }
}
