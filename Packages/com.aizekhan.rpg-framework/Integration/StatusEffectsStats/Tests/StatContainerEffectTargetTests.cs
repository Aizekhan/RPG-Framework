using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Stats;
using RPGFramework.StatusEffects;

namespace RPGFramework.Integration.StatusEffectsStats.Tests
{
    /// <summary>
    /// Proves effects reach a real StatContainer component through the manager: stat modifiers change final stats
    /// while the effect is on and follow its stacks, resource ticks change base values, and nothing is left behind.
    /// </summary>
    [TestFixture]
    public class StatContainerEffectTargetTests
    {
        private enum Stat { AttackPower, Defense, Mana }

        private EntityManager _entities;
        private StatusEffectManager _manager;
        private int _entity;

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _manager = new StatusEffectManager(_entities, null, new StatContainerEffectTarget<Stat>(_entities));
            _entity = _entities.CreateEntity();

            var stats = new StatContainer<Stat, float>();
            stats.Initialize();
            stats.SetBaseStat(Stat.AttackPower, 100f);
            stats.SetBaseStat(Stat.Defense, 50f);
            stats.SetBaseStat(Stat.Mana, 80f);
            _entities.AddComponent(_entity, stats);
        }

        private float Final(Stat stat) => _entities.GetComponentRef<StatContainer<Stat, float>>(_entity).GetFinalStat(stat);
        private float Base(Stat stat) => _entities.GetComponentRef<StatContainer<Stat, float>>(_entity).GetBaseStat(stat);

        [Test]
        public void StatEffects_ChangeFinalStats_WhileOn()
        {
            var might = StatusEffectData.CreateStatModifier("might", "Might", EffectType.Buff, 10f, "AttackPower", 1.5f,
                ModifierOperation.Multiply);
            might.IsStackable = true;
            might.MaxStacks = 2;
            _manager.RegisterEffect(might);
            _manager.RegisterEffect(StatusEffectData.CreateStatModifier("armor", "Armor", EffectType.Buff, 5f, "Defense", 10f));

            _manager.ApplyEffect(_entity, "might", "caster", 0f);
            _manager.ApplyEffect(_entity, "armor", "caster", 0f);
            Assert.That(Final(Stat.AttackPower), Is.EqualTo(150f).Within(1e-3f));
            Assert.That(Final(Stat.Defense), Is.EqualTo(60f));

            _manager.ApplyEffect(_entity, "might", "caster", 0f);
            Assert.That(Final(Stat.AttackPower), Is.EqualTo(225f).Within(1e-3f), "x1.5 twice");

            _manager.UpdateEffects(_entity, 6f); // armor runs out
            Assert.That(Final(Stat.Defense), Is.EqualTo(50f));

            _manager.RemoveEffect(_entity, "might");
            Assert.That(Final(Stat.AttackPower), Is.EqualTo(100f));
        }

        [Test]
        public void ResourceTicks_ChangeTheBaseValue()
        {
            var drain = StatusEffectData.Create("drain", "Drain", EffectType.Debuff);
            drain.Duration = 10f;
            drain.TickInterval = 1f;
            drain.Modifiers.Add(EffectModifierData.Create(ModifierType.Resource, "Mana", 0.5f, ModifierOperation.Multiply));
            _manager.RegisterEffect(drain);
            _manager.ApplyEffect(_entity, "drain", "caster", 0f);

            _manager.ProcessTicks(_entity, 1f);

            Assert.That(Base(Stat.Mana), Is.EqualTo(40f));
        }

        [Test]
        public void UnknownStatNames_AreIgnored()
        {
            _manager.RegisterEffect(StatusEffectData.CreateStatModifier("odd", "Odd", EffectType.Buff, 5f, "Luck", 10f));

            Assert.That(_manager.ApplyEffect(_entity, "odd", "caster", 0f), Is.True);
            Assert.That(_manager.RemoveEffect(_entity, "odd"), Is.True);
            Assert.That(Final(Stat.AttackPower), Is.EqualTo(100f));
        }
    }
}
