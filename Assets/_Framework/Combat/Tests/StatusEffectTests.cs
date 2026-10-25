using NUnit.Framework;
using RPGFramework.Combat;
using System;
using System.Linq;

namespace RPGFramework.Combat.Tests
{
    [TestFixture]
    public class StatusEffectTests
    {
        private StatusEffectManager _manager;
        private MockTarget _target;

        [SetUp]
        public void Setup()
        {
            _manager = new StatusEffectManager();
            _target = new MockTarget("target1");
        }

        #region Core Functionality Tests

        [Test]
        public void ApplyEffect_NewEffect_AddsToTarget()
        {
            var effect = new MockEffect("buff1", "Test Buff", StatusEffectType.Buff, 10f);

            bool applied = _manager.ApplyEffect(_target, effect);

            Assert.That(applied, Is.True);
            Assert.That(_manager.HasEffect(_target, "buff1"), Is.True);
            Assert.That(effect.IsActive, Is.True);
        }

        [Test]
        public void RemoveEffect_ExistingEffect_RemovesFromTarget()
        {
            var effect = new MockEffect("buff1", "Test Buff", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect);

            bool removed = _manager.RemoveEffect(_target, "buff1");

            Assert.That(removed, Is.True);
            Assert.That(_manager.HasEffect(_target, "buff1"), Is.False);
            Assert.That(effect.IsActive, Is.False);
        }

        [Test]
        public void RemoveAllEffects_MultipleEffects_RemovesAll()
        {
            var effect1 = new MockEffect("buff1", "Buff 1", StatusEffectType.Buff, 10f);
            var effect2 = new MockEffect("buff2", "Buff 2", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect1);
            _manager.ApplyEffect(_target, effect2);

            _manager.RemoveAllEffects(_target);

            Assert.That(_manager.GetActiveEffects(_target).Count, Is.EqualTo(0));
            Assert.That(effect1.IsActive, Is.False);
            Assert.That(effect2.IsActive, Is.False);
        }

        [Test]
        public void RemoveEffectsOfType_SpecificType_RemovesOnlyThatType()
        {
            var buff = new MockEffect("buff1", "Buff", StatusEffectType.Buff, 10f);
            var debuff = new MockEffect("debuff1", "Debuff", StatusEffectType.Debuff, 10f);
            _manager.ApplyEffect(_target, buff);
            _manager.ApplyEffect(_target, debuff);

            _manager.RemoveEffectsOfType(_target, StatusEffectType.Debuff);

            Assert.That(_manager.HasEffect(_target, "buff1"), Is.True);
            Assert.That(_manager.HasEffect(_target, "debuff1"), Is.False);
        }

        [Test]
        public void GetActiveEffects_MultipleEffects_ReturnsAll()
        {
            var effect1 = new MockEffect("buff1", "Buff 1", StatusEffectType.Buff, 10f);
            var effect2 = new MockEffect("buff2", "Buff 2", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect1);
            _manager.ApplyEffect(_target, effect2);

            var effects = _manager.GetActiveEffects(_target);

            Assert.That(effects.Count, Is.EqualTo(2));
        }

        [Test]
        public void HasEffect_ExistingEffect_ReturnsTrue()
        {
            var effect = new MockEffect("buff1", "Test Buff", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect);

            Assert.That(_manager.HasEffect(_target, "buff1"), Is.True);
        }

        [Test]
        public void HasEffect_NonExistingEffect_ReturnsFalse()
        {
            Assert.That(_manager.HasEffect(_target, "nonexistent"), Is.False);
        }

        [Test]
        public void ApplyEffect_TargetCannotReceive_ReturnsFalse()
        {
            _target.CanReceive = false;
            var effect = new MockEffect("buff1", "Test Buff", StatusEffectType.Buff, 10f);

            bool applied = _manager.ApplyEffect(_target, effect);

            Assert.That(applied, Is.False);
            Assert.That(_manager.HasEffect(_target, "buff1"), Is.False);
        }

        [Test]
        public void ApplyEffect_NullTarget_ThrowsException()
        {
            var effect = new MockEffect("buff1", "Test Buff", StatusEffectType.Buff, 10f);

            Assert.Throws<ArgumentNullException>(() => _manager.ApplyEffect(null, effect));
        }

        [Test]
        public void ApplyEffect_NullEffect_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => _manager.ApplyEffect(_target, null));
        }

        #endregion

        #region Duration & Timing Tests

        [Test]
        public void UpdateEffects_TimedEffect_DecreasesRemainingDuration()
        {
            var effect = new MockEffect("buff1", "Timed Buff", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect);

            _manager.UpdateEffects(_target, 5f);

            Assert.That(effect.RemainingDuration, Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void UpdateEffects_EffectExpires_RemovesEffect()
        {
            var effect = new MockEffect("buff1", "Timed Buff", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect);

            _manager.UpdateEffects(_target, 15f);

            Assert.That(_manager.HasEffect(_target, "buff1"), Is.False);
            Assert.That(effect.IsActive, Is.False);
        }

        [Test]
        public void UpdateEffects_PermanentEffect_NeverExpires()
        {
            var effect = new MockEffect("buff1", "Permanent Buff", StatusEffectType.Buff, -1f);
            _manager.ApplyEffect(_target, effect);

            _manager.UpdateEffects(_target, 1000f);

            Assert.That(_manager.HasEffect(_target, "buff1"), Is.True);
            Assert.That(effect.RemainingDuration, Is.EqualTo(-1f));
        }

        [Test]
        public void RefreshDuration_TimedEffect_ResetsToMax()
        {
            var effect = new MockEffect("buff1", "Timed Buff", StatusEffectType.Buff, 10f);
            _manager.ApplyEffect(_target, effect);
            _manager.UpdateEffects(_target, 5f);

            effect.RefreshDuration();

            Assert.That(effect.RemainingDuration, Is.EqualTo(10f));
        }

        [Test]
        public void TimedStatusEffect_ZeroDuration_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() =>
                new TimedStatusEffect("buff1", "Invalid", StatusEffectType.Buff, 0f));
        }

        [Test]
        public void TimedStatusEffect_NegativeDuration_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() =>
                new TimedStatusEffect("buff1", "Invalid", StatusEffectType.Buff, -5f));
        }

        #endregion

        #region Stacking Behavior Tests

        [Test]
        public void StackBehavior_Stack_AllowsMultipleInstances()
        {
            var effect1 = new MockEffect("poison", "Poison", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.Stack);
            var effect2 = new MockEffect("poison", "Poison", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.Stack);

            _manager.ApplyEffect(_target, effect1);
            _manager.ApplyEffect(_target, effect2);

            Assert.That(_manager.GetActiveEffects(_target).Count, Is.EqualTo(2));
        }

        [Test]
        public void StackBehavior_Replace_ReplacesOldWithNew()
        {
            var effect1 = new MockEffect("buff", "Buff", StatusEffectType.Buff, 5f,
                stackBehavior: StackBehavior.Replace);
            var effect2 = new MockEffect("buff", "Buff", StatusEffectType.Buff, 10f,
                stackBehavior: StackBehavior.Replace);

            _manager.ApplyEffect(_target, effect1);
            _manager.ApplyEffect(_target, effect2);

            var effects = _manager.GetActiveEffects(_target);
            Assert.That(effects.Count, Is.EqualTo(1));
            Assert.That(effects[0].Duration, Is.EqualTo(10f));
            Assert.That(effect1.IsActive, Is.False);
        }

        [Test]
        public void StackBehavior_Refresh_KeepsExistingRefreshesDuration()
        {
            var effect1 = new MockEffect("buff", "Buff", StatusEffectType.Buff, 10f,
                stackBehavior: StackBehavior.Refresh);
            var effect2 = new MockEffect("buff", "Buff", StatusEffectType.Buff, 10f,
                stackBehavior: StackBehavior.Refresh);

            _manager.ApplyEffect(_target, effect1);
            _manager.UpdateEffects(_target, 5f);
            _manager.ApplyEffect(_target, effect2);

            Assert.That(effect1.RemainingDuration, Is.EqualTo(10f));
            Assert.That(_manager.GetActiveEffects(_target).Count, Is.EqualTo(1));
        }

        [Test]
        public void StackBehavior_Ignore_KeepsExistingIgnoresNew()
        {
            var effect1 = new MockEffect("stun", "Stun", StatusEffectType.Stun, 5f,
                stackBehavior: StackBehavior.Ignore);
            var effect2 = new MockEffect("stun", "Stun", StatusEffectType.Stun, 10f,
                stackBehavior: StackBehavior.Ignore);

            _manager.ApplyEffect(_target, effect1);
            bool applied = _manager.ApplyEffect(_target, effect2);

            Assert.That(applied, Is.False);
            Assert.That(_manager.GetActiveEffects(_target).Count, Is.EqualTo(1));
            Assert.That(_manager.GetActiveEffects(_target)[0].Duration, Is.EqualTo(5f));
        }

        [Test]
        public void StackBehavior_StackCount_IncrementsCounter()
        {
            var effect1 = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5);
            var effect2 = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5);

            _manager.ApplyEffect(_target, effect1);
            _manager.ApplyEffect(_target, effect2);

            Assert.That(effect1.StackCount, Is.EqualTo(2));
            Assert.That(_manager.GetActiveEffects(_target).Count, Is.EqualTo(1));
        }

        [Test]
        public void StackBehavior_StackCount_MaxStacksEnforced()
        {
            var effect = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 3);

            _manager.ApplyEffect(_target, effect);
            _manager.ApplyEffect(_target, new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 3));
            _manager.ApplyEffect(_target, new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 3));
            bool exceeded = _manager.ApplyEffect(_target, new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 3));

            Assert.That(effect.StackCount, Is.EqualTo(3));
            Assert.That(exceeded, Is.False);
        }

        [Test]
        public void StackBehavior_StackCount_RefreshesDuration()
        {
            var effect1 = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5);
            var effect2 = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5);

            _manager.ApplyEffect(_target, effect1);
            _manager.UpdateEffects(_target, 5f);
            _manager.ApplyEffect(_target, effect2);

            Assert.That(effect1.RemainingDuration, Is.EqualTo(10f));
        }

        [Test]
        public void RemoveStack_StackCount_DecrementsCounter()
        {
            var effect = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5);

            _manager.ApplyEffect(_target, effect);
            _manager.ApplyEffect(_target, new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5));
            effect.RemoveStack();

            Assert.That(effect.StackCount, Is.EqualTo(1));
        }

        [Test]
        public void RemoveStack_SingleStack_ReturnsFalse()
        {
            var effect = new MockEffect("bleed", "Bleed", StatusEffectType.DamageOverTime, 10f,
                stackBehavior: StackBehavior.StackCount, maxStacks: 5);

            _manager.ApplyEffect(_target, effect);
            bool removed = effect.RemoveStack();

            Assert.That(removed, Is.False);
            Assert.That(effect.StackCount, Is.EqualTo(1));
        }

        #endregion

        #region Recurring Effects Tests

        [Test]
        public void RecurringEffect_UpdateTriggersTicks()
        {
            var effect = new MockRecurringEffect("poison", "Poison", StatusEffectType.DamageOverTime,
                duration: 10f, tickInterval: 1f, tickValue: 5f);

            _manager.ApplyEffect(_target, effect);
            _manager.UpdateEffects(_target, 2.5f);

            Assert.That(effect.TickCount, Is.EqualTo(2));
            Assert.That(_target.DamageReceived, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void RecurringEffect_PartialTick_DoesNotTrigger()
        {
            var effect = new MockRecurringEffect("poison", "Poison", StatusEffectType.DamageOverTime,
                duration: 10f, tickInterval: 1f, tickValue: 5f);

            _manager.ApplyEffect(_target, effect);
            _manager.UpdateEffects(_target, 0.5f);

            Assert.That(effect.TickCount, Is.EqualTo(0));
            Assert.That(_target.DamageReceived, Is.EqualTo(0f));
        }

        [Test]
        public void RecurringEffect_MultipleTicks_Accumulates()
        {
            var effect = new MockRecurringEffect("poison", "Poison", StatusEffectType.DamageOverTime,
                duration: 10f, tickInterval: 1f, tickValue: 5f);

            _manager.ApplyEffect(_target, effect);
            _manager.UpdateEffects(_target, 1f);
            _manager.UpdateEffects(_target, 1f);
            _manager.UpdateEffects(_target, 1f);

            Assert.That(effect.TickCount, Is.EqualTo(3));
            Assert.That(_target.DamageReceived, Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void RecurringEffect_InvalidTickInterval_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() =>
                new MockRecurringEffect("poison", "Poison", StatusEffectType.DamageOverTime,
                    duration: 10f, tickInterval: 0f, tickValue: 5f));
        }

        #endregion

        #region Validation Tests

        [Test]
        public void StatusEffect_NullEffectId_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MockEffect(null, "Test", StatusEffectType.Buff, 10f));
        }

        [Test]
        public void StatusEffect_EmptyEffectId_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MockEffect("", "Test", StatusEffectType.Buff, 10f));
        }

        [Test]
        public void StatusEffect_NullName_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MockEffect("buff1", null, StatusEffectType.Buff, 10f));
        }

        [Test]
        public void StatusEffect_ZeroMaxStacks_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() =>
                new MockEffect("buff1", "Test", StatusEffectType.Buff, 10f, maxStacks: 0));
        }

        #endregion

        #region Mock Classes

        private class MockTarget : IStatusEffectTarget
        {
            public string TargetId { get; }
            public bool CanReceive { get; set; } = true;
            public float DamageReceived { get; private set; }
            public float HealingReceived { get; private set; }

            public MockTarget(string id)
            {
                TargetId = id;
            }

            public bool CanReceiveEffect(IStatusEffect effect) => CanReceive;

            public float GetStat(string statName) => 100f;

            public void ApplyDamage(float damage, DamageType damageType)
            {
                DamageReceived += damage;
            }

            public void ApplyHealing(float healing)
            {
                HealingReceived += healing;
            }
        }

        private class MockEffect : StatusEffect
        {
            public MockEffect(
                string effectId,
                string name,
                StatusEffectType effectType,
                float duration,
                StackBehavior stackBehavior = StackBehavior.Replace,
                int maxStacks = 1)
                : base(effectId, name, effectType, duration, stackBehavior, maxStacks)
            {
            }
        }

        private class MockRecurringEffect : RecurringStatusEffect
        {
            public int TickCount { get; private set; }

            public MockRecurringEffect(
                string effectId,
                string name,
                StatusEffectType effectType,
                float duration,
                float tickInterval,
                float tickValue)
                : base(effectId, name, effectType, duration, tickInterval, tickValue)
            {
            }

            protected override void ExecuteTick(IStatusEffectTarget target)
            {
                TickCount++;
                target.ApplyDamage(TickValue, DamageType.Pure);
            }
        }

        #endregion
    }
}
