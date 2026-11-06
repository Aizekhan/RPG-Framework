using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ActionCombat;
using RPGFramework.Events;
using RPGFramework.Telemetry;

namespace RPGFramework.Integration.ActionCombatTelemetry.Tests
{
    // WhiteWrite UMCF "Combat analytics" reaches the game's telemetry
    public class CombatTelemetryTests
    {
        private const int Player = 1;
        private const int Goblin = 10;

        private EventBus _bus;
        private CombatAnalytics _analytics;
        private RecordingTelemetry _telemetry;
        private CombatTelemetry _bridge;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _analytics = new CombatAnalytics(_bus);
            _telemetry = new RecordingTelemetry();
            _bridge = new CombatTelemetry(_analytics, _telemetry, _bus) { MechanicId = "umcf" };
        }

        [TearDown]
        public void TearDown()
        {
            _bridge.Dispose();
            _analytics.Dispose();
        }

        private void Swing(int swing, string attack, ContactResult? contact, float chosenFrom = -1f)
        {
            _bus.Publish(new AttackStartedEvent(Goblin, swing, attack, false, 0f));
            if (chosenFrom >= 0f)
                _bus.Publish(new AttackChosenEvent(Goblin, Player, attack, chosenFrom, false, 0f));
            if (contact.HasValue)
                _bus.Publish(new ContactResolvedEvent(Goblin, Player, swing, attack, contact.Value, ContactIgnored.None, 7f, false,
                    CombatVector.Zero, 0f));
            _bus.Publish(new AttackEndedEvent(Goblin, swing, attack, false, 1f));
        }

        [Test]
        public void EachSwingHabitAndDeath_IsAnEvent_WithTheGamesIds()
        {
            _bridge.Fill = e => e.LevelId = "L1";
            Swing(1, "slash", ContactResult.Hit, chosenFrom: 1.25f);
            Swing(2, "slash", null);
            Swing(3, "slash", ContactResult.Parried);
            _bus.Publish(new PlayerHabitNoticedEvent(Goblin, Player, PlayerHabit.Blocks, 0.4f, 0f));
            _bus.Publish(new EnemyDiedEvent(Goblin, 0, 0f));

            var names = _telemetry.Events.Select(e => e.Name).ToList();
            Assert.That(names, Is.EqualTo(new[]
            {
                CombatTelemetry.HitEvent, CombatTelemetry.MissEvent, CombatTelemetry.ParriedEvent, CombatTelemetry.HabitEvent,
                CombatTelemetry.EnemyDeathEvent
            }));
            var hit = _telemetry.Events[0];
            Assert.That(hit.Get("attack"), Is.EqualTo("slash"));
            Assert.That(hit.Get("damage"), Is.EqualTo(7.0));
            Assert.That(hit.Get("distance"), Is.EqualTo(1.25));
            Assert.That(hit.MechanicId, Is.EqualTo("umcf"));
            Assert.That(hit.LevelId, Is.EqualTo("L1"));
            Assert.That(_telemetry.Events[1].Get("distance"), Is.Null, "no enemy chose it");
            Assert.That(_telemetry.Events[3].Get("habit"), Is.EqualTo("Blocks"));
        }

        [Test]
        public void ASummary_HasEachAttackTheChainsAndTheTotal()
        {
            _bridge.Sends = swing => false;
            for (int i = 0; i < 3; i++)
            {
                _bus.Publish(new AttackTokenGrantedEvent(Goblin, Player, 0f));
                Swing(i + 1, "slash", ContactResult.Hit, chosenFrom: 1f);
                _bus.Publish(new AttackTokenReleasedEvent(Goblin, Player, "attack over", 0f));
            }

            Assert.That(_telemetry.Events, Is.Empty, "swings filtered out");
            int tracked = _bridge.TrackSummary(minComboUses: 3, combos: 1);

            Assert.That(tracked, Is.EqualTo(4), "the attack, best and worst chain, the total");
            var kinds = _telemetry.Events.Select(e => e.Get("kind")).ToList();
            Assert.That(kinds, Is.EqualTo(new object[] { "attack", "best_combo", "worst_combo", "total" }));
            Assert.That(_telemetry.Events[0].Get("hit_rate"), Is.EqualTo(1.0));
            Assert.That(_telemetry.Events[1].Get("chain"), Is.EqualTo("slash"));
            Assert.That(_telemetry.Events[3].Get("approaches"), Is.EqualTo(3L));
        }

        private sealed class RecordingTelemetry : ITelemetry
        {
            public readonly List<TelemetryEvent> Events = new List<TelemetryEvent>();

            public bool Consent { get; set; } = true;
            public event System.Action<bool> ConsentChanged { add { } remove { } }
            public string SessionId => "s";
            public string PlayerId { get; set; }
            public string Mode { get; set; }
            public string Cohort { get; set; }
            public string Variant { get; set; }

            public bool Track(TelemetryEvent telemetryEvent)
            {
                Events.Add(telemetryEvent);
                return true;
            }

            public bool Track(string name) => Track(new TelemetryEvent(name));
            public bool SamplePosition(TelemetryPosition position, string levelId = null, string zoneId = null) => false;
            public bool AnswerSurvey(string playtest, string question, string answer) => false;
            public void StartSession() { }
            public void EndSession() { }
            public void Flush() { }
            public void Update(float deltaTime) { }
        }
    }
}
