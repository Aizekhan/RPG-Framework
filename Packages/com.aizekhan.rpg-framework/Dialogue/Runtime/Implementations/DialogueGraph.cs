using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Dialogue
{
    public enum DialogueIssueLevel
    {
        Warning,
        Error
    }

    /// <summary>A problem in a conversation's data (<see cref="DialogueGraph"/>).</summary>
    public readonly struct DialogueIssue
    {
        public DialogueIssue(DialogueIssueLevel level, string code, string dialogueId, string nodeId, string message)
        {
            Level = level;
            Code = code;
            DialogueId = dialogueId;
            NodeId = nodeId;
            Message = message;
        }

        public DialogueIssueLevel Level { get; }

        /// <summary>
        /// noNodes, dupNode, badStart, startIsEnd, badLink, noChoices, unreachable, deadEnd, noWayOut, emptyText,
        /// speakerNotIn, dupChoice, missingQuest, missingStage, dupDialogue.
        /// </summary>
        public string Code { get; }

        public string DialogueId { get; }

        /// <summary>The node it is about; null for the dialogue itself.</summary>
        public string NodeId { get; }

        public string Message { get; }

        public override string ToString() => $"[{Level}] {DialogueId}{(NodeId != null ? "/" + NodeId : "")}: {Message} ({Code})";
    }

    /// <summary>
    /// Checks conversations: a start that exists and is not an end, links (next nodes, answers) to nodes that exist,
    /// no node that cannot be reached, no line that leads nowhere without being an End, no loop the player can never
    /// leave, lines with text, speakers among the participants, and the quest and stage it belongs to.
    /// </summary>
    public static class DialogueGraph
    {
        /// <param name="questExists">Whether a quest id is known (null = not checked).</param>
        /// <param name="stageExists">Whether a quest has a stage (quest, stage; null = not checked).</param>
        public static List<DialogueIssue> Check(IDialogue dialogue, Func<string, bool> questExists = null,
            Func<string, string, bool> stageExists = null)
        {
            var issues = new List<DialogueIssue>();
            if (dialogue == null)
                return issues;
            string id = dialogue.Id;
            if (dialogue.Nodes.Count == 0)
            {
                issues.Add(Error("noNodes", id, null, "has no nodes"));
                return issues;
            }

            var nodes = new Dictionary<string, IDialogueNode>();
            foreach (var node in dialogue.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id) || nodes.ContainsKey(node.Id))
                    issues.Add(Error("dupNode", id, node.Id, string.IsNullOrWhiteSpace(node.Id) ? "a node has no id" : "two nodes have this id"));
                else
                    nodes[node.Id] = node;
            }

            var details = dialogue as IDialogueDetails;
            var participants = details?.ParticipantIds ?? (IReadOnlyList<string>)Array.Empty<string>();
            foreach (var node in nodes.Values)
            {
                foreach (var to in Targets(node))
                {
                    if (string.IsNullOrEmpty(to) || !nodes.ContainsKey(to))
                        issues.Add(Error("badLink", id, node.Id, $"leads to node '{to}', which does not exist"));
                }
                if (node.NodeType == DialogueNodeType.Choice && node.Choices.Count == 0)
                    issues.Add(Error("noChoices", id, node.Id, "is a choice with no answers"));
                if (node.NodeType == DialogueNodeType.Choice && node.Choices.Count > 0 && node.Choices.All(c => c.Condition != null))
                    issues.Add(Warning("deadEnd", id, node.Id, "every answer has a condition: when none passes the player is stuck"));
                if (node.NodeType == DialogueNodeType.Branch && node.NextNodeIds.Count == 0)
                    issues.Add(Warning("deadEnd", id, node.Id, "is a branch that leads nowhere (a line with no way on ends the conversation)"));
                if ((node.NodeType == DialogueNodeType.Line || node.NodeType == DialogueNodeType.Choice) && string.IsNullOrWhiteSpace(node.Text))
                    issues.Add(Warning("emptyText", id, node.Id, "has no text"));
                foreach (var choice in node.Choices.Where(c => string.IsNullOrWhiteSpace(c.Text)))
                    issues.Add(Warning("emptyText", id, node.Id, $"answer '{choice.Id}' has no text"));
                foreach (var duplicate in node.Choices.GroupBy(c => c.Id).Where(g => g.Count() > 1))
                    issues.Add(Error("dupChoice", id, node.Id, $"two answers have the id '{duplicate.Key}'"));
                if (participants.Count > 0 && !string.IsNullOrEmpty(node.SpeakerId) && !IsNeutralSpeaker(node.SpeakerId) &&
                    !participants.Contains(node.SpeakerId))
                    issues.Add(Warning("speakerNotIn", id, node.Id, $"is spoken by '{node.SpeakerId}', who is not a participant"));
            }

            if (details != null && !string.IsNullOrEmpty(details.QuestId) && questExists != null)
            {
                if (!questExists(details.QuestId))
                    issues.Add(Error("missingQuest", id, null, $"belongs to quest '{details.QuestId}', which does not exist"));
                else if (!string.IsNullOrEmpty(details.QuestStageId) && stageExists != null && !stageExists(details.QuestId, details.QuestStageId))
                    issues.Add(Error("missingStage", id, null, $"belongs to stage '{details.QuestStageId}', which quest '{details.QuestId}' does not have"));
            }

            string start = dialogue.RootNodeId;
            if (string.IsNullOrEmpty(start) || !nodes.ContainsKey(start))
            {
                issues.Add(Error("badStart", id, start, $"starts at node '{start}', which does not exist"));
                return issues;
            }
            if (nodes[start].NodeType == DialogueNodeType.End)
                issues.Add(Error("startIsEnd", id, start, "starts at an End node: the conversation stops before it begins"));

            var reachable = Walk(start, n => Targets(nodes[n]).Where(nodes.ContainsKey));
            foreach (var node in nodes.Keys.Where(n => !reachable.Contains(n)))
                issues.Add(Warning("unreachable", id, node, "cannot be reached from the start"));

            // Reachable nodes from which no End and no line without a way on can be reached: a loop with no exit
            var reverse = nodes.Keys.ToDictionary(n => n, n => new List<string>());
            foreach (var node in nodes.Values)
            {
                foreach (var to in Targets(node).Where(nodes.ContainsKey))
                    reverse[to].Add(node.Id);
            }
            var exits = nodes.Values.Where(n => n.NodeType == DialogueNodeType.End || !Targets(n).Any()).Select(n => n.Id);
            var canLeave = new HashSet<string>();
            foreach (var exit in exits)
                canLeave.UnionWith(Walk(exit, n => reverse[n]));
            foreach (var node in reachable.Where(n => !canLeave.Contains(n)))
                issues.Add(Warning("noWayOut", id, node, "is in a loop that never reaches an end"));
            return issues;
        }

        /// <summary><see cref="Check(IDialogue, Func{string, bool}, Func{string, string, bool})"/> for every dialogue, plus ids used twice.</summary>
        public static List<DialogueIssue> Check(IEnumerable<IDialogue> dialogues, Func<string, bool> questExists = null,
            Func<string, string, bool> stageExists = null)
        {
            var all = dialogues.Where(d => d != null).ToList();
            var issues = all.SelectMany(d => Check(d, questExists, stageExists)).ToList();
            foreach (var duplicate in all.GroupBy(d => d.Id).Where(g => g.Count() > 1))
                issues.Add(Error("dupDialogue", duplicate.Key, null, "two dialogues have this id"));
            return issues;
        }

        // The player and a narrator speak in any conversation
        private static bool IsNeutralSpeaker(string speaker) =>
            string.Equals(speaker, "player", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(speaker, "narrator", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> Targets(IDialogueNode node) =>
            node.NextNodeIds.Concat(node.Choices.Select(c => c.TargetNodeId));

        private static HashSet<string> Walk(string from, Func<string, IEnumerable<string>> next)
        {
            var seen = new HashSet<string> { from };
            var open = new Stack<string>();
            open.Push(from);
            while (open.Count > 0)
            {
                foreach (var to in next(open.Pop()))
                {
                    if (seen.Add(to))
                        open.Push(to);
                }
            }
            return seen;
        }

        private static DialogueIssue Error(string code, string dialogue, string node, string message) =>
            new DialogueIssue(DialogueIssueLevel.Error, code, dialogue, node, message);

        private static DialogueIssue Warning(string code, string dialogue, string node, string message) =>
            new DialogueIssue(DialogueIssueLevel.Warning, code, dialogue, node, message);
    }
}
