using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogueNode.
    /// Represents a single node in a dialogue tree (line, choice, branch, or end).
    /// </summary>
    public class DialogueNode : IDialogueNode
    {
        public string Id { get; }
        public DialogueNodeType NodeType { get; }
        public string SpeakerId { get; }
        public string Text { get; }
        public IReadOnlyList<string> NextNodeIds { get; }
        public IReadOnlyList<IDialogueChoice> Choices { get; }
        public IDialogueCondition? Condition { get; }

        /// <summary>
        /// Create a DialogueNode from DialogueNodeData.
        /// </summary>
        public DialogueNode(DialogueNodeData data)
        {
            Id = data.Id;
            NodeType = data.NodeType;
            SpeakerId = data.SpeakerId;
            Text = data.Text;
            NextNodeIds = data.NextNodeIds ?? new string[0];

            var choices = new List<DialogueChoice>();
            if (data.Choices != null)
            {
                foreach (var choiceData in data.Choices)
                {
                    choices.Add(new DialogueChoice(choiceData));
                }
            }

            Choices = choices.AsReadOnly();
            Condition = data.Condition.HasValue ? new DialogueCondition(data.Condition.Value) : null;
        }

        /// <summary>
        /// Create a DialogueNode directly with parameters.
        /// </summary>
        public DialogueNode(
            string id,
            DialogueNodeType nodeType,
            string speakerId,
            string text,
            IReadOnlyList<string> nextNodeIds,
            IReadOnlyList<IDialogueChoice> choices,
            IDialogueCondition? condition = null)
        {
            Id = id;
            NodeType = nodeType;
            SpeakerId = speakerId;
            Text = text;
            NextNodeIds = nextNodeIds ?? new List<string>();
            Choices = choices ?? new List<IDialogueChoice>();
            Condition = condition;
        }

        /// <summary>
        /// Factory method to create a DialogueNode from DialogueNodeData.
        /// </summary>
        public static DialogueNode FromData(DialogueNodeData data)
        {
            return new DialogueNode(data);
        }
    }
}
