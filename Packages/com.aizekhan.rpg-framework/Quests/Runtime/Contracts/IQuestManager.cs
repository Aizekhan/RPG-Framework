using System.Collections.Generic;

namespace RPGFramework.Quests
{
    /// <summary>
    /// ECS-based manager for quest progression and state.
    /// </summary>
    public interface IQuestManager
    {
        /// <summary>
        /// Check if an entity can accept a quest.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID to check</param>
        /// <returns>True if the quest can be accepted</returns>
        bool CanAcceptQuest(int entityId, string questId);

        /// <summary>
        /// Accept a quest for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID to accept</param>
        /// <returns>True if quest was successfully accepted</returns>
        bool AcceptQuest(int entityId, string questId);

        /// <summary>
        /// Update progress for a specific objective.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID</param>
        /// <param name="objectiveId">Objective ID</param>
        /// <param name="progress">Progress amount to add</param>
        /// <returns>True if progress was updated</returns>
        bool UpdateObjectiveProgress(int entityId, string questId, string objectiveId, int progress);

        /// <summary>
        /// Complete a quest for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID to complete</param>
        /// <returns>True if quest was successfully completed</returns>
        bool CompleteQuest(int entityId, string questId);

        /// <summary>
        /// Abandon an active quest.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID to abandon</param>
        /// <returns>True if quest was abandoned</returns>
        bool AbandonQuest(int entityId, string questId);

        /// <summary>
        /// Fail an active quest (timer ran out, escort died, ...). It leaves the active list and can be accepted again.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID to fail</param>
        /// <param name="reason">Game-defined failure reason</param>
        /// <returns>True if quest was failed</returns>
        bool FailQuest(int entityId, string questId, string reason = null);

        /// <summary>
        /// Report something that happened in the world (a kill, a pickup, a visited area) and advance every matching
        /// objective of the entity's active quests: same type, and same target id (an objective with no target id
        /// accepts any target).
        /// </summary>
        /// <param name="entityId">Entity whose quests progress</param>
        /// <param name="type">Objective type</param>
        /// <param name="targetId">Game-defined target id (enemy type, item id, area id)</param>
        /// <param name="amount">Progress to add</param>
        /// <returns>Number of objectives that advanced</returns>
        int ReportProgress(int entityId, ObjectiveType type, string targetId, int amount = 1);

        /// <summary>
        /// Get progress for a specific quest.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <param name="questId">Quest ID</param>
        /// <returns>Quest progress or null if not found</returns>
        IQuestProgress? GetQuestProgress(int entityId, string questId);

        /// <summary>
        /// Get all active quests for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <returns>List of active quest progress</returns>
        IReadOnlyList<IQuestProgress> GetActiveQuests(int entityId);

        /// <summary>
        /// Get all completed quests for an entity.
        /// </summary>
        /// <param name="entityId">Entity ID</param>
        /// <returns>List of completed quest IDs</returns>
        IReadOnlyList<string> GetCompletedQuests(int entityId);
    }
}
