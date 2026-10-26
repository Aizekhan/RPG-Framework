using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Registry for quest definitions.
    /// </summary>
    public interface IQuestRegistry
    {
        /// <summary>
        /// Get a quest by ID.
        /// </summary>
        /// <param name="questId">Quest ID</param>
        /// <returns>Quest or null if not found</returns>
        IQuest? GetQuest(string questId);

        /// <summary>
        /// Get all registered quests.
        /// </summary>
        /// <returns>List of all quests</returns>
        IReadOnlyList<IQuest> GetAllQuests();

        /// <summary>
        /// Register a quest definition.
        /// </summary>
        /// <param name="quest">Quest to register</param>
        void RegisterQuest(IQuest quest);
    }
}
