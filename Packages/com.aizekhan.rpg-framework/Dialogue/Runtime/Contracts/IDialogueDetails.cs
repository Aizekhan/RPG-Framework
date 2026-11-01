using System;
using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// What a conversation is and where it belongs (<see cref="Dialogue"/> implements it): its kind, the quest and
    /// stage it is part of, the speakers expected in it.
    /// </summary>
    public interface IDialogueDetails
    {
        /// <summary>The game's own kind ("quest", "ambient", "trader", "bark"...); empty when not set.</summary>
        string Type { get; }

        /// <summary>The quest it belongs to; empty for none.</summary>
        string QuestId { get; }

        /// <summary>The stage of <see cref="QuestId"/> it belongs to; empty = any stage.</summary>
        string QuestStageId { get; }

        /// <summary>The speakers expected in it; empty = anyone.</summary>
        IReadOnlyList<string> ParticipantIds { get; }
    }

    /// <summary>A line or an answer with a localization key (<see cref="DialogueNode"/>, <see cref="DialogueChoice"/>).</summary>
    public interface ILocalizedLine
    {
        /// <summary>Empty = the text as written.</summary>
        string TextKey { get; }
    }

    /// <summary>The text to show for a line or an answer, in the player's language when it has a key.</summary>
    public static class DialogueText
    {
        /// <param name="localize">The game's lookup: (key, fallback) → text (e.g. <c>ILocalization.Text</c>).</param>
        public static string Of(IDialogueNode node, Func<string, string, string> localize) =>
            node == null ? "" : Of(node as ILocalizedLine, node.Text, localize);

        public static string Of(IDialogueChoice choice, Func<string, string, string> localize) =>
            choice == null ? "" : Of(choice as ILocalizedLine, choice.Text, localize);

        private static string Of(ILocalizedLine line, string text, Func<string, string, string> localize) =>
            localize != null && line != null && !string.IsNullOrEmpty(line.TextKey) ? localize(line.TextKey, text ?? "") : text ?? "";

        /// <summary>
        /// The key a line gets when it has none: <c>dlg.&lt;dialogue&gt;.&lt;node&gt;</c> (an answer:
        /// <c>dlg.&lt;dialogue&gt;.&lt;node&gt;.&lt;choice&gt;</c>), to fill a string table from.
        /// </summary>
        public static string KeyFor(string dialogueId, string nodeId, string choiceId = null) =>
            "dlg." + dialogueId + "." + nodeId + (string.IsNullOrEmpty(choiceId) ? "" : "." + choiceId);
    }

    /// <summary>
    /// A bark: a one-line remark a character makes in passing ("Watch your step."), not a conversation. A bark
    /// dialogue is a list of Line nodes; <see cref="Pick"/> takes one whose condition passes.
    /// </summary>
    public static class DialogueBarks
    {
        /// <summary>
        /// A random Line node of the dialogue whose condition passes in <paramref name="context"/> (no context: only
        /// lines without a condition); null when none can be said.
        /// </summary>
        public static IDialogueNode Pick(IDialogue dialogue, IDialogueContext context, Random random)
        {
            if (dialogue == null)
                return null;
            var lines = new List<IDialogueNode>();
            foreach (var node in dialogue.Nodes)
            {
                if (node.NodeType != DialogueNodeType.Line || string.IsNullOrEmpty(node.Text))
                    continue;
                if (node.Condition != null && (context == null || !node.Condition.Evaluate(context)))
                    continue;
                lines.Add(node);
            }
            if (lines.Count == 0)
                return null;
            return lines[(random ?? new Random()).Next(lines.Count)];
        }
    }
}
