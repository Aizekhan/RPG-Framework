using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Abilities.Tests
{
    /// <summary>
    /// Per-second costs: a toggle pays its upkeep while it is on and switches off by itself when the caster runs dry,
    /// a channel pays while it lasts and ends when it cannot; the upfront part is paid once.
    /// </summary>
    public sealed class AbilityUpkeepTests
    {
        private EventBus _bus;
        private readonly List<string> _log = new List<string>();
        private float _mana;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _bus = new EventBus();
            _bus.Subscribe<AbilityToggledEvent>(e => _log.Add($"{(e.IsOn ? "on" : "off")} at {e.Timestamp} {e.Reason}".TrimEnd()));
            _bus.Subscribe<AbilityChannelEndedEvent>(e => _log.Add($"ended {e.Completed} {e.Reason}"));
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private AbilityExecutor Executor(IAbility ability)
        {
            var registry = new AbilityRegistry();
            registry.RegisterAbility(ability);
            return new AbilityExecutor(registry, new CooldownTracker(), new TargetValidator(), _bus);
        }

        private bool Has(int entity, string resource, float amount) => _mana >= amount - 1e-4f;
        private void Take(int entity, string resource, float amount) => _mana -= amount;

        [Test]
        public void AToggle_PaysItsUpkeepWhileOn_AndSwitchesOffWhenDry()
        {
            var aura = new Ability(new AbilityMetadata("aura", "Aura", AbilityType.Toggle),
                new List<ResourceCost> { new ResourceCost("MP", 10f), new ResourceCost("MP", 4f, perSecond: true) });
            var executor = Executor(aura);
            _mana = 30f;

            Assert.That(executor.ExecuteAbility("aura", 1, 1, 0f, Has, Take, null).Success, Is.True);
            Assert.That(_mana, Is.EqualTo(20f), "the upfront 10; the upkeep is not paid ahead");

            executor.Update(2f);
            Assert.That(_mana, Is.EqualTo(12f).Within(1e-4f), "two seconds of 4");
            Assert.That(executor.IsToggledOn(1, "aura"), Is.True);

            executor.Update(6f);
            Assert.That(executor.IsToggledOn(1, "aura"), Is.False, "16 more could not be paid");
            Assert.That(_mana, Is.EqualTo(12f).Within(1e-4f), "nothing taken for the unpaid seconds");
            Assert.That(_log, Is.EqualTo(new[] { "on at 0", "off at 6 Insufficient MP" }));

            _mana = 30f;
            executor.ExecuteAbility("aura", 1, 1, 10f, Has, Take, null);
            executor.ExecuteAbility("aura", 1, 1, 11.5f, Has, Take, null);
            Assert.That(executor.IsToggledOn(1, "aura"), Is.False);
            Assert.That(_mana, Is.EqualTo(14f).Within(1e-4f), "10 upfront, then 1.5 seconds paid at the switch");
            executor.Update(20f);
            Assert.That(_mana, Is.EqualTo(14f).Within(1e-4f), "off: no more upkeep");
        }

        [Test]
        public void AChannel_PaysWhileItLasts_AndEndsWhenItCannot()
        {
            var drain = new Ability(new AbilityMetadata("drain", "Drain", AbilityType.Channeled),
                new List<ResourceCost> { new ResourceCost("MP", 5f, perSecond: true) },
                new CastInfo(0f, channelDuration: 4f));
            var executor = Executor(drain);

            _mana = 100f;
            executor.ExecuteAbility("drain", 1, 2, 0f, Has, Take, null);
            executor.Update(10f);
            Assert.That(_mana, Is.EqualTo(80f).Within(1e-4f), "four seconds, none after the end");

            _mana = 12f;
            executor.ExecuteAbility("drain", 1, 2, 20f, Has, Take, null);
            executor.Update(22f);
            Assert.That(executor.IsChanneling(1), Is.True);
            executor.Update(23f);
            Assert.That(executor.IsChanneling(1), Is.False, "the third second could not be paid");
            Assert.That(_mana, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(_log[_log.Count - 1], Is.EqualTo("ended False Insufficient MP"));

            _mana = 3f;
            Assert.That(executor.ExecuteAbility("drain", 1, 2, 30f, Has, Take, null).Success, Is.False, "starting needs a second's worth");
        }
    }
}
