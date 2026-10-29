using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ECS;

namespace RPGFramework.StatusEffects.Tests
{
    /// <summary>
    /// Proves an effect does what its modifiers say: stat modifiers reach the game on apply, follow stack changes and
    /// come off on removal or expiry; resource, damage and healing ticks honour the operation; movement and control
    /// are combined across effects; and a second effect with a taken id is refused.
    /// </summary>
    [TestFixture]
    public class StatusEffectApplyTests
    {
        private EntityManager _entities;
        private RecordingTarget _target;
        private StatusEffectManager _manager;
        private int _entity;

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _target = new RecordingTarget();
            _manager = new StatusEffectManager(_entities, null, _target);
            _entity = _entities.CreateEntity();
        }

        private static StatusEffectData Effect(string id, float duration, params EffectModifierData[] modifiers)
        {
            var data = StatusEffectData.Create(id, id, EffectType.Buff);
            data.Duration = duration;
            data.Modifiers.AddRange(modifiers);
            return data;
        }

        private static EffectModifierData Mod(ModifierType type, string targetId, float value, ModifierOperation op)
        {
            return EffectModifierData.Create(type, targetId, value, op);
        }

        // ---- Registration ----

        [Test]
        public void RegisterEffect_SecondEffectWithSameId_IsRefused_AndFirstIsKept()
        {
            Assert.That(_manager.RegisterEffect(Effect("haste", 10f)), Is.True);
            Assert.That(_manager.RegisterEffect(Effect("haste", 99f)), Is.False);

            Assert.That(_manager.GetEffect("haste").Duration, Is.EqualTo(10f));
        }

        [Test]
        public void RegisterEffect_NullOrNoId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.RegisterEffect((IStatusEffect)null));
            Assert.Throws<ArgumentNullException>(() => _manager.RegisterEffect((StatusEffectData)null));
            Assert.Throws<ArgumentException>(() => _manager.RegisterEffect(Effect("", 1f)));
        }

        // ---- Stat modifiers ----

        [Test]
        public void StatModifier_IsPutOnWhenApplied_AndTakenOffWhenRemoved()
        {
            _manager.RegisterEffect(Effect("might", 10f, Mod(ModifierType.Stat, "AttackPower", 1.5f, ModifierOperation.Multiply)));

            _manager.ApplyEffect(_entity, "might", "caster", 0f);

            Assert.That(_target.Stats.Count, Is.EqualTo(1));
            var stat = _target.Stats[StatusEffectManager.StatModifierId("might", 0)];
            Assert.That(stat.StatId, Is.EqualTo("AttackPower"));
            Assert.That(stat.Operation, Is.EqualTo(ModifierOperation.Multiply));
            Assert.That(stat.Value, Is.EqualTo(1.5f));

            _manager.RemoveEffect(_entity, "might");

            Assert.That(_target.Stats, Is.Empty);
        }

        [Test]
        public void StatModifier_IsTakenOffWhenTheEffectExpires()
        {
            _manager.RegisterEffect(Effect("might", 10f, Mod(ModifierType.Stat, "AttackPower", 5f, ModifierOperation.Add)));
            _manager.ApplyEffect(_entity, "might", "caster", 0f);

            _manager.UpdateEffects(_entity, 5f);
            Assert.That(_target.Stats.Count, Is.EqualTo(1));

            _manager.UpdateEffects(_entity, 5f);
            Assert.That(_target.Stats, Is.Empty);
        }

        [Test]
        public void StatModifier_FollowsTheStackCount()
        {
            var add = Effect("rage", 10f,
                Mod(ModifierType.Stat, "AttackPower", 5f, ModifierOperation.Add),
                Mod(ModifierType.Stat, "Defense", 0.9f, ModifierOperation.Multiply));
            add.IsStackable = true;
            add.MaxStacks = 3;
            _manager.RegisterEffect(add);

            _manager.ApplyEffect(_entity, "rage", "caster", 0f);
            _manager.ApplyEffect(_entity, "rage", "caster", 0f);
            _manager.ApplyEffect(_entity, "rage", "caster", 0f);
            _manager.ApplyEffect(_entity, "rage", "caster", 0f); // capped at 3

            Assert.That(_target.Stats.Count, Is.EqualTo(2), "one modifier per authored stat modifier, not per stack");
            Assert.That(_target.Stats[StatusEffectManager.StatModifierId("rage", 0)].Value, Is.EqualTo(15f));
            Assert.That(_target.Stats[StatusEffectManager.StatModifierId("rage", 1)].Value, Is.EqualTo(0.729f).Within(1e-5f));
        }

        [Test]
        public void Power_ScalesModifiersLikeStacks_AndDurationOverridesTheAuthoredOne()
        {
            _manager.RegisterEffect(Effect("might", 10f,
                Mod(ModifierType.Stat, "AttackPower", 5f, ModifierOperation.Add),
                Mod(ModifierType.Stat, "Defense", 0.9f, ModifierOperation.Multiply)));

            Assert.That(_manager.ApplyEffect(_entity, "might", "caster", 0f, power: 2f, duration: 3f), Is.True);

            var instance = _manager.GetActiveEffects(_entity).Single();
            Assert.That(instance.Power, Is.EqualTo(2f));
            Assert.That(instance.RemainingDuration, Is.EqualTo(3f));
            Assert.That(_target.Stats[StatusEffectManager.StatModifierId("might", 0)].Value, Is.EqualTo(10f));
            Assert.That(_target.Stats[StatusEffectManager.StatModifierId("might", 1)].Value, Is.EqualTo(0.81f).Within(1e-5f));

            _manager.UpdateEffects(_entity, 3f);
            Assert.That(_target.Stats, Is.Empty, "it lasted 3 seconds, not the authored 10");
        }

        [Test]
        public void Power_ReapplyingKeepsTheStronger_AndThePlainApplyIsPowerOne()
        {
            _manager.RegisterEffect(Effect("might", 10f, Mod(ModifierType.Stat, "AttackPower", 5f, ModifierOperation.Add)));

            _manager.ApplyEffect(_entity, "might", "caster", 0f);
            Assert.That(_manager.GetActiveEffects(_entity).Single().Power, Is.EqualTo(1f));

            _manager.ApplyEffect(_entity, "might", "caster", 0f, power: 3f);
            _manager.ApplyEffect(_entity, "might", "caster", 0f, power: 0.5f, duration: 4f);

            var instance = _manager.GetActiveEffects(_entity).Single();
            Assert.That(instance.Power, Is.EqualTo(3f));
            Assert.That(instance.RemainingDuration, Is.EqualTo(4f), "the new application sets how long it lasts");
            Assert.That(_target.Stats[StatusEffectManager.StatModifierId("might", 0)].Value, Is.EqualTo(15f));
        }

        [Test]
        public void Power_InvalidValuesCountAsOne_AndAnInvalidDurationThrows()
        {
            _manager.RegisterEffect(Effect("might", 10f, Mod(ModifierType.Stat, "AttackPower", 5f, ModifierOperation.Add)));

            _manager.ApplyEffect(_entity, "might", "caster", 0f, power: float.NaN);

            Assert.That(_manager.GetActiveEffects(_entity).Single().Power, Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _manager.ApplyEffect(_entity, "might", "caster", 0f, 1f, float.PositiveInfinity));
        }

        [Test]
        public void RemoveAllEffects_TakesOffEveryStatModifier()
        {
            _manager.RegisterEffect(Effect("a", 10f, Mod(ModifierType.Stat, "AttackPower", 5f, ModifierOperation.Add)));
            _manager.RegisterEffect(Effect("b", 10f, Mod(ModifierType.Stat, "Defense", 100f, ModifierOperation.Override)));
            _manager.ApplyEffect(_entity, "a", "caster", 0f);
            _manager.ApplyEffect(_entity, "b", "caster", 0f);
            Assert.That(_target.Stats.Count, Is.EqualTo(2));

            _manager.RemoveAllEffects(_entity);

            Assert.That(_target.Stats, Is.Empty);
        }

        [Test]
        public void RemoveAllEffects_OnAnEntityThatIsGone_DoesNothing()
        {
            int gone = _entities.CreateEntity();
            _entities.DestroyEntity(gone);

            Assert.DoesNotThrow(() => _manager.RemoveAllEffects(gone));
            Assert.That(_entities.Exists(gone), Is.False);
        }

        // ---- Resources ----

        private void RegisterTicking(string id, float tickInterval, EffectModifierData modifier, int maxStacks = 1)
        {
            var data = Effect(id, 10f, modifier);
            data.TickInterval = tickInterval;
            data.IsStackable = maxStacks > 1;
            data.MaxStacks = maxStacks;
            _manager.RegisterEffect(data);
        }

        [Test]
        public void ResourceAdd_ChangesTheResourceEveryTick_PerStack()
        {
            RegisterTicking("drain", 1f, Mod(ModifierType.Resource, "Mana", -4f, ModifierOperation.Add), maxStacks: 3);
            _target.Resources["Mana"] = 100f;
            _manager.ApplyEffect(_entity, "drain", "caster", 0f);
            _manager.ApplyEffect(_entity, "drain", "caster", 0f);

            _manager.ProcessTicks(_entity, 0.5f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(100f), "no tick before the interval");

            _manager.ProcessTicks(_entity, 1f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(92f));
        }

        [Test]
        public void ResourceAdd_ScalesWithPower()
        {
            RegisterTicking("drain", 1f, Mod(ModifierType.Resource, "Mana", -4f, ModifierOperation.Add));
            _target.Resources["Mana"] = 100f;
            _manager.ApplyEffect(_entity, "drain", "caster", 0f, power: 2.5f);

            _manager.ProcessTicks(_entity, 1f);

            Assert.That(_target.Resources["Mana"], Is.EqualTo(90f));
        }

        [Test]
        public void ResourceMultiply_ScalesTheCurrentValue()
        {
            RegisterTicking("leak", 1f, Mod(ModifierType.Resource, "Mana", 0.9f, ModifierOperation.Multiply));
            _target.Resources["Mana"] = 200f;
            _manager.ApplyEffect(_entity, "leak", "caster", 0f);

            _manager.ProcessTicks(_entity, 1f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(180f).Within(1e-4f));

            _manager.ProcessTicks(_entity, 2f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(162f).Within(1e-4f));
        }

        [Test]
        public void ResourceOverride_SetsTheValue()
        {
            RegisterTicking("lock", 1f, Mod(ModifierType.Resource, "Energy", 10f, ModifierOperation.Override));
            _target.Resources["Energy"] = 75f;
            _manager.ApplyEffect(_entity, "lock", "caster", 0f);

            _manager.ProcessTicks(_entity, 1f);

            Assert.That(_target.Resources["Energy"], Is.EqualTo(10f));
        }

        [Test]
        public void ResourceOnAnEffectWithoutTicks_IsAppliedOncePerApplication()
        {
            _manager.RegisterEffect(Effect("potion", 0f, Mod(ModifierType.Resource, "Mana", 50f, ModifierOperation.Add)));
            _target.Resources["Mana"] = 10f;

            _manager.ApplyEffect(_entity, "potion", "caster", 0f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(60f));

            _manager.ProcessTicks(_entity, 5f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(60f), "no ticking without a tick interval");
        }

        // ---- Damage and healing ----

        [Test]
        public void DamageMultiply_TakesAShareOfTheCurrentValue()
        {
            RegisterTicking("wither", 1f, Mod(ModifierType.Damage, "Health", 0.9f, ModifierOperation.Multiply));
            _target.Resources["Health"] = 200f;
            _manager.ApplyEffect(_entity, "wither", "caster", 0f);

            float damage = 0f, healing = 0f;
            _manager.ProcessTicks(_entity, 1f, (e, id, d, h) => { damage += d; healing += h; });

            Assert.That(damage, Is.EqualTo(20f).Within(1e-4f), "health x0.9 is 10% of 200");
            Assert.That(healing, Is.EqualTo(0f));
            Assert.That(_target.Resources["Health"], Is.EqualTo(200f), "damage is reported, not applied, as before");
        }

        [Test]
        public void HealingOverride_HealsUpToTheValue()
        {
            RegisterTicking("restore", 1f, Mod(ModifierType.Healing, "Health", 150f, ModifierOperation.Override));
            _target.Resources["Health"] = 100f;
            _manager.ApplyEffect(_entity, "restore", "caster", 0f);

            float healing = 0f;
            _manager.ProcessTicks(_entity, 1f, (e, id, d, h) => healing += h);

            Assert.That(healing, Is.EqualTo(50f));
        }

        [Test]
        public void DamageMultiply_WithoutAResourceToRead_DealsNothing()
        {
            var manager = new StatusEffectManager(_entities);
            var data = Effect("wither", 10f, Mod(ModifierType.Damage, "Health", 0.5f, ModifierOperation.Multiply));
            data.TickInterval = 1f;
            manager.RegisterEffect(data);
            manager.ApplyEffect(_entity, "wither", "caster", 0f);

            int calls = 0;
            manager.ProcessTicks(_entity, 1f, (e, id, d, h) => calls++);

            Assert.That(calls, Is.EqualTo(0), "a share of an unknown value is not guessed as a flat amount");
        }

        // ---- Movement ----

        [Test]
        public void Movement_CombinesEffects_AndTellsTheGameWhenItChanges()
        {
            _manager.RegisterEffect(Effect("slow", 10f, Mod(ModifierType.Movement, "MoveSpeed", 0.5f, ModifierOperation.Multiply)));
            _manager.RegisterEffect(Effect("swift", 10f, Mod(ModifierType.Movement, "MoveSpeed", 0.2f, ModifierOperation.Add)));
            _manager.RegisterEffect(Effect("root", 2f, Mod(ModifierType.Movement, "MoveSpeed", 0f, ModifierOperation.Override)));

            Assert.That(_manager.GetMovementMultiplier(_entity), Is.EqualTo(1f));

            _manager.ApplyEffect(_entity, "slow", "caster", 0f);
            Assert.That(_target.Movement[_entity], Is.EqualTo(0.5f));

            _manager.ApplyEffect(_entity, "swift", "caster", 0f);
            Assert.That(_manager.GetMovementMultiplier(_entity), Is.EqualTo(0.6f).Within(1e-5f), "(1 + 0.2) x 0.5");
            Assert.That(_target.Movement[_entity], Is.EqualTo(0.6f).Within(1e-5f));

            _manager.ApplyEffect(_entity, "root", "caster", 0f);
            Assert.That(_target.Movement[_entity], Is.EqualTo(0f), "an override wins");

            _manager.UpdateEffects(_entity, 3f); // root runs out
            Assert.That(_target.Movement[_entity], Is.EqualTo(0.6f).Within(1e-5f));

            _manager.RemoveAllEffects(_entity);
            Assert.That(_target.Movement[_entity], Is.EqualTo(1f));
        }

        // ---- Control ----

        [Test]
        public void Control_IsOnWhileAnyEffectGivesIt()
        {
            _manager.RegisterEffect(Effect("stun", 2f, Mod(ModifierType.Control, "Stun", 1f, ModifierOperation.Add)));
            _manager.RegisterEffect(Effect("freeze", 5f,
                Mod(ModifierType.Control, "Stun", 1f, ModifierOperation.Add),
                Mod(ModifierType.Control, "Silence", 1f, ModifierOperation.Add)));

            _manager.ApplyEffect(_entity, "stun", "caster", 0f);
            _manager.ApplyEffect(_entity, "freeze", "caster", 0f);

            Assert.That(_manager.IsControlled(_entity, "Stun"), Is.True);
            Assert.That(_manager.GetActiveControls(_entity), Is.EquivalentTo(new[] { "Stun", "Silence" }));
            Assert.That(_target.ControlChanges, Is.EqualTo(new[] { "Stun:on", "Silence:on" }));

            _manager.RemoveEffect(_entity, "stun");
            Assert.That(_manager.IsControlled(_entity, "Stun"), Is.True, "freeze still stuns");
            Assert.That(_target.ControlChanges.Count, Is.EqualTo(2));

            _manager.RemoveEffect(_entity, "freeze");
            Assert.That(_manager.IsControlled(_entity, "Stun"), Is.False);
            Assert.That(_target.ControlChanges, Is.EquivalentTo(new[] { "Stun:on", "Silence:on", "Stun:off", "Silence:off" }));
        }

        // ---- Without a target ----

        [Test]
        public void WithoutATarget_EffectsStillTrack_AndQueriesAnswer()
        {
            var manager = new StatusEffectManager(_entities);
            manager.RegisterEffect(Effect("slow", 10f,
                Mod(ModifierType.Movement, "MoveSpeed", 0.5f, ModifierOperation.Multiply),
                Mod(ModifierType.Stat, "Defense", 5f, ModifierOperation.Add),
                Mod(ModifierType.Control, "Disarm", 1f, ModifierOperation.Add)));

            Assert.That(manager.ApplyEffect(_entity, "slow", "caster", 0f), Is.True);
            Assert.That(manager.GetMovementMultiplier(_entity), Is.EqualTo(0.5f));
            Assert.That(manager.IsControlled(_entity, "Disarm"), Is.True);
            Assert.That(manager.RemoveEffect(_entity, "slow"), Is.True);
            Assert.That(manager.GetMovementMultiplier(_entity), Is.EqualTo(1f));
        }

        // ---- Long frames and loaded games ----

        [Test]
        public void Ticks_MissedInALongFrame_LandTogether_OnTheEffectsRhythm()
        {
            RegisterTicking("drain", 1f, Mod(ModifierType.Resource, "Mana", -4f, ModifierOperation.Add));
            _target.Resources["Mana"] = 100f;
            _manager.ApplyEffect(_entity, "drain", "caster", 0f);

            _manager.ProcessTicks(_entity, 3.5f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(88f), "three ticks");

            _manager.ProcessTicks(_entity, 3.9f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(88f), "the next is due at 4, not at 4.5");
            _manager.ProcessTicks(_entity, 4f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(84f));

            _manager.ProcessTicks(_entity, 104f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(84f - 4f * StatusEffectManager.MaxTicksPerCall), "at most the cap");
            _manager.ProcessTicks(_entity, 104.5f);
            Assert.That(_target.Resources["Mana"], Is.EqualTo(84f - 4f * StatusEffectManager.MaxTicksPerCall), "no backlog after the cap");
        }

        [Test]
        public void Ticks_MissedInALongFrame_AreReportedAsOneTotal()
        {
            RegisterTicking("burn", 1f, Mod(ModifierType.Damage, "Health", 5f, ModifierOperation.Add));
            _manager.ApplyEffect(_entity, "burn", "caster", 0f);

            int calls = 0;
            float damage = 0f;
            _manager.ProcessTicks(_entity, 2f, (e, id, d, h) => { calls++; damage += d; });

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(damage, Is.EqualTo(10f));
        }

        [Test]
        public void Reapply_GivesALoadedEntityItsModifiersMovementAndControls_Again()
        {
            _manager.RegisterEffect(Effect("frost", 10f,
                Mod(ModifierType.Stat, "AttackSpeed", 0.8f, ModifierOperation.Multiply),
                Mod(ModifierType.Movement, "", 0.5f, ModifierOperation.Multiply),
                Mod(ModifierType.Control, "Silence", 0f, ModifierOperation.Add)));
            _manager.ApplyEffect(_entity, "frost", "caster", 0f);

            // A loaded game: the effects come back as the component, through a new manager and target
            _entities.TryGetComponent<ActiveEffectsData>(_entity, out var saved);
            var loaded = new EntityManager();
            int entity = loaded.CreateEntity();
            loaded.AddComponent(entity, saved);
            var target = new RecordingTarget();
            var manager = new StatusEffectManager(loaded, null, target);
            manager.RegisterEffect(_manager.GetEffect("frost"));
            Assert.That(target.Stats, Is.Empty, "nothing until it is reapplied");

            manager.Reapply(entity);
            manager.Reapply(entity);

            Assert.That(target.Stats[StatusEffectManager.StatModifierId("frost", 0)].Value, Is.EqualTo(0.8f));
            Assert.That(target.Movement[entity], Is.EqualTo(0.5f));
            Assert.That(target.ControlChanges, Is.EqualTo(new[] { "Silence:on", "Silence:on" }));
            Assert.That(manager.IsControlled(entity, "Silence"), Is.True);
        }

        private sealed class RecordingTarget : IStatusEffectTarget
        {
            public readonly Dictionary<string, (string StatId, ModifierOperation Operation, float Value)> Stats =
                new Dictionary<string, (string, ModifierOperation, float)>();
            public readonly Dictionary<string, float> Resources = new Dictionary<string, float>();
            public readonly Dictionary<int, float> Movement = new Dictionary<int, float>();
            public readonly List<string> ControlChanges = new List<string>();

            public void ApplyStatModifier(int entityId, string modifierId, string statId, ModifierOperation operation, float value)
            {
                Assert.That(Stats.ContainsKey(modifierId), Is.False, "a modifier is removed before it is put on again");
                Stats[modifierId] = (statId, operation, value);
            }

            public void RemoveStatModifier(int entityId, string modifierId) => Stats.Remove(modifierId);

            public bool TryGetResource(int entityId, string resourceId, out float value) =>
                Resources.TryGetValue(resourceId, out value);

            public void ChangeResource(int entityId, string resourceId, float amount)
            {
                Resources.TryGetValue(resourceId, out var current);
                Resources[resourceId] = current + amount;
            }

            public void SetMovementMultiplier(int entityId, float multiplier) => Movement[entityId] = multiplier;

            public void SetControl(int entityId, string controlId, bool active) =>
                ControlChanges.Add(controlId + (active ? ":on" : ":off"));
        }
    }
}
