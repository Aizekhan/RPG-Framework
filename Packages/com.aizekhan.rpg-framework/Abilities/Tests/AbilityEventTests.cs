using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Abilities.Tests
{
    public sealed class AbilityEventTests
    {
        private EventBus _bus;
        private readonly List<string> _log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _bus = new EventBus();
            _bus.Subscribe<AbilityExecutedEvent>(e => _log.Add($"executed {e.AbilityId} {e.Success}"));
            _bus.Subscribe<AbilityCanceledEvent>(e => _log.Add($"canceled {e.AbilityId}"));
            _bus.Subscribe<CooldownStartedEvent>(e => _log.Add($"cooldown {e.AbilityId} {e.Duration}"));
            _bus.Subscribe<CooldownExpiredEvent>(e => _log.Add($"expired {e.AbilityId} {e.EntityId}"));
        }

        private AbilityExecutor CreateExecutor()
        {
            var registry = new AbilityRegistry();
            registry.RegisterAbility(new Ability(new AbilityMetadata("bolt", "Bolt", AbilityType.Active, cooldown: 2f)));
            registry.RegisterAbility(new Ability(new AbilityMetadata("heal", "Heal", AbilityType.Active, cooldown: 0f),
                castInfo: new CastInfo(1.5f)));
            return new AbilityExecutor(registry, new CooldownTracker(), new TargetValidator(), _bus);
        }

        [Test]
        public void Executor_PublishesExecutionCooldownFailureAndCancel()
        {
            var executor = CreateExecutor();

            executor.ExecuteAbility("bolt", 1, 2, 0f, null, null, null);
            executor.ExecuteAbility("bolt", 1, 2, 0.5f, null, null, null); // on cooldown
            executor.ExecuteAbility("heal", 1, 1, 1f, null, null, null);   // starts casting
            executor.CancelCast(1);

            CollectionAssert.AreEqual(new[]
            {
                "executed bolt True",
                "cooldown bolt 2",
                "executed bolt False",
                "canceled heal"
            }, _log);
        }

        [Test]
        public void CooldownTracker_PublishesStartAndExpiry()
        {
            var tracker = new CooldownTracker(_bus);
            tracker.StartCooldown(3, "dash", 1f, 10f);

            Assert.That(tracker.CollectExpired(10.5f), Is.EqualTo(0));
            Assert.That(tracker.CollectExpired(11f), Is.EqualTo(1));
            Assert.That(tracker.IsOnCooldown(3, "dash", 11f), Is.False);

            CollectionAssert.AreEqual(new[] { "cooldown dash 1", "expired dash 3" }, _log);
        }
    }
}
