using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuestRegistry.
    /// Simple dictionary-based registry for quest definitions.
    /// </summary>
    public class QuestRegistry : IQuestRegistry
    {
        private readonly Dictionary<string, IQuest> _quests = new();

        /// <summary>
        /// Get a quest by ID.
        /// </summary>
        public IQuest? GetQuest(string questId)
        {
            return _quests.TryGetValue(questId, out var quest) ? quest : null;
        }

        /// <summary>
        /// Get all registered quests.
        /// </summary>
        public IReadOnlyList<IQuest> GetAllQuests()
        {
            return _quests.Values.ToList();
        }

        /// <summary>
        /// Register a quest definition.
        /// </summary>
        public void RegisterQuest(IQuest quest)
        {
            if (quest != null && !_quests.ContainsKey(quest.Id))
            {
                _quests[quest.Id] = quest;
            }
        }
    }
}
