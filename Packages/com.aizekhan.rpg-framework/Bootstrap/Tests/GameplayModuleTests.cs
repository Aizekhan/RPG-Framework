using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Abilities;
using RPGFramework.Bootstrap.Gameplay;
using RPGFramework.Logging;
using RPGFramework.StatusEffects;

namespace RPGFramework.Bootstrap.Tests
{
    /// <summary>
    /// The Abilities and StatusEffects feature modules give a game working services from the container alone.
    /// </summary>
    public sealed class GameplayModuleTests
    {
        private GameHost _host;

        [SetUp]
        public void SetUp() => _host = new GameHost(new Logger(LogSeverity.Warning, new MemoryLogSink()));

        [TearDown]
        public void TearDown() => _host.Dispose();

        [Test]
        public void AbilitiesModule_ExecutorUsesTheRegisteredServicesAndTheOptions()
        {
            const int hero = 1, wolf = 2, friend = 3;
            _host.Start(b => b.UseModule(new AbilitiesModule(new AbilitiesOptions
            {
                RelationshipCheck = (caster, target) => target == wolf
            })));
            var c = _host.Container;
            c.Resolve<IAbilityRegistry>().RegisterAbility(new Ability(
                new AbilityMetadata("bite", "Bite", AbilityType.Active, 3f, 0f), new List<ResourceCost>(),
                new CastInfo(0f, 0f, true), TargetType.Enemy));
            var executor = c.Resolve<IAbilityExecutor>();

            Assert.That(executor, Is.SameAs(c.Resolve<IAbilityExecutor>()));
            Assert.That(executor.ExecuteAbility("bite", hero, friend, 0f, null, null, null).Success, Is.False, "not an enemy");
            Assert.That(executor.ExecuteAbility("bite", hero, wolf, 0f, null, null, null).Success, Is.True);
            Assert.That(c.Resolve<ICooldownTracker>().IsOnCooldown(hero, "bite", 1f), Is.True);
            Assert.That(c.Resolve<ITargetValidator>(), Is.Not.Null);
        }

        [Test]
        public void StatusEffectsModule_ManagerRegistersAndAppliesEffectsOnCoreEntities()
        {
            var target = new RecordingTarget();
            _host.Start(b => b.UseModule(new StatusEffectsModule(new StatusEffectsOptions { Target = target })));
            var manager = _host.Container.Resolve<IStatusEffectManager>();
            int entity = _host.World.Entities.CreateEntity();

            Assert.That(manager.RegisterEffect(StatusEffectData.CreateStatModifier("rage", "Rage", EffectType.Buff, 5f, "Attack", 3f)), Is.True);
            Assert.That(manager.ApplyEffect(entity, "rage", "test", 0f), Is.True);

            Assert.That(manager.HasEffect(entity, "rage"), Is.True);
            Assert.That(target.Applied, Is.EqualTo(new[] { "Attack +3" }));
        }

        private sealed class RecordingTarget : IStatusEffectTarget
        {
            public readonly List<string> Applied = new List<string>();

            public void ApplyStatModifier(int entityId, string modifierId, string statId, ModifierOperation operation, float value) =>
                Applied.Add($"{statId} +{value}");

            public void RemoveStatModifier(int entityId, string modifierId) { }
            public bool TryGetResource(int entityId, string resourceId, out float value) { value = 0f; return false; }
            public void ChangeResource(int entityId, string resourceId, float amount) { }
            public void SetMovementMultiplier(int entityId, float multiplier) { }
            public void SetControl(int entityId, string controlId, bool active) { }
        }
    }
}
