using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.StatusEffects.Tests
{
    /// <summary>
    /// Proves StatusEffectManager publishes its events on a real event bus: applied on every apply (with the
    /// resulting stacks), tick for each DoT/HoT tick, expired plus removed when a duration runs out, and removed
    /// when effects are taken off manually.
    /// </summary>
    [TestFixture]
    public class StatusEffectEventTests
    {
        private EventBus _bus;
        private StatusEffectManager _manager;
        private int _entityId;

        private readonly List<EffectAppliedEvent> _applied = new List<EffectAppliedEvent>();
        private readonly List<EffectTickEvent> _ticks = new List<EffectTickEvent>();
        private readonly List<EffectExpiredEvent> _expired = new List<EffectExpiredEvent>();
        private readonly List<EffectRemovedEvent> _removed = new List<EffectRemovedEvent>();

        [SetUp]
        public void SetUp()
        {
            _applied.Clear();
            _ticks.Clear();
            _expired.Clear();
            _removed.Clear();

            _bus = new EventBus();
            _bus.Subscribe<EffectAppliedEvent>(e => _applied.Add(e));
            _bus.Subscribe<EffectTickEvent>(e => _ticks.Add(e));
            _bus.Subscribe<EffectExpiredEvent>(e => _expired.Add(e));
            _bus.Subscribe<EffectRemovedEvent>(e => _removed.Add(e));

            var entityManager = new EntityManager();
            _manager = new StatusEffectManager(entityManager, _bus);
            _entityId = entityManager.CreateEntity();

            _manager.RegisterEffect(StatusEffectData.CreateDoT("poison", "Poison", 10f, 1f, 5f));
            _manager.RegisterEffect(StatusEffectData.CreateHoT("regen", "Regeneration", 20f, 2f, 10f));
            _manager.RegisterEffect(StatusEffectData.CreateStatModifier(
                "haste", "Haste", EffectType.Buff, 15f, "MoveSpeed", 1.5f, ModifierOperation.Multiply));

            var stackable = StatusEffectData.CreateDoT("poison_stack", "Stackable Poison", 10f, 1f, 3f);
            stackable.IsStackable = true;
            stackable.MaxStacks = 5;
            _manager.RegisterEffect(stackable);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        [Test]
        public void Applying_PublishesEffectApplied()
        {
            Assert.That(_manager.ApplyEffect(_entityId, "poison", "caster", 2f), Is.True);

            Assert.That(_applied.Count, Is.EqualTo(1));
            var e = _applied[0];
            Assert.That(e.EntityId, Is.EqualTo(_entityId));
            Assert.That(e.EffectId, Is.EqualTo("poison"));
            Assert.That(e.SourceId, Is.EqualTo("caster"));
            Assert.That(e.StartTime, Is.EqualTo(2f));
            Assert.That(e.Stacks, Is.EqualTo(1));

            // Unknown effects are refused and publish nothing
            Assert.That(_manager.ApplyEffect(_entityId, "unknown", "caster", 2f), Is.False);
            Assert.That(_applied.Count, Is.EqualTo(1));
        }

        [Test]
        public void Stacking_PublishesEffectAppliedWithStackCount()
        {
            _manager.ApplyEffect(_entityId, "poison_stack", "caster", 0f);
            _manager.ApplyEffect(_entityId, "poison_stack", "caster", 1f);

            Assert.That(_applied.Count, Is.EqualTo(2));
            Assert.That(_applied[1].Stacks, Is.EqualTo(2));
            Assert.That(_applied[1].StartTime, Is.EqualTo(1f));
        }

        [Test]
        public void Ticking_PublishesEffectTick()
        {
            _manager.ApplyEffect(_entityId, "poison", "caster", 0f);
            _manager.ApplyEffect(_entityId, "regen", "caster", 0f);
            int callbacks = 0;

            _manager.ProcessTicks(_entityId, 1f, (id, effectId, damage, healing) => callbacks++);

            Assert.That(_ticks.Count, Is.EqualTo(1), "only poison is due at t=1");
            var e = _ticks[0];
            Assert.That(e.EntityId, Is.EqualTo(_entityId));
            Assert.That(e.EffectId, Is.EqualTo("poison"));
            Assert.That(e.Damage, Is.EqualTo(5f));
            Assert.That(e.Healing, Is.EqualTo(0f));
            Assert.That(e.TickTime, Is.EqualTo(1f));
            Assert.That(callbacks, Is.EqualTo(1), "the callback still runs alongside the event");

            _manager.ProcessTicks(_entityId, 2f);

            Assert.That(_ticks.Count, Is.EqualTo(3));
            var regen = _ticks.Find(t => t.EffectId == "regen");
            Assert.That(regen.Healing, Is.EqualTo(10f));
            Assert.That(regen.Damage, Is.EqualTo(0f));
        }

        [Test]
        public void Expiring_PublishesEffectExpired()
        {
            _manager.ApplyEffect(_entityId, "poison", "caster", 0f);
            _manager.ApplyEffect(_entityId, "haste", "caster", 0f);

            _manager.UpdateEffects(_entityId, 5f);
            Assert.That(_expired, Is.Empty);

            _manager.UpdateEffects(_entityId, 6f); // poison (10s) runs out, haste (15s) does not

            Assert.That(_expired.Count, Is.EqualTo(1));
            Assert.That(_expired[0].EntityId, Is.EqualTo(_entityId));
            Assert.That(_expired[0].EffectId, Is.EqualTo("poison"));
            Assert.That(_expired[0].ExpireTime, Is.EqualTo(0f));

            Assert.That(_removed.Count, Is.EqualTo(1));
            Assert.That(_removed[0].EffectId, Is.EqualTo("poison"));
            Assert.That(_removed[0].WasExpired, Is.True);
            Assert.That(_manager.HasEffect(_entityId, "poison"), Is.False);
        }

        [Test]
        public void Removing_PublishesEffectRemoved()
        {
            _manager.ApplyEffect(_entityId, "poison", "caster", 0f);

            Assert.That(_manager.RemoveEffect(_entityId, "poison"), Is.True);

            Assert.That(_removed.Count, Is.EqualTo(1));
            Assert.That(_removed[0].EntityId, Is.EqualTo(_entityId));
            Assert.That(_removed[0].EffectId, Is.EqualTo("poison"));
            Assert.That(_removed[0].WasExpired, Is.False);
            Assert.That(_removed[0].RemovalTime, Is.EqualTo(0f));
            Assert.That(_expired, Is.Empty);

            // Nothing left to remove: no second event
            Assert.That(_manager.RemoveEffect(_entityId, "poison"), Is.False);
            Assert.That(_removed.Count, Is.EqualTo(1));
        }

        [Test]
        public void RemovingAll_PublishesEffectRemovedForEach()
        {
            _manager.ApplyEffect(_entityId, "poison", "caster", 0f);
            _manager.ApplyEffect(_entityId, "haste", "caster", 0f);

            _manager.RemoveAllEffects(_entityId);

            Assert.That(_removed.Count, Is.EqualTo(2));
            Assert.That(_removed.ConvertAll(r => r.EffectId), Is.EquivalentTo(new[] { "poison", "haste" }));
            Assert.That(_removed.TrueForAll(r => !r.WasExpired), Is.True);
            Assert.That(_manager.GetActiveEffects(_entityId), Is.Empty);
        }
    }
}
