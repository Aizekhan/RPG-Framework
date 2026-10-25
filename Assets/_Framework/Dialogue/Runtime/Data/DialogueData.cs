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
    }
}
