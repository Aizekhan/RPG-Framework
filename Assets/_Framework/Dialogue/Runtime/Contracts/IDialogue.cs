using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Represents a dialogue definition with nodes, choices, and branching structure.
    /// </summary>
    public interface IDialogue
    {
        /// <summary>
        /// Unique identifier for this dialogue.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display title of the dialogue.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Full description of the dialogue.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// ID of the root node where the dialogue begins.
        /// </summary>
        string RootNodeId { get; }

        /// <summary>
        /// All nodes in this dialogue.
        /// </summary>
        IReadOnlyList<IDialogueNode> Nodes { get; }

        /// <summary>
        /// Get a specific node by ID.
        /// </summary>
        /// <param name="nodeId">The ID of the node to retrieve.</param>
        /// <returns>The dialogue node, or null if not found.</returns>
        IDialogueNode? GetNode(string nodeId);
    }
}
