namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueCondition.
    /// Represents a condition that can be evaluated to determine dialogue flow.
    /// Used for conditional nodes and choice availability.
    /// </summary>
    public class DialogueCondition : IDialogueCondition
    {
        public ConditionType Type { get; }
        public string ParameterId { get; }
        public int RequiredValue { get; }

        /// <summary>
        /// Create a DialogueCondition from DialogueConditionData.
        /// </summary>
        public DialogueCondition(DialogueConditionData data)
        {
            Type = data.Type;
            ParameterId = data.ParameterId;
            RequiredValue = data.RequiredValue;
        }

        /// <summary>
        /// Create a DialogueCondition directly with parameters.
        /// </summary>
        public DialogueCondition(
            ConditionType type,
            string parameterId,
            int requiredValue)
        {
            Type = type;
            ParameterId = parameterId;
            RequiredValue = requiredValue;
        }

        /// <summary>
        /// Factory method to create a DialogueCondition from DialogueConditionData.
        /// </summary>
        public static DialogueCondition FromData(DialogueConditionData data)
        {
            return new DialogueCondition(data);
        }

        /// <summary>
        /// Evaluate this condition against the provided context.
        /// </summary>
        public bool Evaluate(IDialogueContext context)
        {
            if (context == null)
                return false;

            switch (Type)
            {
                case ConditionType.QuestComplete:
                    return context.HasCompletedQuest(ParameterId);

                case ConditionType.StatCheck:
                    return context.GetStatValue(ParameterId) >= RequiredValue;

                case ConditionType.ItemOwned:
                    return context.HasItem(ParameterId);

                case ConditionType.VariableCheck:
                    return context.Variables.TryGetValue(ParameterId, out var value) && value == RequiredValue;

                case ConditionType.Custom:
                    // Custom conditions always evaluate to false unless overridden by game logic
                    return false;

                default:
                    return false;
            }
        }
    }
}
