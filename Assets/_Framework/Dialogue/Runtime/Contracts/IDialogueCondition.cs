namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Represents a condition that can be evaluated to determine dialogue flow.
    /// Used for conditional nodes and choice availability.
    /// </summary>
    public interface IDialogueCondition
    {
        /// <summary>
        /// Type of condition (QuestComplete, StatCheck, ItemOwned, VariableCheck, Custom).
        /// </summary>
        ConditionType Type { get; }

        /// <summary>
        /// Parameter ID for the condition (quest ID, stat name, item ID, variable name, etc.).
        /// </summary>
        string ParameterId { get; }

        /// <summary>
        /// Required value for comparison (quest state, stat value, quantity, etc.).
        /// </summary>
        int RequiredValue { get; }

        /// <summary>
        /// Evaluate this condition against the provided context.
        /// </summary>
        /// <param name="context">The dialogue context to evaluate against.</param>
        /// <returns>True if the condition is met, false otherwise.</returns>
        bool Evaluate(IDialogueContext context);
    }
}
