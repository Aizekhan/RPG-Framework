using System;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Serializable condition for dialogue branching and choice availability.
    /// </summary>
    [Serializable]
    public struct DialogueConditionData
    {
        /// <summary>
        /// Type of condition (QuestComplete, StatCheck, ItemOwned, VariableCheck, Custom).
        /// </summary>
        public ConditionType Type;

        /// <summary>
        /// Parameter ID for the condition (quest ID, stat name, item ID, variable name, etc.).
        /// </summary>
        public string ParameterId;

        /// <summary>
        /// Required value for comparison (quest state, stat value, quantity, etc.).
        /// </summary>
        public int RequiredValue;
    }
}
