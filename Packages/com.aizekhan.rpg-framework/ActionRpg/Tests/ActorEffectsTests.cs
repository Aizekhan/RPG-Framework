using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Abilities;
using RPGFramework.Bootstrap;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Logging;
using RPGFramework.StatusEffects;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

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
        public void Apply_WithPowerAndDuration_AndTheKillSaysItWasThePoison()
        {
            StartGame();
            var killed = new List<ActorKilledEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorKilledEvent>(killed.Add);
            int weak = World.SpawnEnemy(Goblin);
            int strong = World.SpawnEnemy(Goblin);

            Assert.That(World.Effects.Apply(weak, Poison, World.PlayerId, power: 2f, duration: 2.5f), Is.True);
            Assert.That(World.Effects.Apply(strong, Poison, World.PlayerId, power: 4f), Is.True);
            Tick(3.05f);

            Assert.That(World.Get(weak).Health, Is.EqualTo(14f), "2 ticks of 8, then it ran out");
            Assert.That(World.EffectsOn(weak), Is.Empty);
            Assert.That(World.Get(strong), Is.Null, "2 ticks of 16");
            var kill = killed.Single();
            Assert.That(kill.EntityId, Is.EqualTo(strong));
            Assert.That(kill.Context.FromEffect, Is.True);
            Assert.That(kill.Context.SourceId, Is.EqualTo(ActorEffects.SourceIdOf(Poison)));
        }

        [Test]
        public void Pause_StopsEffectsAndTheirTime_UntilTheLastReasonIsLifted()
        {
            StartGame();
            var pause = _host.Container.Resolve<RPGFramework.Systems.IGamePause>();
            var paused = new List<RPGFramework.Systems.GamePausedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<RPGFramework.Systems.GamePausedEvent>(paused.Add);
            int goblin = World.SpawnEnemy(Goblin);
            World.Effects.Apply(goblin, Poison, World.PlayerId);

            Assert.That(pause.Pause("vote"), Is.True);
            Assert.That(pause.Pause("menu"), Is.True);
            Assert.That(World.IsPaused, Is.True);
            Tick(5f);
            Assert.That(World.Get(goblin).Health, Is.EqualTo(30f), "no poison ticks while paused");
            Assert.That(World.Effects.Time, Is.EqualTo(0f));

            pause.Resume("vote");
            Tick(2f);
            Assert.That(World.Get(goblin).Health, Is.EqualTo(30f), "the menu still pauses it");

            pause.Resume("menu");
            Assert.That(World.IsPaused, Is.False);
            Tick(1.05f);
            Assert.That(World.Get(goblin).Health, Is.EqualTo(26f));
            Assert.That(paused.Select(p => p.Reason), Is.EqualTo(new[] { "vote" }), "one pause, however many reasons");
        }

        [Test]
        [Retry(3)] // the editor sometimes allocates on the test thread mid-measure (seen in CI); a real allocation repeats
        public void AQuietFrame_AllocatesNothing()
        {
            // MythRunner FG-14: a game with effects, none on anyone, paid for them every frame
            StartGame();
            for (int i = 0; i < 20; i++)
                World.SpawnEnemy(Goblin);
            World.Effects.Update(0.016f); // warm up

            Assert.That(() => World.Effects.Update(0.016f), Is.Not.AllocatingGCMemory());
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
        public void AGameOffFree3D_MeasuresRange_PicksTargets_AndAddsItsOwnActions()
        {
            // A runner: characters stand at a distance along the track; the game measures, aims and acts
            _content.Abilities.Add(new Ability(new AbilityMetadata("ram", "Ram", cooldown: 2f, range: 5f), targetType: TargetType.Enemy));
            _content.Abilities.Add(new Ability(new AbilityMetadata("lane", "Change lane", cooldown: 1f), targetType: TargetType.None));
            var track = new Dictionary<int, float>();
            _host.Start(builder =>
            {
                _module = ActionRpgComposition.Configure(builder, _settings, _content);
                _module.Distance = (a, b) => System.Math.Abs(track[a] - track[b]);
            });
            int near = World.SpawnEnemy(Goblin), far = World.SpawnEnemy(Goblin);
            track[World.PlayerId] = 0f;
            track[near] = 4f;
            track[far] = 30f;
            var rammed = new List<int>();
            int lane = 1;
            World.Abilities.SetEffect("ram", (caster, target) => rammed.Add(target));
            World.Abilities.SetEffect("lane", (caster, target) => lane++);
            World.Abilities.ResolveTarget = (caster, ability) =>
                ability.Range > 0f ? track.Where(t => t.Key != caster).OrderBy(t => t.Value).First().Key : -1;

            Assert.That(World.UseAbility("ram", far), Is.False, "30 along the track is out of range");
            Assert.That(World.UseAbility("ram"), Is.True, "the game aims at the nearest one ahead");
            Assert.That(rammed, Is.EqualTo(new[] { near }));

            Assert.That(World.UseAbility("lane"), Is.True);
            Assert.That(lane, Is.EqualTo(2), "an action the framework does not know, with its cooldown");
            Assert.That(World.UseAbility("lane"), Is.False, "cooling down");
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
