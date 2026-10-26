using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Quests
{
    /// <summary>
    /// Implementation of IQuestManager.
    /// ECS-based manager for quest progression and state.
    /// With an event publisher, raises QuestAccepted/ObjectiveProgressed/QuestCompleted/QuestAbandoned/QuestFailed.
    /// </summary>
    public class QuestManager : IQuestManager
    {
        private readonly IEntityManager _entityManager;
        private readonly IQuestRegistry _questRegistry;
        private readonly IEventPublisher _events;

        public QuestManager(IEntityManager entityManager, IQuestRegistry questRegistry, IEventPublisher events = null)
        {
            _entityManager = entityManager;
            _questRegistry = questRegistry;
            _events = events;
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

            _events?.Publish(new QuestAcceptedEvent
            {
                EntityId = entityId,
                QuestId = questId,
                AcceptedAt = DateTime.UtcNow
            });
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

            if (newProgress != currentProgress)
            {
                _events?.Publish(new ObjectiveProgressedEvent
                {
                    EntityId = entityId,
                    QuestId = questId,
                    ObjectiveId = objectiveId,
                    CurrentProgress = newProgress,
                    RequiredProgress = objective.RequiredCount,
                    IsCompleted = newProgress >= objective.RequiredCount
                });
            }
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

            _events?.Publish(new QuestCompletedEvent
            {
                EntityId = entityId,
                QuestId = questId,
                CompletedAt = new DateTime(questProgress.CompletedAtTicks.Value, DateTimeKind.Utc),
                Rewards = quest.Rewards != null ? new List<IQuestReward>(quest.Rewards) : new List<IQuestReward>()
            });
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

            _events?.Publish(new QuestAbandonedEvent { EntityId = entityId, QuestId = questId });
            return true;
        }

        /// <summary>
        /// Fail an active quest.
        /// </summary>
        public bool FailQuest(int entityId, string questId, string reason = null)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return false;

            if (activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.ContainsKey(questId))
                return false;

            activeQuests.ActiveQuests.Remove(questId);
            _entityManager.AddComponent(entityId, activeQuests);

            _events?.Publish(new QuestFailedEvent { EntityId = entityId, QuestId = questId, FailureReason = reason });
            return true;
        }

        /// <summary>
        /// Advance every matching objective of the entity's active quests.
        /// </summary>
        public int ReportProgress(int entityId, ObjectiveType type, string targetId, int amount = 1)
        {
            if (amount <= 0)
                return 0;

            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests) ||
                activeQuests.ActiveQuests == null || activeQuests.ActiveQuests.Count == 0)
                return 0;

            int advanced = 0;
            // Snapshot: handlers of the progress events may accept, complete or abandon quests
            foreach (var questId in activeQuests.ActiveQuests.Keys.ToList())
            {
                var quest = _questRegistry.GetQuest(questId);
                if (quest == null)
                    continue;

                foreach (var objective in quest.Objectives)
                {
                    if (objective.Type != type ||
                        (!string.IsNullOrEmpty(objective.TargetId) && objective.TargetId != targetId))
                        continue;

                    var progress = GetQuestProgress(entityId, questId);
                    if (progress == null ||
                        (progress.ObjectiveProgress.TryGetValue(objective.Id, out var current) && current >= objective.RequiredCount))
                        continue;

                    if (UpdateObjectiveProgress(entityId, questId, objective.Id, amount))
                        advanced++;
                }
            }
            return advanced;
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
