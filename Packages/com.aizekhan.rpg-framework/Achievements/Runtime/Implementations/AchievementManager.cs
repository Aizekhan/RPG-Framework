using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Achievements.Runtime;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Implementation of IAchievementManager.
    /// ECS-based manager for achievement tracking and unlocking.
    /// With an event publisher, raises AchievementProgress and CriterionCompleted when criterion values change,
    /// and AchievementUnlocked when an achievement unlocks.
    /// </summary>
    public class AchievementManager : IAchievementManager
    {
        private readonly IAchievementRegistry _registry;
        private readonly IEntityManager _entityManager;
        private readonly Dictionary<int, Dictionary<string, AchievementProgress>> _entityProgress;
        private readonly IEventPublisher _events;

        /// <summary>
        /// Create an achievement manager.
        /// </summary>
        /// <param name="registry">Achievement registry.</param>
        /// <param name="entityManager">ECS entity manager.</param>
        /// <param name="events">Optional publisher for achievement events; null publishes nothing.</param>
        public AchievementManager(IAchievementRegistry registry, IEntityManager entityManager,
            IEventPublisher events = null)
        {
            _events = events;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _entityProgress = new Dictionary<int, Dictionary<string, AchievementProgress>>();
        }

        /// <summary>
        /// Update progress for a specific achievement criterion.
        /// </summary>
        public bool UpdateCriterionProgress(int entityId, string achievementId, string criterionId, int currentValue)
        {
            var progress = GetOrCreateProgress(entityId, achievementId);
            if (progress == null)
                return false;

            bool updated = UpdateCriterion(entityId, progress, criterionId, currentValue);

            // Check if achievement can be unlocked
            if (updated && progress.AreAllCriteriaCompleted() && progress.State != AchievementState.Unlocked)
            {
                UnlockAchievement(entityId, achievementId);
            }

            return updated;
        }

        /// <summary>
        /// Manually unlock an achievement for an entity.
        /// </summary>
        public bool UnlockAchievement(int entityId, string achievementId)
        {
            var achievement = _registry.GetAchievement(achievementId);
            if (achievement == null)
                return false;

            var progress = GetOrCreateProgress(entityId, achievementId);
            if (progress == null || progress.State == AchievementState.Unlocked)
                return false;

            // Mark as unlocked
            progress.Unlock();

            // Update ECS component
            var data = _entityManager.GetComponent<AchievementData>(entityId);
            var unlockedIds = data.UnlockedAchievementIds?.ToList() ?? new List<string>();
            var timestamps = data.UnlockTimestamps?.ToList() ?? new List<long>();

            var unlockedAt = DateTime.UtcNow;
            if (!unlockedIds.Contains(achievementId))
            {
                unlockedIds.Add(achievementId);
                timestamps.Add(unlockedAt.Ticks);

                _entityManager.AddComponent(entityId, new AchievementData(
                    unlockedIds.ToArray(),
                    timestamps.ToArray(),
                    data.TotalPoints + achievement.Points));

                // Only a first-time unlock is news (the entity data may already hold it, e.g. from a save)
                _events?.Publish(new AchievementUnlockedEvent(
                    entityId, achievementId, achievement.Tier, achievement.Points, unlockedAt));
            }

            return true;
        }

        /// <summary>
        /// Check and unlock achievements retroactively based on current entity state.
        /// </summary>
        public IReadOnlyList<string> CheckRetroactiveUnlocks(
            int entityId,
            CriterionType criterionType,
            IReadOnlyDictionary<string, int> criterionData)
        {
            var unlockedAchievements = new List<string>();
            var achievements = _registry.GetAchievementsByCriterionType(criterionType);

            foreach (var achievement in achievements)
            {
                if (IsAchievementUnlocked(entityId, achievement.AchievementId))
                    continue;

                var progress = GetOrCreateProgress(entityId, achievement.AchievementId);
                if (progress == null)
                    continue;

                // Update progress for matching criteria
                foreach (var criterion in achievement.Criteria.Where(c => c.Type == criterionType))
                {
                    if (criterionData.TryGetValue(criterion.TargetId, out int value))
                    {
                        UpdateCriterion(entityId, progress, criterion.CriterionId, value);
                    }
                }

                // Check if can be unlocked
                if (progress.AreAllCriteriaCompleted())
                {
                    if (UnlockAchievement(entityId, achievement.AchievementId))
                    {
                        unlockedAchievements.Add(achievement.AchievementId);
                    }
                }
            }

            return unlockedAchievements;
        }

        /// <summary>
        /// Get progress for a specific achievement.
        /// </summary>
        public IAchievementProgress? GetAchievementProgress(int entityId, string achievementId)
        {
            if (!_entityProgress.TryGetValue(entityId, out var achievements))
                return null;

            achievements.TryGetValue(achievementId, out var progress);
            return progress;
        }

        /// <summary>
        /// Get all unlocked achievements for an entity.
        /// </summary>
        public IReadOnlyList<string> GetUnlockedAchievements(int entityId)
        {
            var data = _entityManager.GetComponent<AchievementData>(entityId);
            return data.UnlockedAchievementIds?.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Get all achievements in progress for an entity.
        /// </summary>
        public IReadOnlyList<IAchievementProgress> GetInProgressAchievements(int entityId)
        {
            if (!_entityProgress.TryGetValue(entityId, out var achievements))
                return new List<IAchievementProgress>();

            return achievements.Values
                .Where(p => p.State == AchievementState.InProgress)
                .Cast<IAchievementProgress>()
                .ToList();
        }

        /// <summary>
        /// Check if an entity has unlocked a specific achievement.
        /// </summary>
        public bool IsAchievementUnlocked(int entityId, string achievementId)
        {
            var data = _entityManager.GetComponent<AchievementData>(entityId);
            return data.UnlockedAchievementIds?.Contains(achievementId) ?? false;
        }

        /// <summary>
        /// Initialize achievement tracking for a new entity.
        /// </summary>
        public void InitializeEntityAchievements(int entityId)
        {
            _entityManager.AddComponent(entityId, AchievementData.Empty());
            _entityProgress[entityId] = new Dictionary<string, AchievementProgress>();

            // Initialize progress tracking for all achievements
            foreach (var achievement in _registry.GetAllAchievements())
            {
                var criteria = achievement.Criteria.Select(c =>
                    (c.CriterionId, c.RequiredCount));

                _entityProgress[entityId][achievement.AchievementId] =
                    new AchievementProgress(achievement.AchievementId, criteria);
            }
        }

        /// <summary>
        /// Get achievements by tier.
        /// </summary>
        public IReadOnlyList<string> GetAchievementsByTier(AchievementTier tier)
        {
            return _registry.GetAchievementsByTier(tier)
                .Select(a => a.AchievementId)
                .ToList();
        }

        /// <summary>
        /// Update one criterion and publish the progress (and, on completion, criterion completed) events.
        /// </summary>
        private bool UpdateCriterion(int entityId, AchievementProgress progress, string criterionId, int currentValue)
        {
            var before = progress.GetCriterionProgress(criterionId);
            if (!progress.UpdateCriterion(criterionId, currentValue))
                return false;

            if (_events == null || !before.HasValue)
                return true;

            var after = progress.GetCriterionProgress(criterionId).Value;
            bool completedNow = after.IsCompleted && !before.Value.IsCompleted;

            _events.Publish(new AchievementProgressEvent(
                entityId,
                progress.AchievementId,
                criterionId,
                before.Value.CurrentValue,
                after.CurrentValue,
                after.RequiredValue,
                completedNow,
                progress.CompletionPercentage));

            if (completedNow)
            {
                var criterion = _registry.GetAchievement(progress.AchievementId)?.Criteria
                    .FirstOrDefault(c => c.CriterionId == criterionId);
                if (criterion != null)
                {
                    _events.Publish(new CriterionCompletedEvent(
                        entityId,
                        progress.AchievementId,
                        criterionId,
                        criterion.Type,
                        after.CurrentValue,
                        after.RequiredValue));
                }
            }

            return true;
        }

        /// <summary>
        /// Get or create progress for an achievement.
        /// </summary>
        private AchievementProgress? GetOrCreateProgress(int entityId, string achievementId)
        {
            var achievement = _registry.GetAchievement(achievementId);
            if (achievement == null)
                return null;

            if (!_entityProgress.TryGetValue(entityId, out var achievements))
            {
                achievements = new Dictionary<string, AchievementProgress>();
                _entityProgress[entityId] = achievements;
            }

            if (!achievements.TryGetValue(achievementId, out var progress))
            {
                var criteria = achievement.Criteria.Select(c =>
                    (c.CriterionId, c.RequiredCount));

                progress = new AchievementProgress(achievementId, criteria);
                achievements[achievementId] = progress;
            }

            return progress;
        }
    }
}
