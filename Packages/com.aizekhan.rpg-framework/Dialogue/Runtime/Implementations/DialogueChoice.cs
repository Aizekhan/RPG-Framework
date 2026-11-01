namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueChoice.
    /// Represents a choice available to the player in a dialogue node.
    /// </summary>
    public class DialogueChoice : IDialogueChoice, ILocalizedLine
    {
        public string Id { get; }
        public string Text { get; }
        public string TargetNodeId { get; }
        public IDialogueCondition? Condition { get; }
        public bool IsEnabled { get; }
        public string TextKey { get; } = "";

        /// <summary>
        /// Create a DialogueChoice from DialogueChoiceData.
        /// </summary>
        public DialogueChoice(DialogueChoiceData data)
        {
            Id = data.Id;
            Text = data.Text;
            TextKey = data.TextKey ?? "";
            TargetNodeId = data.TargetNodeId;
            Condition = data.Condition.HasValue ? new DialogueCondition(data.Condition.Value) : null;
            IsEnabled = true;
        }

        /// <summary>
        /// Create a DialogueChoice directly with parameters.
        /// </summary>
        public DialogueChoice(
            string id,
            string text,
            string targetNodeId,
            IDialogueCondition? condition = null,
            bool isEnabled = true)
        {
            Id = id;
            Text = text;
            TargetNodeId = targetNodeId;
            Condition = condition;
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// Factory method to create a DialogueChoice from DialogueChoiceData.
        /// </summary>
        public static DialogueChoice FromData(DialogueChoiceData data)
        {
            return new DialogueChoice(data);
        }
    }
}
