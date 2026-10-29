using System;
using System.Collections.Generic;

namespace RPGFramework.Dialogue
{
    /// <summary>
    /// A context that also answers the game's own questions: how many of an item the entity has (an ItemOwned
    /// condition with RequiredValue above 1 asks for that many) and Custom conditions (ParameterId is the game's
    /// condition id, RequiredValue its number). A plain <see cref="IDialogueContext"/> keeps working for one item; there
    /// an ItemOwned asking for more than one, and every Custom condition, fails (it cannot be checked).
    /// </summary>
    public interface IDialogueGameContext : IDialogueContext
    {
        int CountItem(string itemId);

        bool CheckCustom(string conditionId, int value);
    }

    /// <summary>
    /// A dialogue context made of the game's answers, for when the game has no context class of its own:
    /// <c>new DialogueContext(player) { QuestDone = quests.IsCompleted, ItemCount = bag.Count, Custom = (id, n) =&gt; ... }</c>.
    /// A question without an answer is false (or 0).
    /// </summary>
    public sealed class DialogueContext : IDialogueGameContext
    {
        public DialogueContext(int entityId)
        {
            EntityId = entityId;
        }

        public int EntityId { get; }
        public string CurrentNodeId { get; set; }
        public Dictionary<string, int> Variables { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        public Func<string, bool> QuestDone { get; set; }
        public Func<string, int> ItemCount { get; set; }
        public Func<string, int> Stat { get; set; }

        /// <summary>
        /// (condition id, value) of a Custom condition.
        /// </summary>
        public Func<string, int, bool> Custom { get; set; }

        public bool HasCompletedQuest(string questId) => QuestDone != null && QuestDone(questId);
        public bool HasItem(string itemId) => CountItem(itemId) > 0;
        public int GetStatValue(string statId) => Stat?.Invoke(statId) ?? 0;
        public int CountItem(string itemId) => ItemCount?.Invoke(itemId) ?? 0;
        public bool CheckCustom(string conditionId, int value) => Custom != null && Custom(conditionId, value);
    }
}
