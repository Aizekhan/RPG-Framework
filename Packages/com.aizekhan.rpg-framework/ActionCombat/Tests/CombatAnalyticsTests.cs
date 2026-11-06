using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat.Tests
{
    // WhiteWrite UMCF "Combat analytics": hit and miss rates, attack distance, failed approaches, interruptions, the
    // best and worst combos, how often each attack is used
    public class CombatAnalyticsTests
    {
        private const int Player = 1;
        private const int Goblin = 10;

        private EventBus _bus;
        private CombatAnalytics _analytics;
        private int _swing;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _analytics = new CombatAnalytics(_bus);
        }

        [TearDown]
        public void TearDown() => _analytics.Dispose();

        // A swing from start to end with the given contacts
        private void Swing(int attacker, string attack, bool interrupted = false, float chosenFrom = -1f, bool combo = false,
            (ContactResult Result, float Damage)[] contacts = null)
        {
            int swing = ++_swing;
            _bus.Publish(new AttackStartedEvent(attacker, swing, attack, false, 0f));
            if (chosenFrom >= 0f)
                _bus.Publish(new AttackChosenEvent(attacker, Player, attack, chosenFrom, combo, 0f));
            foreach (var (result, damage) in contacts ?? new (ContactResult, float)[0])
                _bus.Publish(new ContactResolvedEvent(attacker, Player, swing, attack, result, ContactIgnored.None, damage, false,
                    CombatVector.Zero, 0f));
            _bus.Publish(new AttackEndedEvent(attacker, swing, attack, interrupted, 1f));
        }

        [Test]
        public void ItCountsEachAttacksHitsMissesInterruptionsAndDistance()
        {
            var finished = new List<SwingOutcome>();
            _analytics.SwingFinished += finished.Add;

            Swing(Goblin, "slash", chosenFrom: 1.5f, contacts: new[] { (ContactResult.Hit, 10f) });
            Swing(Goblin, "slash", chosenFrom: 2.5f);
            Swing(Goblin, "slash", interrupted: true, chosenFrom: 2f, contacts: new[] { (ContactResult.Dodged, 0f), (ContactResult.Blocked, 3f) });
            Swing(Player, "jab", contacts: new[] { (ContactResult.Ignored, 0f) });

            var slash = _analytics.Of("slash");
            Assert.That(slash.Uses, Is.EqualTo(3));
            Assert.That(slash.HitRate, Is.EqualTo(1f / 3f).Within(1e-5f));
            Assert.That(slash.MissRate, Is.EqualTo(1f / 3f).Within(1e-5f));
            Assert.That(slash.Blocked, Is.EqualTo(1), "a block beats a dodge in the same swing");
            Assert.That(slash.InterruptRate, Is.EqualTo(1f / 3f).Within(1e-5f));
            Assert.That(slash.AverageDistance, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(slash.Damage, Is.EqualTo(13f));
            Assert.That(_analytics.Of("jab").Misses, Is.EqualTo(1), "an ignored contact is no contact");
            Assert.That(_analytics.Of("jab").AverageDistance, Is.Zero, "no enemy chose it");
            Assert.That(_analytics.Total.Uses, Is.EqualTo(4));
            Assert.That(_analytics.Attacks.Select(a => a.AttackId), Is.EqualTo(new[] { "slash", "jab" }));
            Assert.That(finished.Select(f => f.Result), Is.EqualTo(new[] { SwingResult.Hit, SwingResult.Miss, SwingResult.Blocked, SwingResult.Miss }));
            Assert.That(finished[0].TargetId, Is.EqualTo(Player));
            Assert.That(finished[3].Distance, Is.EqualTo(-1f));
        }

        [Test]
        public void EnemyChains_AreRanked_AndTokensWithoutAnAttackAreFailedApproaches()
        {
            for (int i = 0; i < 3; i++)
            {
                _bus.Publish(new AttackTokenGrantedEvent(Goblin, Player, 0f));
                Swing(Goblin, "slash", chosenFrom: 1f, contacts: new[] { (ContactResult.Hit, 5f) });
                Swing(Goblin, "overhead", chosenFrom: 1f, combo: true, contacts: new[] { (ContactResult.Hit, 8f) });
                _bus.Publish(new AttackTokenReleasedEvent(Goblin, Player, "attack over", 0f));

                _bus.Publish(new AttackTokenGrantedEvent(Goblin, Player, 0f));
                Swing(Goblin, "lunge", chosenFrom: 4f);
                _bus.Publish(new AttackTokenReleasedEvent(Goblin, Player, "combo missed", 0f));
            }
            _bus.Publish(new AttackTokenGrantedEvent(Goblin, Player, 0f));
            _bus.Publish(new AttackTokenReleasedEvent(Goblin, Player, "no move ready", 0f));

            Assert.That(_analytics.Best().Select(c => c.Chain), Is.EqualTo(new[] { "slash > overhead", "lunge" }));
            Assert.That(_analytics.Worst().First().Chain, Is.EqualTo("lunge"));
            var combo = _analytics.Combos.First();
            Assert.That(combo.Uses, Is.EqualTo(3));
            Assert.That(combo.Swings, Is.EqualTo(6));
            Assert.That(combo.HitRate, Is.EqualTo(1f));
            Assert.That(combo.Damage, Is.EqualTo(39f));
            Assert.That(_analytics.Best(4), Is.Empty);
            Assert.That(_analytics.Approaches, Is.EqualTo(7));
            Assert.That(_analytics.FailedApproaches, Is.EqualTo(1));
            Assert.That(_analytics.FailedApproachRate, Is.EqualTo(1f / 7f).Within(1e-5f));
        }

        [Test]
        public void OnlyTheAttackersItCounts_AreCounted_AndResetForgetsAll()
        {
            _analytics.Counts = id => id == Player;
            Swing(Goblin, "slash", chosenFrom: 1f, contacts: new[] { (ContactResult.Hit, 5f) });
            Swing(Player, "jab", contacts: new[] { (ContactResult.Hit, 4f) });

            Assert.That(_analytics.Of("slash"), Is.Null);
            Assert.That(_analytics.Total.Hits, Is.EqualTo(1));

            _analytics.Reset();
            Assert.That(_analytics.Total.Uses, Is.Zero);
            Assert.That(_analytics.Attacks, Is.Empty);
        }

        [Test]
        public void ItWorksOnARealFight()
        {
            var world = new SingleTargetWorld();
            var rules = new CombatRules(new AnyDamage(), _bus);
            using (var director = new EnemyDirector(rules, world, _bus, seed: 3) { SweepAttacks = true })
            {
                rules.Add(Player);
                rules.Add(Goblin);
                var behaviour = new EnemyBehaviour { NoticeSeconds = 0f, RecoveryPause = 0.2f };
                var slash = new AttackDefinition("slash", 0.2f, 0.2f, 0.2f) { Sweep = new[] { new SweepSegment(0f, 1f, -60f, 60f, 2f) } };
                behaviour.Moves.Add(new EnemyMove(slash, 0f, 2f));
                director.Add(Goblin, behaviour, CombatVector.Zero);
                rules.SetPose(Goblin, CombatVector.Zero, new CombatVector(0f, 0f, 1f));
                for (int i = 0; i < 100; i++)
                {
                    rules.Tick(0.05f);
                    director.Tick();
                }
            }

            var stats = _analytics.Of("slash");
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.Uses, Is.GreaterThan(1));
            Assert.That(stats.Hits, Is.GreaterThan(0));
            Assert.That(_analytics.Approaches, Is.GreaterThan(0));
        }

        private sealed class SingleTargetWorld : IEnemyWorld
        {
            private static readonly CombatVector Forward = new CombatVector(0f, 0f, 1f);

            public IEnumerable<int> TargetsOf(int enemyId) => new[] { Player };

            public bool TryGetPose(int entityId, out CombatVector position, out CombatVector forward)
            {
                position = entityId == Player ? new CombatVector(0f, 0f, 1.2f) : CombatVector.Zero;
                forward = Forward;
                return entityId == Player || entityId == Goblin;
            }

            public bool CanSee(int fromId, int toId) => true;
            public float HealthShare(int entityId) => 1f;
            public bool IsAlive(int entityId) => true;
        }

        private sealed class AnyDamage : ICombatDamage
        {
            public bool CanHit(int attackerId, int targetId) => true;
            public float Deal(int attackerId, int targetId, AttackContext context) => 1f;
        }
    }
}
