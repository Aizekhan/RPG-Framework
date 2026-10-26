using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Represents a single node in a dialogue tree (line, choice, branch, or end).
    /// </summary>
    public interface IDialogueNode
    {
        /// <summary>
        /// Unique identifier for this node within its dialogue.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Type of this node (Line, Choice, Branch, End).
        /// </summary>
        DialogueNodeType NodeType { get; }

        /// <summary>
        /// ID of the speaker for this node (NPC, Player, Narrator, etc.).
        /// </summary>
        string SpeakerId { get; }

        /// <summary>
        /// Text content to display for this node.
        /// </summary>
        string Text { get; }

        /// <summary>
        /// IDs of the next nodes to proceed to (usually one for lines, multiple for branches).
        /// </summary>
        IReadOnlyList<string> NextNodeIds { get; }

        /// <summary>
        /// Player choices available at this node (empty for non-choice nodes).
        /// </summary>
        IReadOnlyList<IDialogueChoice> Choices { get; }

        /// <summary>
        /// Condition that must be met to display this node (optional).
        /// </summary>
        IDialogueCondition? Condition { get; }
    }
}
