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
    /// With an event publisher, raises QuestAccepted/ObjectiveProgressed/QuestStageChanged/QuestCompleted/
    /// QuestAbandoned/QuestFailed.
    /// Quests with stages (<see cref="IStagedQuest"/>) move between them (<see cref="IQuestStages"/>): only the current
    /// stage's objectives count, a stage whose required objectives are done takes its link without a condition, a
    /// signalled condition takes the link that names it, and a success or failure stage ends the quest.
    /// </summary>
    public class QuestManager : IQuestManager, IQuestStages
    {
        private readonly IEntityManager _entityManager;
        private readonly IQuestRegistry _questRegistry;
        private readonly IEventPublisher _events;
        private readonly Func<DateTime> _clock;

        /// <param name="requirements">Answers the quests' requirements and minimum level; none = a quest with
        /// requirements cannot be taken, the minimum level is not checked.</param>
        /// <param name="clock">UTC now, for repeat cooldowns; <see cref="DateTime.UtcNow"/> when not given.</param>
        public QuestManager(IEntityManager entityManager, IQuestRegistry questRegistry, IEventPublisher events = null,
            IQuestRequirements requirements = null, Func<DateTime> clock = null)
        {
            _entityManager = entityManager;
            _questRegistry = questRegistry;
            _events = events;
            Requirements = requirements;
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Answers the quests' requirements and minimum level; can be set after construction (the action RPG kit
        /// sets its own when none is given).
        /// </summary>
        public IQuestRequirements Requirements { get; set; }

        /// <summary>
        /// Check if an entity can accept a quest (<see cref="WhyNot"/> says why not).
        /// </summary>
        public bool CanAcceptQuest(int entityId, string questId) => WhyNot(entityId, questId) == QuestRefusal.None;

        public QuestRefusal WhyNot(int entityId, string questId)
        {
            var quest = _questRegistry.GetQuest(questId);
            if (quest == null)
                return QuestRefusal.UnknownQuest;

            _entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests);
            if (activeQuests.ActiveQuests != null && activeQuests.ActiveQuests.ContainsKey(questId))
                return QuestRefusal.AlreadyActive;

            bool completed = activeQuests.CompletedQuestIds != null && activeQuests.CompletedQuestIds.Contains(questId);
            if (completed && !quest.IsRepeatable)
                return QuestRefusal.AlreadyCompleted;
            if (CooldownLeft(entityId, questId) > TimeSpan.Zero)
                return QuestRefusal.CoolingDown;

            foreach (var prerequisiteId in quest.PrerequisiteQuestIds)
            {
                if (activeQuests.CompletedQuestIds == null || !activeQuests.CompletedQuestIds.Contains(prerequisiteId))
                    return QuestRefusal.PrerequisiteMissing;
            }

            if (Requirements != null && quest.MinimumLevel > 1 &&
                !Requirements.IsMet(entityId, new QuestRequirementData { Kind = QuestRequirementKinds.Level, Value = quest.MinimumLevel }))
                return QuestRefusal.LevelTooLow;
            if (quest is IStagedQuest staged)
            {
                foreach (var requirement in staged.Requirements)
                {
                    if (Requirements == null || !Requirements.IsMet(entityId, requirement))
                        return QuestRefusal.RequirementNotMet;
                }
            }
            return QuestRefusal.None;
        }

        /// <summary>
        /// Accept a quest for an entity. A quest with stages enters its first stage.
        /// </summary>
        public bool AcceptQuest(int entityId, string questId)
        {
            if (!CanAcceptQuest(entityId, questId))
                return false;

            var quest = _questRegistry.GetQuest(questId);
            if (quest == null)
                return false;

            var activeQuests = QuestsOf(entityId);

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
            progressData.StartedAtTicks = _clock().Ticks;
            string start = StartOf(quest);
            progressData.StageId = start;
            activeQuests.ActiveQuests[questId] = progressData;

            _entityManager.AddComponent(entityId, activeQuests);

            _events?.Publish(new QuestAcceptedEvent
            {
                EntityId = entityId,
                QuestId = questId,
                AcceptedAt = _clock()
            });

            if (start != null)
            {
                _events?.Publish(new QuestStageChangedEvent
                {
                    EntityId = entityId, QuestId = questId, FromStageId = null, ToStageId = start,
                    Outcome = StageOf(quest, start)?.Outcome ?? QuestOutcome.None
                });
                Enter(entityId, quest, start, null);
            }

            return true;
        }

        /// <summary>
        /// Update progress on a specific objective.
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

                // A stage whose objectives are now done moves on
                var stage = CurrentStageOf(entityId, questId);
                if (stage != null)
                    Enter(entityId, quest, stage, null);
            }

            return true;
        }

        /// <summary>
        /// Complete a quest (all required objectives must be done; a quest with stages must be in a success stage,
        /// which completes it on its own).
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

            if (questProgress.StageId != null && StageOf(quest, questProgress.StageId) is QuestStageData stage)
            {
                if (stage.Outcome != QuestOutcome.Success)
                    return false;
            }
            else
            {
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
            }

            // Mark quest as completed
            var now = _clock();
            questProgress.State = QuestState.Completed;
            questProgress.CompletedAtTicks = now.Ticks;

            activeQuests.ActiveQuests.Remove(questId);
            if (activeQuests.CompletedQuestIds == null)
                activeQuests.CompletedQuestIds = new List<string>();
            if (!activeQuests.CompletedQuestIds.Contains(questId))
                activeQuests.CompletedQuestIds.Add(questId);
            activeQuests.TotalQuestsCompleted++;
            if (activeQuests.LastCompletedTicks == null)
                activeQuests.LastCompletedTicks = new Dictionary<string, long>();
            activeQuests.LastCompletedTicks[questId] = now.Ticks;

            _entityManager.AddComponent(entityId, activeQuests);

            _events?.Publish(new QuestCompletedEvent
            {
                EntityId = entityId,
                QuestId = questId,
                CompletedAt = new DateTime(now.Ticks, DateTimeKind.Utc),
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
        /// Fail an active quest (a game rule decided it cannot be completed any more).
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
        /// Advance every matching objective of the entity's active quests (in a quest with stages, those that count
        /// in its current stage).
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
                    var stage = CurrentStageOf(entityId, questId);
                    if (stage != null && !CountsIn(quest, stage, objective.Id))
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
        /// Get all completed quest IDs for an entity.
        /// </summary>
        public IReadOnlyList<string> GetCompletedQuests(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests))
                return new List<string>();

            return activeQuests.CompletedQuestIds ?? new List<string>();
        }

        // --- stages

        public string CurrentStageOf(int entityId, string questId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests) ||
                activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.TryGetValue(questId, out var progress))
                return null;
            return progress.StageId;
        }

        public bool AdvanceStage(int entityId, string questId, string stageId, bool force = false)
        {
            var quest = _questRegistry.GetQuest(questId);
            var current = CurrentStageOf(entityId, questId);
            if (quest == null || current == null || StageOf(quest, stageId) == null)
                return false;
            if (!force && !(StageOf(quest, current)?.Next ?? Array.Empty<QuestStageLinkData>()).Any(l => l.To == stageId))
                return false;
            Move(entityId, quest, current, stageId, null);
            return true;
        }

        public int Signal(int entityId, string condition)
        {
            if (string.IsNullOrWhiteSpace(condition) ||
                !_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests) || activeQuests.ActiveQuests == null)
                return 0;
            condition = condition.Trim();
            int moved = 0;
            foreach (var questId in activeQuests.ActiveQuests.Keys.ToList())
            {
                var quest = _questRegistry.GetQuest(questId);
                var current = CurrentStageOf(entityId, questId);
                if (quest == null || current == null)
                    continue;
                foreach (var link in StageOf(quest, current)?.Next ?? Array.Empty<QuestStageLinkData>())
                {
                    if (string.IsNullOrWhiteSpace(link.Condition) ||
                        !string.Equals(link.Condition.Trim(), condition, StringComparison.OrdinalIgnoreCase) || StageOf(quest, link.To) == null)
                        continue;
                    Move(entityId, quest, current, link.To, condition);
                    moved++;
                    break;
                }
            }
            return moved;
        }

        public DateTime? LastCompletedAt(int entityId, string questId)
        {
            if (!_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests) ||
                activeQuests.LastCompletedTicks == null || !activeQuests.LastCompletedTicks.TryGetValue(questId, out long ticks))
                return null;
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        public TimeSpan CooldownLeft(int entityId, string questId)
        {
            if (!(_questRegistry.GetQuest(questId) is IStagedQuest quest) || !quest.IsRepeatable || quest.RepeatCooldownSeconds <= 0d)
                return TimeSpan.Zero;
            var last = LastCompletedAt(entityId, questId);
            if (last == null)
                return TimeSpan.Zero;
            var left = last.Value.AddSeconds(quest.RepeatCooldownSeconds) - _clock();
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        public bool RestoreStage(int entityId, string questId, string stageId)
        {
            var quest = _questRegistry.GetQuest(questId);
            if (quest == null || StageOf(quest, stageId) == null ||
                !_entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests) ||
                activeQuests.ActiveQuests == null || !activeQuests.ActiveQuests.TryGetValue(questId, out var progress))
                return false;
            progress.StageId = stageId;
            activeQuests.ActiveQuests[questId] = progress;
            _entityManager.AddComponent(entityId, activeQuests);
            return true;
        }

        public void RestoreCompletedAt(int entityId, string questId, DateTime completedAtUtc)
        {
            var activeQuests = QuestsOf(entityId);
            if (activeQuests.LastCompletedTicks == null)
                activeQuests.LastCompletedTicks = new Dictionary<string, long>();
            activeQuests.LastCompletedTicks[questId] = completedAtUtc.ToUniversalTime().Ticks;
            _entityManager.AddComponent(entityId, activeQuests);
        }

        private ActiveQuestsData QuestsOf(int entityId)
        {
            _entityManager.TryGetComponent<ActiveQuestsData>(entityId, out var activeQuests);
            activeQuests.ActiveQuests = activeQuests.ActiveQuests ?? new Dictionary<string, QuestProgressData>();
            activeQuests.CompletedQuestIds = activeQuests.CompletedQuestIds ?? new List<string>();
            return activeQuests;
        }

        private static string StartOf(IQuest quest) =>
            quest is IStagedQuest staged && staged.Stages.Count > 0
                ? (StageOf(quest, staged.StartStageId) != null ? staged.StartStageId : staged.Stages[0].Id)
                : null;

        private static QuestStageData? StageOf(IQuest quest, string stageId)
        {
            if (stageId == null || !(quest is IStagedQuest staged))
                return null;
            foreach (var stage in staged.Stages)
            {
                if (stage.Id == stageId)
                    return stage;
            }
            return null;
        }

        // An objective counts in the stages that name it; one named by no stage counts in all of them
        private static bool CountsIn(IQuest quest, string stageId, string objectiveId)
        {
            var staged = (IStagedQuest)quest;
            bool named = false;
            foreach (var stage in staged.Stages)
            {
                if (stage.ObjectiveIds == null || Array.IndexOf(stage.ObjectiveIds, objectiveId) < 0)
                    continue;
                if (stage.Id == stageId)
                    return true;
                named = true;
            }
            return !named;
        }

        private bool StageDone(int entityId, IQuest quest, string stageId)
        {
            var progress = GetQuestProgress(entityId, quest.Id);
            if (progress == null)
                return false;
            foreach (var objective in quest.Objectives)
            {
                if (objective.IsOptional || !CountsIn(quest, stageId, objective.Id))
                    continue;
                if (!progress.ObjectiveProgress.TryGetValue(objective.Id, out int done) || done < objective.RequiredCount)
                    return false;
            }
            return true;
        }

        private void Move(int entityId, IQuest quest, string from, string to, string condition)
        {
            if (!RestoreStage(entityId, quest.Id, to))
                return;
            var stage = StageOf(quest, to).Value;
            _events?.Publish(new QuestStageChangedEvent
            {
                EntityId = entityId, QuestId = quest.Id, FromStageId = from, ToStageId = to, Condition = condition,
                Outcome = stage.Outcome
            });
            Enter(entityId, quest, to, condition);
        }

        // In a stage: end the quest when it is an end, else take the link without a condition once its objectives
        // are done (a chain of such stages is walked, at most once per stage)
        private void Enter(int entityId, IQuest quest, string stageId, string condition)
        {
            var visited = new HashSet<string>();
            while (stageId != null && visited.Add(stageId))
            {
                if (CurrentStageOf(entityId, quest.Id) != stageId)
                    return;
                var stage = StageOf(quest, stageId).Value;
                if (stage.Outcome == QuestOutcome.Success)
                {
                    CompleteQuest(entityId, quest.Id);
                    return;
                }
                if (stage.Outcome == QuestOutcome.Failure)
                {
                    FailQuest(entityId, quest.Id, condition ?? "stage " + stageId);
                    return;
                }
                if (!StageDone(entityId, quest, stageId))
                    return;
                var next = (stage.Next ?? Array.Empty<QuestStageLinkData>())
                    .FirstOrDefault(l => string.IsNullOrWhiteSpace(l.Condition) && StageOf(quest, l.To) != null);
                if (next.To == null || !RestoreStage(entityId, quest.Id, next.To))
                    return;
                _events?.Publish(new QuestStageChangedEvent
                {
                    EntityId = entityId, QuestId = quest.Id, FromStageId = stageId, ToStageId = next.To,
                    Outcome = StageOf(quest, next.To).Value.Outcome
                });
                stageId = next.To;
                condition = null;
            }
        }
    }
}
