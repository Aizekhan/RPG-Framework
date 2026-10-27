using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.StatusEffects;
using UnityEngine;

namespace RPGFramework.Tools.StatusEffectEditor.Tests
{
    /// <summary>
    /// A status effect made as an asset becomes exactly the definition the status effect manager runs on: it is applied,
    /// ticks, stacks as set and expires. The checks catch every setting that would make an effect behave differently
    /// than it reads in the inspector, and the database registers its effects so the game can apply them by id.
    /// </summary>
    public sealed class StatusEffectAssetTests
    {
        private readonly List<Object> _made = new List<Object>();
        private EntityManager _entities;
        private StatusEffectManager _manager;
        private int _entity;

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _manager = new StatusEffectManager(_entities);
            _entity = _entities.CreateEntity();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _made.Add(asset);
            return asset;
        }

        private StatusEffectAsset Poison(string id = "poison")
        {
            var effect = Make<StatusEffectAsset>();
            effect.effectId = id;
            effect.effectName = "Poison";
            effect.description = "Hurts over time";
            effect.effectType = EffectType.DamageOverTime;
            effect.length = StatusEffectLength.Timed;
            effect.duration = 10f;
            effect.tickInterval = 1f;
            effect.isStackable = true;
            effect.maxStacks = 3;
            effect.modifiers = new List<StatusEffectModifierEntry>
            {
                new StatusEffectModifierEntry { type = ModifierType.Damage, targetId = "Health", value = 5f, operation = ModifierOperation.Add }
            };
            return effect;
        }

        private StatusEffectAsset Haste(string id = "haste")
        {
            var effect = Make<StatusEffectAsset>();
            effect.effectId = id;
            effect.effectName = "Haste";
            effect.effectType = EffectType.Buff;
            effect.length = StatusEffectLength.Timed;
            effect.duration = 15f;
            effect.tickInterval = 0f;
            effect.isStackable = false;
            effect.maxStacks = 1;
            effect.modifiers = new List<StatusEffectModifierEntry>
            {
                new StatusEffectModifierEntry { type = ModifierType.Stat, targetId = "MoveSpeed", value = 1.5f, operation = ModifierOperation.Multiply }
            };
            return effect;
        }

        private static void AssertOnlyError(StatusEffectAsset effect, string expected)
        {
            var errors = effect.Validate();
            Assert.That(errors.Count, Is.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain(expected));
        }

        private int Stacks(string effectId)
        {
            return _manager.GetActiveEffects(_entity).Single(e => e.EffectId == effectId).CurrentStacks;
        }

        private float Remaining(string effectId)
        {
            return _manager.GetActiveEffects(_entity).Single(e => e.EffectId == effectId).RemainingDuration;
        }

        // ---- Conversion and the real runtime ----

        [Test]
        public void GoodEffects_HaveNoProblems()
        {
            Assert.That(Poison().Validate(), Is.Empty);
            Assert.That(Haste().Validate(), Is.Empty);
        }

        [Test]
        public void ToData_KeepsEveryField()
        {
            var data = Poison().ToData();

            Assert.That(data.Id, Is.EqualTo("poison"));
            Assert.That(data.Name, Is.EqualTo("Poison"));
            Assert.That(data.Description, Is.EqualTo("Hurts over time"));
            Assert.That(data.Type, Is.EqualTo(EffectType.DamageOverTime));
            Assert.That(data.Duration, Is.EqualTo(10f));
            Assert.That(data.TickInterval, Is.EqualTo(1f));
            Assert.That(data.IsStackable, Is.True);
            Assert.That(data.MaxStacks, Is.EqualTo(3));
            Assert.That(data.Modifiers.Count, Is.EqualTo(1));
            Assert.That(data.Modifiers[0].Type, Is.EqualTo(ModifierType.Damage));
            Assert.That(data.Modifiers[0].TargetId, Is.EqualTo("Health"));
            Assert.That(data.Modifiers[0].Value, Is.EqualTo(5f));
            Assert.That(data.Modifiers[0].Operation, Is.EqualTo(ModifierOperation.Add));
        }

        [Test]
        public void ToData_GivesPermanentMinusOne_InstantZero_AndSkipsEmptyModifierSlots()
        {
            var effect = Haste();
            effect.modifiers.Add(null);

            effect.length = StatusEffectLength.Permanent;
            Assert.That(effect.ToData().Duration, Is.EqualTo(-1f));

            effect.length = StatusEffectLength.Instant;
            Assert.That(effect.ToData().Duration, Is.EqualTo(0f));
            Assert.That(effect.ToData().Modifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConvertedPoison_IsApplied_StacksToItsCap_TicksPerStack_AndExpires()
        {
            _manager.RegisterEffect(Poison().ToData());

            Assert.That(_manager.ApplyEffect(_entity, "poison", "caster", 0f), Is.True);
            Assert.That(Stacks("poison"), Is.EqualTo(1));

            for (int i = 0; i < 5; i++)
                _manager.ApplyEffect(_entity, "poison", "caster", 0f);
            Assert.That(Stacks("poison"), Is.EqualTo(3), "stacks stop at Max Stacks");

            float damage = 0f;
            _manager.ProcessTicks(_entity, 0.5f, (entity, effect, dmg, heal) => damage += dmg);
            Assert.That(damage, Is.EqualTo(0f), "no tick before the tick interval");

            _manager.ProcessTicks(_entity, 1f, (entity, effect, dmg, heal) => damage += dmg);
            Assert.That(damage, Is.EqualTo(15f), "5 damage per stack");

            _manager.UpdateEffects(_entity, 9f);
            Assert.That(_manager.HasEffect(_entity, "poison"), Is.True);

            _manager.UpdateEffects(_entity, 1f);
            Assert.That(_manager.HasEffect(_entity, "poison"), Is.False, "gone after its 10 s");
        }

        [Test]
        public void ConvertedHealing_TicksHealing()
        {
            var regen = Poison("regen");
            regen.effectType = EffectType.HealOverTime;
            regen.isStackable = false;
            regen.maxStacks = 1;
            regen.modifiers[0].type = ModifierType.Healing;
            Assert.That(regen.Validate(), Is.Empty);
            _manager.RegisterEffect(regen.ToData());
            _manager.ApplyEffect(_entity, "regen", "caster", 0f);

            float healing = 0f;
            _manager.ProcessTicks(_entity, 1f, (entity, effect, dmg, heal) => healing += heal);

            Assert.That(healing, Is.EqualTo(5f));
        }

        [Test]
        public void ConvertedUnstackableEffect_StartsItsTimeOverInsteadOfStacking()
        {
            _manager.RegisterEffect(Haste().ToData());
            _manager.ApplyEffect(_entity, "haste", "caster", 0f);
            _manager.UpdateEffects(_entity, 5f);
            Assert.That(Remaining("haste"), Is.EqualTo(10f));

            _manager.ApplyEffect(_entity, "haste", "caster", 5f);

            Assert.That(Stacks("haste"), Is.EqualTo(1));
            Assert.That(Remaining("haste"), Is.EqualTo(15f));
        }

        [Test]
        public void ConvertedPermanentEffect_NeverExpires_AndInstantEffect_IsGoneOnTheNextUpdate()
        {
            var aura = Haste("aura");
            aura.length = StatusEffectLength.Permanent;
            var cleanse = Haste("cleanse");
            cleanse.length = StatusEffectLength.Instant;
            Assert.That(aura.Validate(), Is.Empty);
            Assert.That(cleanse.Validate(), Is.Empty);
            _manager.RegisterEffect(aura.ToData());
            _manager.RegisterEffect(cleanse.ToData());
            _manager.ApplyEffect(_entity, "aura", "caster", 0f);
            _manager.ApplyEffect(_entity, "cleanse", "caster", 0f);

            _manager.UpdateEffects(_entity, 1000f);

            Assert.That(_manager.HasEffect(_entity, "aura"), Is.True);
            Assert.That(_manager.HasEffect(_entity, "cleanse"), Is.False);
        }

        [Test]
        public void Describe_SaysWhatTheEffectDoes_InOneLine()
        {
            Assert.That(Poison().Describe(), Is.EqualTo("Poison: 5 damage every 1 s for 10 s (stacks to 3)"));
            Assert.That(Haste().Describe(), Is.EqualTo("Haste: MoveSpeed x1.5 for 15 s"));

            var aura = Haste();
            aura.length = StatusEffectLength.Permanent;
            Assert.That(aura.Describe(), Is.EqualTo("Haste: MoveSpeed x1.5 until removed"));
        }

        // ---- Effect checks, one per rule ----

        [Test]
        public void Check_EmptyId()
        {
            var effect = Poison();
            effect.effectId = " ";
            AssertOnlyError(effect, "Effect ID");
        }

        [Test]
        public void Check_EmptyName()
        {
            var effect = Poison();
            effect.effectName = "";
            AssertOnlyError(effect, "Effect Name");
        }

        [Test]
        public void Check_TimedEffectWithoutDuration()
        {
            var effect = Poison();
            effect.duration = 0f;
            AssertOnlyError(effect, "Duration must be above 0");
        }

        [Test]
        public void Check_NegativeTickInterval()
        {
            var effect = Haste();
            effect.tickInterval = -1f;
            AssertOnlyError(effect, "cannot be negative");
        }

        [Test]
        public void Check_DamageWithoutTickInterval_NeverTicks()
        {
            var effect = Poison();
            effect.tickInterval = 0f;
            AssertOnlyError(effect, "never ticks");
        }

        [Test]
        public void Check_TickIntervalWithNothingToTick()
        {
            var effect = Haste();
            effect.tickInterval = 1f;
            AssertOnlyError(effect, "nothing ticks");
        }

        [Test]
        public void Check_TickIntervalLongerThanDuration()
        {
            var effect = Poison();
            effect.tickInterval = 20f;
            AssertOnlyError(effect, "longer than Duration");
        }

        [Test]
        public void Check_PermanentTickingEffect_IsFine()
        {
            var effect = Poison();
            effect.length = StatusEffectLength.Permanent;
            effect.duration = 0f;
            effect.tickInterval = 20f;
            Assert.That(effect.Validate(), Is.Empty);
        }

        [Test]
        public void Check_InstantEffectThatTicks()
        {
            var effect = Poison();
            effect.length = StatusEffectLength.Instant;
            AssertOnlyError(effect, "instant effect");
        }

        [Test]
        public void Check_MaxStacksBelowOne()
        {
            var effect = Haste();
            effect.maxStacks = 0;
            AssertOnlyError(effect, "at least 1");
        }

        [Test]
        public void Check_StackableWithMaxStacksOne()
        {
            var effect = Poison();
            effect.maxStacks = 1;
            AssertOnlyError(effect, "can never stack");
        }

        [Test]
        public void Check_MaxStacksOnAnUnstackableEffect()
        {
            var effect = Haste();
            effect.maxStacks = 4;
            AssertOnlyError(effect, "not stackable");
        }

        [Test]
        public void Check_DamageOverTimeWithoutDamage()
        {
            var effect = Haste();
            effect.effectType = EffectType.DamageOverTime;
            AssertOnlyError(effect, "needs a Damage modifier");
        }

        [Test]
        public void Check_HealOverTimeWithoutHealing()
        {
            var effect = Haste();
            effect.effectType = EffectType.HealOverTime;
            AssertOnlyError(effect, "needs a Healing modifier");
        }

        [Test]
        public void Check_EmptyModifierSlot()
        {
            var effect = Haste();
            effect.modifiers.Add(null);
            AssertOnlyError(effect, "Modifier 2 is an empty slot");
        }

        [Test]
        public void Check_DamagePerTickNotAboveZero()
        {
            var effect = Poison();
            effect.modifiers[0].value = 0f;
            AssertOnlyError(effect, "must be above 0");
        }

        [Test]
        public void Check_DamageWithAnOperationOtherThanAdd()
        {
            var effect = Poison();
            effect.modifiers[0].operation = ModifierOperation.Multiply;
            AssertOnlyError(effect, "must be Add");
        }

        [Test]
        public void Check_StatModifierWithoutTarget()
        {
            var effect = Haste();
            effect.modifiers[0].targetId = "";
            AssertOnlyError(effect, "needs a target");
        }

        // ---- Database ----

        [Test]
        public void Database_OfGoodEffects_HasNoProblems_AndRegistersThemAll()
        {
            var database = Make<StatusEffectDatabase>();
            database.effects = new List<StatusEffectAsset> { Poison(), Haste() };

            Assert.That(database.Validate(), Is.Empty);
            Assert.That(database.GetEffect("haste"), Is.SameAs(database.effects[1]));
            Assert.That(database.RegisterInto(_manager), Is.EqualTo(2));
            Assert.That(_manager.ApplyEffect(_entity, "poison", "caster", 0f), Is.True);
            Assert.That(_manager.ApplyEffect(_entity, "haste", "caster", 0f), Is.True);
            Assert.That(_manager.GetEffect("haste").Duration, Is.EqualTo(15f));
        }

        [Test]
        public void Database_SkipsEmptySlots_WhenRegistering()
        {
            var database = Make<StatusEffectDatabase>();
            database.effects = new List<StatusEffectAsset> { null, Haste() };

            Assert.That(database.RegisterInto(_manager), Is.EqualTo(1));
            Assert.That(_manager.ApplyEffect(_entity, "haste", "caster", 0f), Is.True);
        }

        [Test]
        public void DatabaseCheck_Empty()
        {
            var database = Make<StatusEffectDatabase>();
            Assert.That(database.Validate(), Has.Some.Contains("empty"));
        }

        [Test]
        public void DatabaseCheck_EmptySlot()
        {
            var database = Make<StatusEffectDatabase>();
            database.effects = new List<StatusEffectAsset> { Haste(), null };

            var errors = database.Validate();

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("1 empty slot"));
        }

        [Test]
        public void DatabaseCheck_DuplicateIds()
        {
            var database = Make<StatusEffectDatabase>();
            database.effects = new List<StatusEffectAsset> { Poison("venom"), Haste("venom") };

            var errors = database.Validate();

            Assert.That(errors.Count, Is.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Duplicate effect IDs").And.Contain("venom"));
        }

        [Test]
        public void DatabaseCheck_ReportsEachEffectsOwnProblems()
        {
            var broken = Haste();
            broken.maxStacks = 0;
            var database = Make<StatusEffectDatabase>();
            database.effects = new List<StatusEffectAsset> { Poison(), broken };

            var errors = database.Validate();

            Assert.That(errors, Has.Some.Contains("Effect 'Haste' (haste)"));
            Assert.That(errors, Has.Some.Contains("Max Stacks must be at least 1"));
        }
    }
}
