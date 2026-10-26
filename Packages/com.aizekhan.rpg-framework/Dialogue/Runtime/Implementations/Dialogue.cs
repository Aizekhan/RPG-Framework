using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// Implementation of IDialogue.
    /// Represents a complete dialogue definition with nodes and branching structure.
    /// </summary>
    public class Dialogue : IDialogue
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string RootNodeId { get; }
        public IReadOnlyList<IDialogueNode> Nodes { get; }

        private readonly Dictionary<string, IDialogueNode> _nodeDict;

        /// <summary>
        /// Create a Dialogue from DialogueData.
        /// </summary>
        public Dialogue(DialogueData data)
        {
            Id = data.Id;
            Title = data.Title;
            Description = data.Description;
            RootNodeId = data.RootNodeId;

            var nodes = new List<DialogueNode>();
            if (data.Nodes != null)
            {
                foreach (var nodeData in data.Nodes)
                {
                    nodes.Add(new DialogueNode(nodeData));
                }
            }

            Nodes = nodes.AsReadOnly();
            _nodeDict = Nodes.ToDictionary(n => n.Id);
        }

        /// <summary>
        /// Create a Dialogue directly with parameters.
        /// </summary>
        public Dialogue(
            string id,
            string title,
            string description,
            string rootNodeId,
            IReadOnlyList<IDialogueNode> nodes)
        {
            Id = id;
            Title = title;
            Description = description;
            RootNodeId = rootNodeId;
            Nodes = nodes ?? new List<IDialogueNode>();
            _nodeDict = Nodes.ToDictionary(n => n.Id);
        }

        /// <summary>
        /// Factory method to create a Dialogue from DialogueData.
        /// </summary>
        public static Dialogue FromData(DialogueData data)
        {
            return new Dialogue(data);
        }

        /// <summary>
        /// Get a specific node by ID.
        /// </summary>
        public IDialogueNode? GetNode(string nodeId)
        {
            return _nodeDict.TryGetValue(nodeId, out var node) ? node : null;
        }
    }
}
