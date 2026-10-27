using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Abilities;
using RPGFramework.Bootstrap;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.StatusEffects;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Status effects and abilities in the action RPG, through the real framework modules: poison ticks as effect
    /// damage and counts for the poisoner, buffs change stats and run out, slows and stuns land on the actor,
    /// effects end with their character; abilities check cooldowns, run the game's effect, can be cut short by a
    /// stun, and cooldowns can be shortened. A game without them gets neither.
    /// </summary>
    public sealed class ActorEffectsTests
    {
        private const string Goblin = "goblin";
        private const string Poison = "poison";
        private const string Rage = "rage";
        private const string Slow = "slow";
        private const string Stun = "stun";
        private const string Strike = "strike";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData
            {
                Id = Goblin, DisplayName = "Goblin", MaxHealth = 30f, Attack = 6f, Defense = 100f, ExperienceReward = 40
            });
            _content = new ActionRpgContent();
            _content.StatusEffects.Add(StatusEffectData.CreateDoT(Poison, "Poison", duration: 10f, tickInterval: 1f, damagePerTick: 4f));
            _content.StatusEffects.Add(StatusEffectData.CreateStatModifier(Rage, "Rage", EffectType.Buff, 2f, "Attack", 10f));
            var slow = StatusEffectData.Create(Slow, "Slow", EffectType.Debuff);
            slow.Duration = 3f;
            slow.Modifiers.Add(EffectModifierData.Create(ModifierType.Movement, "Speed", 0.5f, ModifierOperation.Multiply));
            _content.StatusEffects.Add(slow);
            var stun = StatusEffectData.Create(Stun, "Stun", EffectType.Debuff);
            stun.Duration = 1.5f;
            stun.Modifiers.Add(EffectModifierData.Create(ModifierType.Control, "Stun", 1f));
            _content.StatusEffects.Add(stun);
            _content.Abilities.Add(new Ability(new AbilityMetadata(Strike, "Strike", cooldown: 4f), targetType: TargetType.Enemy));
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame() =>
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds - 0.001f; time += 0.1f)
                _host.Update(0.1f);
        }

        [Test]
        public void Poison_TicksAsEffectDamage_ThroughArmour_AndTheKillIsThePoisoners()
        {
            StartGame();
            var hits = new List<ActorDamagedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorDamagedEvent>(hits.Add);
            int goblin = World.SpawnEnemy(Goblin);

            Assert.That(World.Effects.Apply(goblin, Poison, World.PlayerId), Is.True);
            Assert.That(World.EffectsOn(goblin).Single().SourceId, Is.EqualTo(World.PlayerId));
            Tick(1.05f);

            var tick = hits.Single();
            Assert.That(tick.Amount, Is.EqualTo(4f), "defense 100 does not stop poison");
            Assert.That(tick.AttackerId, Is.EqualTo(World.PlayerId));
            Assert.That(tick.Context.FromEffect, Is.True);
            Assert.That(tick.Context.SourceId, Is.EqualTo(ActorEffects.SourceIdOf(Poison)));

            Tick(8f);

            Assert.That(World.Get(goblin), Is.Null, "30 health is 8 ticks of 4");
            Assert.That(World.Kills, Is.EqualTo(1));
            Assert.That(World.TotalExperience, Is.EqualTo(40), "the poisoner earned it");
        }

        [Test]
        public void Buff_ChangesTheStat_AndRunsOut()
        {
            StartGame();
            var removed = new List<EffectRemovedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<EffectRemovedEvent>(removed.Add);

            World.Effects.Apply(World.PlayerId, Rage, World.PlayerId);
            Assert.That(World.Player.Attack, Is.EqualTo(60f));
            Assert.That(World.Effects.Has(World.PlayerId, Rage), Is.True);

            Tick(2.2f);

            Assert.That(World.Player.Attack, Is.EqualTo(50f));
            Assert.That(World.EffectsOn(World.PlayerId), Is.Empty);
            Assert.That(removed.Single().EffectId, Is.EqualTo(Rage));
        }

        [Test]
        public void SlowAndStun_LandOnTheActor_AndEndWithTheFall()
        {
            StartGame();
            int goblin = World.SpawnEnemy(Goblin);

            World.Effects.Apply(World.PlayerId, Slow, goblin);
            World.Effects.Apply(World.PlayerId, Stun, goblin);

            Assert.That(World.Player.MoveMultiplier, Is.EqualTo(0.5f));
            Assert.That(World.Player.HasControl("Stun"), Is.True);

            World.DealDamage(goblin, World.PlayerId, 1000f);

            Assert.That(World.Player.IsAlive(), Is.False);
            Assert.That(World.EffectsOn(World.PlayerId), Is.Empty, "a fall clears effects");
            Assert.That(World.Player.MoveMultiplier, Is.EqualTo(1f));
            Assert.That(World.Player.HasControl("Stun"), Is.False);
            Assert.That(World.Effects.Apply(World.PlayerId, Slow), Is.False, "nothing lands on the fallen");
        }

        [Test]
        public void HealthResource_Hurts_AsEffectDamage()
        {
            var drain = StatusEffectData.Create("drain", "Drain", EffectType.Debuff);
            drain.Duration = 5f;
            drain.Modifiers.Add(EffectModifierData.Create(ModifierType.Resource, ActorEffectTarget.Health, -7f));
            _content.StatusEffects.Add(drain);
            StartGame();

            World.Effects.Apply(World.PlayerId, "drain");

            Assert.That(World.Player.Health, Is.EqualTo(93f));
        }

        [Test]
        public void Ability_RunsTheGamesEffect_ThenCoolsDown_AndCanBeCooledFaster()
        {
            StartGame();
            int goblin = World.SpawnEnemy(Goblin);
            var used = new List<(int Caster, int Target)>();
            World.Abilities.SetEffect(Strike, (caster, target) =>
            {
                used.Add((caster, target));
                World.DealDamage(caster, target, 5f, AttackContext.Effect(1));
            });

            Assert.That(World.UseAbility(Strike, goblin), Is.True);
            Assert.That(used, Is.EqualTo(new[] { (World.PlayerId, goblin) }));
            Assert.That(World.Get(goblin).Health, Is.EqualTo(25f));
            Assert.That(World.CooldownLeft(Strike), Is.EqualTo(4f).Within(0.01f));
            Assert.That(World.UseAbility(Strike, goblin), Is.False, "still cooling down");

            Tick(1f);
            World.Abilities.ReduceCooldowns(World.PlayerId, 0.5f);
            Assert.That(World.CooldownLeft(Strike), Is.EqualTo(1.5f).Within(0.05f));

            Tick(1.6f);
            Assert.That(World.UseAbility(Strike, goblin), Is.True);
            Assert.That(used, Has.Count.EqualTo(2));
        }

        [Test]
        public void Stunned_CannotUseAbilities()
        {
            StartGame();
            int goblin = World.SpawnEnemy(Goblin);
            World.Effects.Apply(World.PlayerId, Stun, goblin);

            Assert.That(World.UseAbility(Strike, goblin), Is.False);

            Tick(1.6f);
            Assert.That(World.UseAbility(Strike, goblin), Is.True, "the stun is over");
        }

        [Test]
        public void AGameWithoutThem_HasNeither()
        {
            _content.StatusEffects.Clear();
            _content.Abilities.Clear();
            StartGame();

            Assert.That(World.Effects, Is.Null);
            Assert.That(World.Abilities, Is.Null);
            Assert.That(World.EffectsOn(World.PlayerId), Is.Empty);
            Assert.That(World.UseAbility(Strike), Is.False);
            Assert.That(World.CooldownLeft(Strike), Is.Zero);
            Assert.That(_host.Container.IsRegistered<IStatusEffectManager>(), Is.False);
        }
    }
}
