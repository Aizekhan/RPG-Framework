using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Abilities.Tests
{
    /// <summary>
    /// A channel with a tick interval calls its effect on landing and on every tick up to its end (no tick after it),
    /// says when it ends, run out or stopped; charges with a recharge time come back one at a time.
    /// </summary>
    public sealed class AbilityChannelAndChargeTests
    {
        private EventBus _bus;
        private readonly List<string> _log = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _bus = new EventBus();
            _bus.Subscribe<AbilityChannelTickEvent>(e => _log.Add($"tick {e.Tick} at {e.Timestamp}"));
            _bus.Subscribe<AbilityChannelEndedEvent>(e => _log.Add($"ended {e.Completed} {e.Reason} after {e.Ticks}"));
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private AbilityExecutor Executor(params IAbility[] abilities)
        {
            var registry = new AbilityRegistry();
            foreach (var ability in abilities)
                registry.RegisterAbility(ability);
            return new AbilityExecutor(registry, new CooldownTracker(), new TargetValidator(), _bus);
        }

        private static IAbility Beam() =>
            new Ability(new AbilityMetadata("beam", "Beam", AbilityType.Channeled),
                castInfo: new CastInfo(0f, channelDuration: 2f) { ChannelTickInterval = 0.5f });

        [Test]
        public void AChannel_TicksItsEffect_UpToItsEnd()
        {
            var executor = Executor(Beam());
            int hits = 0;

            executor.ExecuteAbility("beam", 1, 2, 0f, null, null, (caster, target, id) => hits++);
            Assert.That(hits, Is.EqualTo(1), "it lands when the channel starts");
            Assert.That(executor.IsChanneling(1), Is.True);

            executor.Update(1.2f);
            Assert.That(hits, Is.EqualTo(3), "ticks at 0.5 and 1.0");
            executor.Update(5f);
            Assert.That(hits, Is.EqualTo(5), "ticks at 1.5 and 2.0, none after the end");
            Assert.That(executor.IsChanneling(1), Is.False);
            Assert.That(_log, Is.EqualTo(new[] { "tick 1 at 0.5", "tick 2 at 1", "tick 3 at 1.5", "tick 4 at 2", "ended True  after 4" }));
        }

        [Test]
        public void AStoppedChannel_LandsTheTicksDueAndSaysWhy()
        {
            var executor = Executor(Beam());
            int hits = 0;
            executor.ExecuteAbility("beam", 1, 2, 0f, null, null, (caster, target, id) => hits++);

            executor.InterruptCast(1, 1.1f, "stunned");

            Assert.That(hits, Is.EqualTo(3));
            Assert.That(_log, Is.EqualTo(new[] { "tick 1 at 0.5", "tick 2 at 1", "ended False stunned after 2" }));
        }

        [Test]
        public void Charges_ComeBackOneAtATime()
        {
            var dash = new Ability(new AbilityMetadata("dash", "Dash", AbilityType.Active, maxCharges: 2) { ChargeRechargeTime = 3f });
            var executor = Executor(dash);

            Assert.That(executor.ExecuteAbility("dash", 1, 1, 0f, null, null, null).Success, Is.True);
            Assert.That(executor.ExecuteAbility("dash", 1, 1, 1f, null, null, null).Success, Is.True);
            Assert.That(executor.ExecuteAbility("dash", 1, 1, 2f, null, null, null).Success, Is.False, "both used");

            Assert.That(executor.CanExecuteAbility("dash", 1, 3f), Is.True, "the first back 3 s after it went");
            Assert.That(executor.ExecuteAbility("dash", 1, 1, 3.5f, null, null, null).Success, Is.True);
            Assert.That(executor.CanExecuteAbility("dash", 1, 5.9f), Is.False, "the second is on its way");
            Assert.That(executor.CanExecuteAbility("dash", 1, 6f), Is.True);

            var instance = new AbilityInstance(dash, 7);
            instance.CompleteCast(10f);
            Assert.That(instance.NextChargeIn(11f), Is.EqualTo(2f));
            instance.RefreshCharges(13f);
            Assert.That(instance.RemainingCharges, Is.EqualTo(2));
            Assert.That(instance.NextChargeIn(13f), Is.EqualTo(0f), "full: none on its way");
        }
    }
}
