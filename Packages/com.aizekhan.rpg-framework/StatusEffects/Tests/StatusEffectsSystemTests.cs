using NUnit.Framework;
using RPGFramework.ECS;
using System.Collections.Generic;

namespace RPGFramework.StatusEffects.Tests
{
    [TestFixture]
    public class StatusEffectsSystemTests
    {
        private EntityManager _entityManager;
        private StatusEffectManager _effectManager;
        private int _testEntityId;

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
            _effectManager = new StatusEffectManager(_entityManager);
            _testEntityId = _entityManager.CreateEntity();

            // Register test effects
            RegisterTestEffects();
        }

        private void RegisterTestEffects()
        {
            // Poison DoT effect
            var poisonData = StatusEffectData.CreateDoT("poison", "Poison", 10f, 1f, 5f);
            _effectManager.RegisterEffect(poisonData);

            // Regeneration HoT effect
            var regenData = StatusEffectData.CreateHoT("regen", "Regeneration", 20f, 2f, 10f);
            _effectManager.RegisterEffect(regenData);

            // Speed buff
            var speedBuff = StatusEffectData.CreateStatModifier(
                "haste", "Haste", EffectType.Buff, 15f, "MoveSpeed", 1.5f, ModifierOperation.Multiply);
            _effectManager.RegisterEffect(speedBuff);

            // Armor debuff
            var armorDebuff = StatusEffectData.CreateStatModifier(
                "weaken", "Weaken", EffectType.Debuff, 8f, "Defense", 0.7f, ModifierOperation.Multiply);
            _effectManager.RegisterEffect(armorDebuff);

            // Stackable poison
            var stackablePoison = StatusEffectData.CreateDoT("poison_stack", "Stackable Poison", 10f, 1f, 3f);
            stackablePoison.IsStackable = true;
            stackablePoison.MaxStacks = 5;
            _effectManager.RegisterEffect(stackablePoison);
        }

        // ==================== Effect Application Tests ====================

        [Test]
        public void ApplyEffect_NewEffect_Succeeds()
        {
            bool result = _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);

            Assert.That(result, Is.True);
            Assert.That(_effectManager.HasEffect(_testEntityId, "poison"), Is.True);
        }

        [Test]
        public void ApplyEffect_InvalidEffectId_ReturnsFalse()
        {
            bool result = _effectManager.ApplyEffect(_testEntityId, "invalid_effect", "caster", 0f);

            Assert.That(result, Is.False);
        }

        [Test]
        public void ApplyEffect_MultipleEffects_AllApplied()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "haste", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "weaken", "caster", 0f);

            var activeEffects = _effectManager.GetActiveEffects(_testEntityId);
            Assert.That(activeEffects.Count, Is.EqualTo(3));
        }

        // ==================== Effect Stacking Tests ====================

        [Test]
        public void ApplyEffect_StackableEffect_AddsStacks()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison_stack", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "poison_stack", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "poison_stack", "caster", 0f);

            var activeEffects = _effectManager.GetActiveEffects(_testEntityId);
            Assert.That(activeEffects.Count, Is.EqualTo(1));
            Assert.That(activeEffects[0].CurrentStacks, Is.EqualTo(3));
        }

        [Test]
        public void ApplyEffect_NonStackableEffect_RefreshesDuration()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);
            _effectManager.UpdateEffects(_testEntityId, 5f); // 5 seconds pass

            var effectsBefore = _effectManager.GetActiveEffects(_testEntityId);
            float durationBefore = effectsBefore[0].RemainingDuration;

            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 5f); // Reapply

            var effectsAfter = _effectManager.GetActiveEffects(_testEntityId);
            float durationAfter = effectsAfter[0].RemainingDuration;

            Assert.That(durationAfter, Is.GreaterThan(durationBefore));
        }

        // ==================== Effect Duration Tests ====================

        [Test]
        public void UpdateEffects_ReducesDuration()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);

            var effectsBefore = _effectManager.GetActiveEffects(_testEntityId);
            float durationBefore = effectsBefore[0].RemainingDuration;

            _effectManager.UpdateEffects(_testEntityId, 2f);

            var effectsAfter = _effectManager.GetActiveEffects(_testEntityId);
            float durationAfter = effectsAfter[0].RemainingDuration;

            Assert.That(durationAfter, Is.LessThan(durationBefore));
            Assert.That(durationAfter, Is.EqualTo(durationBefore - 2f).Within(0.01f));
        }

        [Test]
        public void UpdateEffects_ExpiredEffect_RemovedAutomatically()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);

            _effectManager.UpdateEffects(_testEntityId, 15f); // More than duration (10s)

            var activeEffects = _effectManager.GetActiveEffects(_testEntityId);
            Assert.That(activeEffects.Count, Is.EqualTo(0));
        }

        // ==================== Effect Removal Tests ====================

        [Test]
        public void RemoveEffect_ExistingEffect_Succeeds()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);

            bool result = _effectManager.RemoveEffect(_testEntityId, "poison");

            Assert.That(result, Is.True);
            Assert.That(_effectManager.HasEffect(_testEntityId, "poison"), Is.False);
        }

        [Test]
        public void RemoveEffect_NonExistentEffect_ReturnsFalse()
        {
            bool result = _effectManager.RemoveEffect(_testEntityId, "poison");

            Assert.That(result, Is.False);
        }

        [Test]
        public void RemoveAllEffects_ClearsAllEffects()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "haste", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "weaken", "caster", 0f);

            _effectManager.RemoveAllEffects(_testEntityId);

            var activeEffects = _effectManager.GetActiveEffects(_testEntityId);
            Assert.That(activeEffects.Count, Is.EqualTo(0));
        }

        // ==================== Effect Query Tests ====================

        [Test]
        public void HasEffect_ExistingEffect_ReturnsTrue()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);

            Assert.That(_effectManager.HasEffect(_testEntityId, "poison"), Is.True);
        }

        [Test]
        public void HasEffect_NonExistentEffect_ReturnsFalse()
        {
            Assert.That(_effectManager.HasEffect(_testEntityId, "poison"), Is.False);
        }

        [Test]
        public void GetActiveEffects_ReturnsAllActive()
        {
            _effectManager.ApplyEffect(_testEntityId, "poison", "caster", 0f);
            _effectManager.ApplyEffect(_testEntityId, "haste", "caster", 0f);

            var activeEffects = _effectManager.GetActiveEffects(_testEntityId);

            Assert.That(activeEffects.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetActiveEffects_NoEntity_ReturnsEmptyList()
        {
            int nonExistentEntity = 999;

            var activeEffects = _effectManager.GetActiveEffects(nonExistentEntity);

            Assert.That(activeEffects.Count, Is.EqualTo(0));
        }

        // ==================== Data Structure Tests ====================

        [Test]
        public void StatusEffectData_Create_InitializesCorrectly()
        {
            var data = StatusEffectData.Create("test", "Test Effect", EffectType.Buff);

            Assert.That(data.Id, Is.EqualTo("test"));
            Assert.That(data.Name, Is.EqualTo("Test Effect"));
            Assert.That(data.Type, Is.EqualTo(EffectType.Buff));
            Assert.That(data.Duration, Is.EqualTo(0f));
        }

        [Test]
        public void StatusEffectData_CreateDoT_ConfiguresCorrectly()
        {
            var data = StatusEffectData.CreateDoT("dot", "DoT Effect", 10f, 1f, 5f);

            Assert.That(data.Type, Is.EqualTo(EffectType.DamageOverTime));
            Assert.That(data.Duration, Is.EqualTo(10f));
            Assert.That(data.TickInterval, Is.EqualTo(1f));
            Assert.That(data.Modifiers.Count, Is.EqualTo(1));
            Assert.That(data.Modifiers[0].Type, Is.EqualTo(ModifierType.Damage));
            Assert.That(data.Modifiers[0].Value, Is.EqualTo(5f));
        }

        [Test]
        public void ActiveEffectsData_CreateEmpty_Initializes()
        {
            var data = ActiveEffectsData.CreateEmpty();

            Assert.That(data.ActiveInstances, Is.Not.Null);
            Assert.That(data.ActiveInstances.Count, Is.EqualTo(0));
        }

        [Test]
        public void ActiveEffectsData_AddEffect_AddsToList()
        {
            var data = ActiveEffectsData.CreateEmpty();
            var instance = StatusEffectInstanceData.Create("test", 1, 10f, "caster", 0f);

            data.AddEffect(instance);

            Assert.That(data.ActiveInstances.Count, Is.EqualTo(1));
            Assert.That(data.HasEffect("test"), Is.True);
        }

        [Test]
        public void ActiveEffectsData_RemoveEffect_RemovesFromList()
        {
            var data = ActiveEffectsData.CreateEmpty();
            var instance = StatusEffectInstanceData.Create("test", 1, 10f, "caster", 0f);
            data.AddEffect(instance);

            data.RemoveEffect("test");

            Assert.That(data.ActiveInstances.Count, Is.EqualTo(0));
            Assert.That(data.HasEffect("test"), Is.False);
        }

        // ==================== Modifier Tests ====================

        [Test]
        public void EffectModifier_Apply_AddOperation()
        {
            var modifier = new EffectModifier(ModifierType.Stat, "Attack", 10f, ModifierOperation.Add);

            float result = modifier.Apply(50f);

            Assert.That(result, Is.EqualTo(60f));
        }

        [Test]
        public void EffectModifier_Apply_MultiplyOperation()
        {
            var modifier = new EffectModifier(ModifierType.Stat, "Attack", 1.5f, ModifierOperation.Multiply);

            float result = modifier.Apply(50f);

            Assert.That(result, Is.EqualTo(75f));
        }

        [Test]
        public void EffectModifier_Apply_OverrideOperation()
        {
            var modifier = new EffectModifier(ModifierType.Stat, "Attack", 100f, ModifierOperation.Override);

            float result = modifier.Apply(50f);

            Assert.That(result, Is.EqualTo(100f));
        }
    }
}
