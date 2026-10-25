namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Type of condition for dialogue branching.
    /// </summary>
    public enum ConditionType
    {
        /// <summary>
        /// Check if a quest has been completed.
        /// ParameterId = quest ID, RequiredValue = 1 (completed)
        /// </summary>
        QuestComplete,

        /// <summary>
        /// Check if a stat meets a threshold.
        /// ParameterId = stat name, RequiredValue = minimum value
        /// </summary>
        StatCheck,

        /// <summary>
        /// Check if player owns a specific item.
        /// ParameterId = item ID, RequiredValue = minimum quantity
        /// </summary>
        ItemOwned,

        /// <summary>
        /// Check a custom dialogue variable.
        /// ParameterId = variable name, RequiredValue = expected value
        /// </summary>
        VariableCheck,

        /// <summary>
        /// Custom condition evaluated by game logic.
        /// ParameterId = custom condition ID
        /// </summary>
        Custom
    }
}
