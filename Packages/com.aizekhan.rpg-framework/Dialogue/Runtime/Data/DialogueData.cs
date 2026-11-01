using System;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Serializable dialogue definition with nodes and structure.
    /// </summary>
    [Serializable]
    public struct DialogueData
    {
        /// <summary>
        /// Unique identifier for this dialogue.
        /// </summary>
        public string Id;

        /// <summary>
        /// Display title of the dialogue.
        /// </summary>
        public string Title;

        /// <summary>
        /// Full description of the dialogue.
        /// </summary>
        public string Description;

        /// <summary>
        /// ID of the root node where the dialogue begins.
        /// </summary>
        public string RootNodeId;

        /// <summary>
        /// All nodes in this dialogue.
        /// </summary>
        public DialogueNodeData[] Nodes;

        /// <summary>The game's own kind of dialogue ("quest", "ambient", "trader", "bark"...); free text, optional.</summary>
        public string Type;

        /// <summary>The quest this conversation belongs to; empty for none.</summary>
        public string QuestId;

        /// <summary>The quest stage it belongs to (<see cref="QuestId"/>); empty = any stage.</summary>
        public string QuestStageId;

        /// <summary>The speakers expected in it (character ids); empty = anyone.</summary>
        public string[] ParticipantIds;
    }
}
