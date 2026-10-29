using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Achievements.Runtime;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Achievements
{
    /// <summary>
    /// Counts what a player does (kills by enemy kind, items got, quests done, the level reached) and moves every
    /// achievement criterion of that type and target along: a criterion with no target counts them all. The counts
    /// are the player's profile (<see cref="Capture"/>, <see cref="Restore"/>), apart from any one save: a load puts
    /// back what was unlocked without announcing it again, and achievements added since unlock if the counts already
    /// reach them.
    /// </summary>
    public sealed class AchievementTracker
    {
        private readonly IAchievementRegistry _registry;
        private readonly Dictionary<int, Dictionary<(CriterionType, string), long>> _counts =
            new Dictionary<int, Dictionary<(CriterionType, string), long>>();
        private readonly QuietPublisher _publisher;

        public AchievementTracker(IAchievementRegistry registry, IEntityManager entities, IEventPublisher events = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _publisher = new QuietPublisher(events);
            Manager = new AchievementManager(registry, entities ?? throw new ArgumentNullException(nameof(entities)), _publisher);
        }

        public IAchievementManager Manager { get; }

        public IAchievementRegistry Registry => _registry;

        /// <summary>
        /// The count of a kind of deed; an empty target is every target together.
        /// </summary>
        public long CountOf(int entityId, CriterionType type, string targetId = "") =>
            _counts.TryGetValue(entityId, out var counts) && counts.TryGetValue((type, targetId ?? ""), out long value) ? value : 0L;

        /// <summary>
        /// Something happened this many times (a kill of a "wolf", 3 "pelt"s got, 120 damage dealt).
        /// </summary>
        public void Add(int entityId, CriterionType type, string targetId, long amount = 1)
        {
            if (amount <= 0)
                return;
            targetId ??= "";
            Set(entityId, type, targetId, CountOf(entityId, type, targetId) + amount);
            if (targetId.Length > 0)
                Set(entityId, type, "", CountOf(entityId, type, "") + amount);
            Push(entityId, type, targetId);
        }

        /// <summary>
        /// A high-water mark was reached (level 10, 5000 gold held): it only counts when it is above the last one.
        /// </summary>
        public void Reach(int entityId, CriterionType type, string targetId, long value)
        {
            targetId ??= "";
            if (value <= CountOf(entityId, type, targetId))
                return;
            Set(entityId, type, targetId, value);
            if (targetId.Length > 0 && value > CountOf(entityId, type, ""))
                Set(entityId, type, "", value);
            Push(entityId, type, targetId);
        }

        public bool IsUnlocked(int entityId, string achievementId) => Manager.IsAchievementUnlocked(entityId, achievementId);

        public IReadOnlyList<string> UnlockedBy(int entityId) => Manager.GetUnlockedAchievements(entityId);

        /// <summary>
        /// How far an achievement is (0..1).
        /// </summary>
        public float ProgressOf(int entityId, string achievementId)
        {
            if (IsUnlocked(entityId, achievementId))
                return 1f;
            var achievement = _registry.GetAchievement(achievementId);
            if (achievement == null || achievement.Criteria.Count == 0)
                return 0f;
            return achievement.Criteria.Average(c => c.RequiredCount <= 0 ? 1f
                : Math.Min(1f, (float)CountFor(entityId, c) / c.RequiredCount));
        }

        public AchievementRecord Capture(int entityId)
        {
            var record = new AchievementRecord();
            if (_counts.TryGetValue(entityId, out var counts))
            {
                record.Counts.AddRange(counts.OrderBy(c => c.Key.Item1).ThenBy(c => c.Key.Item2, StringComparer.Ordinal)
                    .Select(c => new AchievementCountRecord { Type = c.Key.Item1, TargetId = c.Key.Item2, Value = c.Value }));
            }
            record.Unlocked.AddRange(UnlockedBy(entityId));
            return record;
        }

        /// <summary>
        /// Puts a profile back: the counts, and what was unlocked (no unlock event for those). Achievements the
        /// counts reach that were not unlocked yet (added in an update) unlock now, with their event.
        /// </summary>
        public void Restore(int entityId, AchievementRecord record)
        {
            if (record == null)
                return;
            _counts.Remove(entityId);
            foreach (var count in record.Counts ?? new List<AchievementCountRecord>())
            {
                if (count != null && count.Value > 0)
                    Set(entityId, count.Type, count.TargetId ?? "", count.Value);
            }
            _publisher.Quiet = true;
            try
            {
                foreach (var id in record.Unlocked ?? new List<string>())
                {
                    if (id != null && _registry.HasAchievement(id))
                        Manager.UnlockAchievement(entityId, id);
                }
            }
            finally
            {
                _publisher.Quiet = false;
            }
            foreach (var achievement in _registry.GetAllAchievements())
            {
                if (IsUnlocked(entityId, achievement.AchievementId))
                    continue;
                foreach (var criterion in achievement.Criteria)
                    Update(entityId, achievement.AchievementId, criterion);
            }
        }

        private void Set(int entityId, CriterionType type, string targetId, long value)
        {
            if (!_counts.TryGetValue(entityId, out var counts))
                _counts[entityId] = counts = new Dictionary<(CriterionType, string), long>();
            counts[(type, targetId)] = value;
        }

        private long CountFor(int entityId, Runtime.Contracts.IAchievementCriterion criterion) =>
            CountOf(entityId, criterion.Type, criterion.TargetId ?? "");

        // Every criterion that counts this target, or every target
        private void Push(int entityId, CriterionType type, string targetId)
        {
            foreach (var achievement in _registry.GetAchievementsByCriterionType(type))
            {
                if (IsUnlocked(entityId, achievement.AchievementId))
                    continue;
                foreach (var criterion in achievement.Criteria)
                {
                    string target = criterion.TargetId ?? "";
                    if (criterion.Type == type && (target.Length == 0 || target == targetId))
                        Update(entityId, achievement.AchievementId, criterion);
                }
            }
        }

        private void Update(int entityId, string achievementId, Runtime.Contracts.IAchievementCriterion criterion)
        {
            long value = CountFor(entityId, criterion);
            if (value > 0)
                Manager.UpdateCriterionProgress(entityId, achievementId, criterion.CriterionId, (int)Math.Min(value, int.MaxValue));
        }

        // Passes events on, except the unlock events of a profile being put back
        private sealed class QuietPublisher : IEventPublisher
        {
            private readonly IEventPublisher _inner;

            public QuietPublisher(IEventPublisher inner) => _inner = inner;

            public bool Quiet { get; set; }

            public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
            {
                if (_inner != null && !Quiet)
                    _inner.Publish(eventData);
            }

            public void PublishSticky<TEvent>(TEvent eventData) where TEvent : IEvent
            {
                if (_inner != null && !Quiet)
                    _inner.PublishSticky(eventData);
            }

            public Cysharp.Threading.Tasks.UniTask PublishAsync<TEvent>(TEvent eventData,
                System.Threading.CancellationToken cancellationToken = default) where TEvent : IEvent =>
                _inner != null && !Quiet ? _inner.PublishAsync(eventData, cancellationToken) : Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public void Enqueue<TEvent>(TEvent eventData) where TEvent : IEvent
            {
                if (_inner != null && !Quiet)
                    _inner.Enqueue(eventData);
            }
        }
    }

    /// <summary>
    /// A player's achievements as a profile keeps them: the counts and what was unlocked.
    /// </summary>
    public sealed class AchievementRecord
    {
        public List<AchievementCountRecord> Counts { get; set; } = new List<AchievementCountRecord>();
        public List<string> Unlocked { get; set; } = new List<string>();
    }

    public sealed class AchievementCountRecord
    {
        public CriterionType Type { get; set; }
        public string TargetId { get; set; } = "";
        public long Value { get; set; }
    }
}
