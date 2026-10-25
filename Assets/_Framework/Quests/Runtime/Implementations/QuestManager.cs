using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuestManager.
    /// ECS-based manager for quest progression and state.
    /// </summary>
    public class QuestManager : IQuestManager
    {
        private readonly IEntityManager _entityManager;
        private readonly IQuestRegistry _questRegistry;

        public QuestManager(IEntityManager entityManager, IQuestRegistry questRegistry)
        {
            _entityManager = entityManager;
            _questRegistry = questRegistry;
        }

        /// <summary>
        /// Check if an entity can accept a quest.
        /// </summary>
        public bool CanAcceptQuest(int entityId, string questId)
        {
            var quest = _questRegistry.GetQuest(questId);
            if (quest == null)
                return false;

            // Check if entity has ActiveQuestsData
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
            {
                // Check minimum level requirement (would need a level component)
                return true;
            }

            // Check if quest is already active
            if (activeQuests.ActiveQuests != null && activeQuests.ActiveQuests.ContainsKey(questId))
                return false;

            // Check if quest is already completed (and not repeatable)
            if (!quest.IsRepeatable && activeQuests.CompletedQuestIds != null && activeQuests.CompletedQuestIds.Contains(questId))
                return false;

            // Check prerequisite quests
            foreach (var prerequisiteId in quest.PrerequisiteQuestIds)
            {
                if (activeQuests.CompletedQuestIds == null || !activeQuests.CompletedQuestIds.Contains(prerequisiteId))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Accept a quest for an entity.
        /// </summary>
        public bool AcceptQuest(int entityId, string questId)
        {
            if (!CanAcceptQuest(entityId, questId))
                return false;

            var quest = _questRegistry.GetQuest(questId);
            if (quest == null)
                return false;

            // Get or create ActiveQuestsData
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
            {
                activeQuests = new ActiveQuestsData
                {
                    ActiveQuests = new Dictionary<string, QuestProgressData>(),
                    CompletedQuestIds = new List<string>(),
                    TotalQuestsCompleted = 0
                };
            }

            // Create new quest progress
            var progress = new QuestProgress(questId);

            // Initialize objective progress
            var objectiveProgress = new Dictionary<string, int>();
            foreach (var objective in quest.Objectives)
            {
                objectiveProgress[objective.Id] = 0;
            }

            // Update the progress with initialized objectives
            var progressData = progress.ToData();
            progressData.ObjectiveProgress = objectiveProgress;

            activeQuests.ActiveQuests[questId] = progressData;
            _entityManager.AddComponent(entityId, activeQuests);

            return true;
        }

        /// <summary>
        /// Update progress for a specific objective.
        /// </summary>
        public bool UpdateObjectiveProgress(int entityId, string questId, string objectiveId, int progress)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return false;

            if (activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.TryGetValue(questId, out var questProgress))
                return false;

            var quest = _questRegistry.GetQuest(questId);
            if (quest == null)
                return false;

            var objective = quest.Objectives.FirstOrDefault(o => o.Id == objectiveId);
            if (objective == null)
                return false;

            // Update progress
            if (!questProgress.ObjectiveProgress.ContainsKey(objectiveId))
                questProgress.ObjectiveProgress[objectiveId] = 0;

            int currentProgress = questProgress.ObjectiveProgress[objectiveId];
            int newProgress = Math.Min(currentProgress + progress, objective.RequiredCount);

            questProgress.ObjectiveProgress[objectiveId] = newProgress;
            activeQuests.ActiveQuests[questId] = questProgress;
            _entityManager.AddComponent(entityId, activeQuests);

            return true;
        }

        /// <summary>
        /// Complete a quest for an entity.
        /// </summary>
        public bool CompleteQuest(int entityId, string questId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return false;

            if (activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.TryGetValue(questId, out var questProgress))
                return false;

            var quest = _questRegistry.GetQuest(questId);
            if (quest == null)
                return false;

            // Check if all non-optional objectives are complete
            foreach (var objective in quest.Objectives)
            {
                if (objective.IsOptional)
                    continue;

                if (!questProgress.ObjectiveProgress.TryGetValue(objective.Id, out var progress) ||
                    progress < objective.RequiredCount)
                {
                    return false;
                }
            }

            // Mark quest as completed
            questProgress.State = QuestState.Completed;
            questProgress.CompletedAtTicks = DateTime.UtcNow.Ticks;

            activeQuests.ActiveQuests.Remove(questId);
            if (activeQuests.CompletedQuestIds == null)
                activeQuests.CompletedQuestIds = new List<string>();

            activeQuests.CompletedQuestIds.Add(questId);
            activeQuests.TotalQuestsCompleted++;

            _entityManager.AddComponent(entityId, activeQuests);

            return true;
        }

        /// <summary>
        /// Abandon an active quest.
        /// </summary>
        public bool AbandonQuest(int entityId, string questId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return false;

            if (activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.TryGetValue(questId, out var questProgress))
                return false;

            questProgress.State = QuestState.Abandoned;
            activeQuests.ActiveQuests.Remove(questId);
            _entityManager.AddComponent(entityId, activeQuests);

            return true;
        }

        /// <summary>
        /// Get progress for a specific quest.
        /// </summary>
        public IQuestProgress? GetQuestProgress(int entityId, string questId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return null;

            if (activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.TryGetValue(questId, out var questProgress))
                return null;

            return new QuestProgress(questProgress);
        }

        /// <summary>
        /// Get all active quests for an entity.
        /// </summary>
        public IReadOnlyList<IQuestProgress> GetActiveQuests(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return new List<IQuestProgress>();

            if (activeQuests.ActiveQuests == null || activeQuests.ActiveQuests.Count == 0)
                return new List<IQuestProgress>();

            var result = new List<IQuestProgress>();
            foreach (var questData in activeQuests.ActiveQuests.Values)
            {
                result.Add(new QuestProgress(questData));
            }

            return result;
        }

        /// <summary>
        /// Get all completed quests for an entity.
        /// </summary>
        public IReadOnlyList<string> GetCompletedQuests(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return new List<string>();

            return activeQuests.CompletedQuestIds ?? new List<string>();
        }
    }
}
