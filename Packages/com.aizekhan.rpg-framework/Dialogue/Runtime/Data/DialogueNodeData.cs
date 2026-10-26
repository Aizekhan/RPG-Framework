using System;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Serializable dialogue node (line, choice, branch, or end).
    /// </summary>
    [Serializable]
    public struct DialogueNodeData
    {
        /// <summary>
        /// Unique identifier for this node within its dialogue.
        /// </summary>
        public string Id;

        /// <summary>
        /// Type of this node (Line, Choice, Branch, End).
        /// </summary>
        public DialogueNodeType NodeType;

        /// <summary>
        /// ID of the speaker for this node (NPC, Player, Narrator, etc.).
        /// </summary>
        public string SpeakerId;

        /// <summary>
        /// Text content to display for this node.
        /// </summary>
        public string Text;

        /// <summary>
        /// IDs of the next nodes to proceed to (usually one for lines, multiple for branches).
        /// </summary>
        public string[] NextNodeIds;

        /// <summary>
        /// Player choices available at this node (empty for non-choice nodes).
        /// </summary>
        public DialogueChoiceData[] Choices;

        /// <summary>
        /// Condition that must be met to display this node (optional).
        /// </summary>
        public DialogueConditionData? Condition;
    }
}
