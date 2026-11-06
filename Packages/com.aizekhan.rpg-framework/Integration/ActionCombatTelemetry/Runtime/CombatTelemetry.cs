using System;
using System.Collections.Generic;
using RPGFramework.ActionCombat;
using RPGFramework.Events;
using RPGFramework.Telemetry;

namespace RPGFramework.Integration.ActionCombatTelemetry
{
    /// <summary>
    /// Sends action combat to telemetry: every finished swing of <see cref="CombatAnalytics"/> as
    /// <see cref="HitEvent"/>, <see cref="MissEvent"/>, <see cref="BlockedEvent"/>, <see cref="ParriedEvent"/> or
    /// <see cref="DodgedEvent"/> (properties <c>attack</c>, <c>attacker</c>, <c>target</c>, <c>damage</c>,
    /// <c>distance</c>, <c>combo</c>, <c>interrupted</c>); each habit an enemy learns as <see cref="HabitEvent"/>; each
    /// enemy death as <see cref="EnemyDeathEvent"/>; and on <see cref="TrackSummary"/> the numbers per attack and the
    /// best and worst chains as <see cref="SummaryEvent"/>s. <see cref="Fill"/> adds the game's ids (level, zone,
    /// entity, mechanic) to each event.
    /// </summary>
    public sealed class CombatTelemetry : IDisposable
    {
        public const string HitEvent = "combat_hit";
        public const string MissEvent = "combat_miss";
        public const string BlockedEvent = "combat_blocked";
        public const string ParriedEvent = "combat_parried";
        public const string DodgedEvent = "combat_dodged";
        public const string HabitEvent = "combat_habit_learnt";
        public const string EnemyDeathEvent = "combat_enemy_died";
        public const string SummaryEvent = "combat_summary";

        private readonly CombatAnalytics _analytics;
        private readonly ITelemetry _telemetry;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <param name="events">The fight's bus, for learnt habits and deaths; null: swings only.</param>
        public CombatTelemetry(CombatAnalytics analytics, ITelemetry telemetry, IEventBus events = null)
        {
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _analytics.SwingFinished += OnSwing;
            if (events != null)
            {
                _subscriptions.Add(events.Subscribe<PlayerHabitNoticedEvent>(OnHabit));
                _subscriptions.Add(events.Subscribe<EnemyDiedEvent>(OnDied));
            }
        }

        /// <summary>
        /// The design id of the combat mechanic, set on every event (<see cref="TelemetryEvent.MechanicId"/>).
        /// </summary>
        public string MechanicId { get; set; }

        /// <summary>
        /// The game adds its own ids or properties to an event before it is tracked (the level, the zone, the
        /// fighter's entity id).
        /// </summary>
        public Action<TelemetryEvent> Fill { get; set; }

        /// <summary>
        /// Which swings are sent (all by default; for example only the player's, when enemies are many).
        /// </summary>
        public Func<SwingOutcome, bool> Sends { get; set; }

        /// <summary>
        /// One <see cref="SummaryEvent"/> per attack (uses, hit, miss and interrupt rates, average distance, damage)
        /// and one per ranked chain (kind <c>best_combo</c> / <c>worst_combo</c>), for the end of a level or a session.
        /// </summary>
        /// <returns>The events tracked.</returns>
        public int TrackSummary(int minComboUses = 3, int combos = 3)
        {
            int tracked = 0;
            foreach (var attack in _analytics.Attacks)
            {
                var e = New(SummaryEvent)
                    .Set("kind", "attack")
                    .Set("attack", attack.AttackId ?? "")
                    .Set("uses", attack.Uses)
                    .Set("hit_rate", Round(attack.HitRate))
                    .Set("miss_rate", Round(attack.MissRate))
                    .Set("interrupt_rate", Round(attack.InterruptRate))
                    .Set("average_distance", Round(attack.AverageDistance))
                    .Set("damage", Round(attack.Damage));
                if (Track(e))
                    tracked++;
            }
            tracked += TrackCombos("best_combo", _analytics.Best(minComboUses), combos);
            tracked += TrackCombos("worst_combo", _analytics.Worst(minComboUses), combos);
            var total = New(SummaryEvent)
                .Set("kind", "total")
                .Set("uses", _analytics.Total.Uses)
                .Set("hit_rate", Round(_analytics.Total.HitRate))
                .Set("miss_rate", Round(_analytics.Total.MissRate))
                .Set("interrupt_rate", Round(_analytics.Total.InterruptRate))
                .Set("approaches", _analytics.Approaches)
                .Set("failed_approach_rate", Round(_analytics.FailedApproachRate));
            if (Track(total))
                tracked++;
            return tracked;
        }

        public void Dispose()
        {
            _analytics.SwingFinished -= OnSwing;
            foreach (var subscription in _subscriptions)
                subscription.Dispose();
            _subscriptions.Clear();
        }

        private int TrackCombos(string kind, List<ComboStats> ranked, int count)
        {
            int tracked = 0;
            for (int i = 0; i < ranked.Count && i < count; i++)
            {
                var combo = ranked[i];
                var e = New(SummaryEvent)
                    .Set("kind", kind)
                    .Set("chain", combo.Chain)
                    .Set("uses", combo.Uses)
                    .Set("hit_rate", Round(combo.HitRate))
                    .Set("damage", Round(combo.Damage));
                if (Track(e))
                    tracked++;
            }
            return tracked;
        }

        private void OnSwing(SwingOutcome swing)
        {
            if (Sends != null && !Sends(swing))
                return;
            var e = New(NameOf(swing.Result))
                .Set("attack", swing.AttackId ?? "")
                .Set("attacker", swing.AttackerId)
                .Set("target", swing.TargetId)
                .Set("damage", Round(swing.Damage))
                .Set("combo", swing.Combo)
                .Set("interrupted", swing.Interrupted);
            if (swing.Distance >= 0f)
                e.Set("distance", Round(swing.Distance));
            Track(e);
        }

        private void OnHabit(PlayerHabitNoticedEvent habit) =>
            Track(New(HabitEvent)
                .Set("enemy", habit.EnemyId)
                .Set("target", habit.TargetId)
                .Set("habit", habit.Habit.ToString())
                .Set("share", Round(habit.Share)));

        private void OnDied(EnemyDiedEvent died) =>
            Track(New(EnemyDeathEvent).Set("enemy", died.EnemyId));

        private TelemetryEvent New(string name) => new TelemetryEvent(name) { MechanicId = MechanicId };

        private bool Track(TelemetryEvent e)
        {
            Fill?.Invoke(e);
            return _telemetry.Track(e);
        }

        private static string NameOf(SwingResult result)
        {
            switch (result)
            {
                case SwingResult.Hit: return HitEvent;
                case SwingResult.Blocked: return BlockedEvent;
                case SwingResult.Parried: return ParriedEvent;
                case SwingResult.Dodged: return DodgedEvent;
                default: return MissEvent;
            }
        }

        private static double Round(float value) => Math.Round((double)value, 3);
    }
}
