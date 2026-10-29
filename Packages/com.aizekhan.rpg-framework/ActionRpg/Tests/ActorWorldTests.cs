using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Quests;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The action RPG rules without Unity, through the real framework modules: kills give experience, loot and quest
    /// progress; quests turn in and reward; level-ups grow stats; waves come, grow and wait for a dead player.
    /// </summary>
    public sealed class ActorWorldTests
    {
        private const string Goblin = "goblin";
        private const string Ear = "goblin_ear";
        private const string Medal = "medal";
        private const string HuntQuest = "hunt";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private readonly List<ActorSpawnedEvent> _spawned = new List<ActorSpawnedEvent>();
        private readonly List<ActorDespawnedEvent> _despawned = new List<ActorDespawnedEvent>();
        private readonly List<WaveStartedEvent> _waves = new List<WaveStartedEvent>();

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData
                {
                    Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f,
                    HealthPerLevel = 20f, AttackPerLevel = 3f, DefensePerLevel = 1f
                },
                Waves = new WaveSettings { FirstWaveDelay = 1f, DelayBetweenWaves = 2f, FirstWaveSize = 2, AddedPerWave = 1, MaxWaveSize = 3 },
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData
            {
                Id = Goblin, DisplayName = "Goblin", MaxHealth = 30f, Attack = 6f, Defense = 2f, ExperienceReward = 40,
                LootTableId = Goblin
            });
            _settings.StartingQuests.Add(HuntQuest);

            _content = new ActionRpgContent();
            _content.Items.Add(new Item(Ear, "Goblin Ear", "Proof", ItemType.Crafting, maxStackSize: 99));
            _content.Items.Add(new Item(Medal, "Medal", "For the hunt", ItemType.Quest));
            _content.LootTables.Add(LootTableBuilder.Create(Goblin, "Goblin").AddGuaranteedDrop(Ear).WithDrops(0, 0).Build());
            _content.Quests.Add(new Quest(HuntQuest, "The Hunt", "Slay 2 goblins",
                new List<IQuestObjective> { new QuestObjective("slay", ObjectiveType.Kill, Goblin, 2, "Slay goblins") },
                new List<IQuestReward> { new QuestReward(RewardType.Experience, null, 100), new QuestReward(RewardType.Item, Medal, 1) },
                new List<string>(), minimumLevel: 1, isRepeatable: false));

            _spawned.Clear();
            _despawned.Clear();
            _waves.Clear();
        }

        [Test]
        public void Achievements_CountKillsItemsQuestsAndDamage_ForThePlayer()
        {
            RPGFramework.Achievements.Achievement Make(string id, RPGFramework.Achievements.CriterionType type, string target, int count) =>
                new RPGFramework.Achievements.Achievement(id, id, "", "", 10, RPGFramework.Achievements.AchievementTier.Common,
                    new List<RPGFramework.Achievements.Runtime.Contracts.IAchievementCriterion>
                    {
                        new RPGFramework.Achievements.AchievementCriterion("c", type, target, count)
                    },
                    new List<RPGFramework.Achievements.Runtime.IAchievementReward>(), new List<string>());
            _content.Achievements.Add(Make("first_blood", RPGFramework.Achievements.CriterionType.KillCount, Goblin, 1));
            _content.Achievements.Add(Make("earful", RPGFramework.Achievements.CriterionType.ItemCollected, Ear, 5));
            _content.Achievements.Add(Make("hunter", RPGFramework.Achievements.CriterionType.QuestCompleted, HuntQuest, 1));
            _content.Achievements.Add(Make("bruiser", RPGFramework.Achievements.CriterionType.DamageDealt, "", 50));
            StartGame();
            var unlocked = new List<RPGFramework.Achievements.AchievementUnlockedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<RPGFramework.Achievements.AchievementUnlockedEvent>(unlocked.Add);
            var achievements = World.Achievements;
            int hero = World.PlayerId;

            World.DealDamage(hero, World.SpawnEnemy(Goblin), 1000f);
            Assert.That(achievements.IsUnlocked(hero, "first_blood"), Is.True);
            Assert.That(achievements.IsUnlocked(hero, "bruiser"), Is.False, "the goblin had 30 health to lose");
            World.DealDamage(hero, World.SpawnEnemy(Goblin), 1000f);
            Assert.That(achievements.IsUnlocked(hero, "bruiser"), Is.True, "60 dealt");
            Assert.That(achievements.IsUnlocked(hero, "hunter"), Is.True, "two goblins finish the hunt");
            long ears = achievements.CountOf(hero, RPGFramework.Achievements.CriterionType.ItemCollected, Ear);
            World.GiveItem(Ear, 5);
            Assert.That(achievements.IsUnlocked(hero, "earful"), Is.True);
            Assert.That(achievements.CountOf(hero, RPGFramework.Achievements.CriterionType.ItemCollected, Ear), Is.EqualTo(ears + 5));
            Assert.That(unlocked.Select(u => u.AchievementId), Is.SupersetOf(new[] { "first_blood", "bruiser", "hunter", "earful" }));

            long before = achievements.CountOf(hero, RPGFramework.Achievements.CriterionType.ItemCollected, Ear);
            World.RestoreProgress(World.CaptureProgress());
            Assert.That(achievements.CountOf(hero, RPGFramework.Achievements.CriterionType.ItemCollected, Ear), Is.EqualTo(before),
                "a load does not count its items again");
        }

        [Test]
        public void CriticalChance_FromAStatusEffect_MakesStrikesCritical()
        {
            _settings.Enemies[0].MaxHealth = 1000f;
            _content.StatusEffects.Add(RPGFramework.StatusEffects.StatusEffectData.CreateStatModifier("keen", "Keen",
                RPGFramework.StatusEffects.EffectType.Buff, 10f, "CriticalChance", 1f));
            StartGame();
            var hits = new List<ActorDamagedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorDamagedEvent>(hits.Add);
            int goblin = World.SpawnEnemy(Goblin);

            World.Attack(World.PlayerId, goblin);
            Assert.That(hits.Last().Context.IsCritical, Is.False, "no chance, no critical");
            float normal = hits.Last().Amount;

            Assert.That(World.Effects.Apply(World.PlayerId, "keen", World.PlayerId), Is.True);
            Assert.That(World.Player.CriticalChance, Is.EqualTo(1f));
            World.Attack(World.PlayerId, goblin);

            Assert.That(hits.Last().Context.IsCritical, Is.True);
            Assert.That(hits.Last().Context.Multiplier, Is.EqualTo(1.5f), "the character's critical damage");
            Assert.That(hits.Last().Amount, Is.GreaterThan(normal));
        }

        [Test]
        public void Evasion_MakesStrikesMiss_AndLifeStealHealsTheAttacker()
        {
            _settings.Enemies[0].MaxHealth = 1000f;
            _settings.Enemies[0].LifeSteal = 0.5f;
            StartGame();
            var evaded = new List<ActorEvadedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorEvadedEvent>(evaded.Add);
            int goblin = World.SpawnEnemy(Goblin);
            World.Get(goblin).ApplyDamage(100f);

            float dealt = World.Attack(goblin, World.PlayerId);
            Assert.That(dealt, Is.GreaterThan(0f));
            Assert.That(World.Get(goblin).Health, Is.EqualTo(900f + dealt * 0.5f).Within(1e-3f), "half of it back");

            World.Player.SetBaseStat(ActorStat.Evasion, 1f);
            float health = World.Player.Health;
            Assert.That(World.Attack(goblin, World.PlayerId), Is.EqualTo(0f));
            Assert.That(World.Player.Health, Is.EqualTo(health));
            Assert.That(evaded.Single().EntityId, Is.EqualTo(World.PlayerId));
            Assert.That(evaded.Single().AttackerId, Is.EqualTo(goblin));
            Assert.That(World.DealDamage(goblin, World.PlayerId, 5f), Is.GreaterThan(0f),
                "other damage is not evaded");
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame()
        {
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));
            var bus = _host.Container.Resolve<IEventBus>();
            bus.Subscribe<ActorSpawnedEvent>(_spawned.Add);
            bus.Subscribe<ActorDespawnedEvent>(_despawned.Add);
            bus.Subscribe<WaveStartedEvent>(_waves.Add);
        }

        private void Tick(float seconds)
        {
            for (float time = 0f; time < seconds; time += 0.1f)
                _host.Update(0.1f);
        }

        private void Slay(int enemy)
        {
            for (int round = 0; round < 50 && World.Get(enemy) != null; round++)
                World.Attack(World.PlayerId, enemy);
        }

        [Test]
        public void Start_CreatesThePlayerWithStartingQuests()
        {
            StartGame();

            Assert.That(World.Player, Is.Not.Null);
            Assert.That(World.Player.IsPlayer, Is.True);
            Assert.That(World.Player.MaxHealth, Is.EqualTo(100f));
            Assert.That(World.Level, Is.EqualTo(1));
            Assert.That(World.ActiveQuests.Select(q => q.Quest.Id), Is.EqualTo(new[] { HuntQuest }));
            Assert.That(World.QuestTargetOf(World.PlayerId), Is.EqualTo("hero"));
        }

        [Test]
        public void Kill_GivesExperienceLootAndQuestProgress()
        {
            StartGame();
            int goblin = World.SpawnEnemy(Goblin);

            Slay(goblin);

            Assert.That(World.Get(goblin), Is.Null);
            Assert.That(World.Kills, Is.EqualTo(1));
            Assert.That(World.TotalExperience, Is.EqualTo(40));
            Assert.That(World.Inventory.Count, Is.EqualTo(1));
            var progress = World.ActiveQuests.Single().Progress;
            Assert.That(progress.ObjectiveProgress["slay"], Is.EqualTo(1));
            Assert.That(_despawned.Select(d => d.CharacterId), Is.EqualTo(new[] { Goblin }));
        }

        [Test]
        public void Loot_IsAnnounced_RolledThenEachItemHandedOut()
        {
            StartGame();
            var bus = _host.Container.Resolve<IEventBus>();
            var order = new List<string>();
            var generated = new List<LootGeneratedEvent>();
            var dropped = new List<LootDroppedEvent>();
            bus.Subscribe<LootGeneratedEvent>(e => { generated.Add(e); order.Add("generated"); });
            bus.Subscribe<LootDroppedEvent>(e => { dropped.Add(e); order.Add("dropped"); });
            int goblin = World.SpawnEnemy(Goblin);

            Slay(goblin);

            Assert.That(order, Is.EqualTo(new[] { "generated", "dropped" }));
            Assert.That(generated[0].TableId, Is.EqualTo(Goblin));
            Assert.That(generated[0].TriggerEntityId, Is.EqualTo(World.PlayerId));
            Assert.That(generated[0].SourceEntityId, Is.EqualTo(goblin));
            Assert.That(generated[0].ItemCount, Is.EqualTo(1));
            Assert.That(dropped[0].ItemId, Is.EqualTo(Ear));
            Assert.That(dropped[0].Quantity, Is.EqualTo(1));
            Assert.That(dropped[0].SourceEntityId, Is.EqualTo(goblin));
            Assert.That(dropped[0].ItemEntityId, Is.EqualTo(-1), "it went straight into the backpack");
        }

        [Test]
        public void Kill_IsAnnouncedWithTheCharacterAndKiller()
        {
            StartGame();
            var killed = new List<ActorKilledEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorKilledEvent>(killed.Add);
            int goblin = World.SpawnEnemy(Goblin);

            var axe = new AttackContext(tags: 4, sourceId: 9);
            for (int round = 0; round < 50 && World.Get(goblin) != null; round++)
                World.Attack(World.PlayerId, goblin, axe);

            Assert.That(killed.Count, Is.EqualTo(1));
            Assert.That(killed[0].EntityId, Is.EqualTo(goblin));
            Assert.That(killed[0].CharacterId, Is.EqualTo(Goblin));
            Assert.That(killed[0].ByPlayer, Is.True);
            Assert.That(killed[0].Context.SourceId, Is.EqualTo(9), "the blow that killed: the axe");
            Assert.That(killed[0].Context.HasAll(4), Is.True);
            Assert.That(killed[0].Context.FromEffect, Is.False);
        }

        [Test]
        public void ACriticalHit_MultipliesTheDamage_AndSaysSo()
        {
            // MythRunner FG-15: the game rolled the crit and folded it into the amount, so nobody knew
            StartGame();
            var damaged = new List<ActorDamagedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorDamagedEvent>(damaged.Add);
            int goblin = World.SpawnEnemy(Goblin);

            float plain = World.DealDamage(World.PlayerId, goblin, 4f, AttackContext.Effect(1));
            float boosted = World.DealDamage(World.PlayerId, goblin, 4f, AttackContext.Effect(1).AsCritical(2f).WithMultiplier(1.5f));

            Assert.That(plain, Is.EqualTo(4f));
            Assert.That(boosted, Is.EqualTo(12f), "a crit of 2 and a power strike of 1.5");
            Assert.That(damaged[0].Context.IsCritical, Is.False);
            Assert.That(damaged[0].Context.Multiplier, Is.EqualTo(1f));
            Assert.That(damaged[1].Context.IsCritical, Is.True);
            Assert.That(damaged[1].Context.Multiplier, Is.EqualTo(3f));
        }

        [Test]
        public void AttackContext_MultipliersCombine_AndSurviveTheOtherChanges()
        {
            var context = new AttackContext(tags: 2, sourceId: 7).WithMultiplier(1.2f).AsCritical(2f).WithTags(3).Between(1, 2);

            Assert.That(default(AttackContext).Multiplier, Is.EqualTo(1f));
            Assert.That(default(AttackContext).IsCritical, Is.False);
            Assert.That(context.Multiplier, Is.EqualTo(2.4f).Within(1e-5f));
            Assert.That(context.IsCritical, Is.True);
            Assert.That(context.Tags, Is.EqualTo(3));
            Assert.That(context.AttackerId, Is.EqualTo(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => context.WithMultiplier(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => context.AsCritical(-1f));
        }

        [Test]
        public void GiveItem_FillsTheBackpackAndRefusesUnknownItems()
        {
            StartGame();

            Assert.That(World.GiveItem(Ear, 3), Is.True);
            Assert.That(World.Inventory.Count, Is.EqualTo(3));
            Assert.That(World.GiveItem("nothing"), Is.False);
            Assert.That(World.ExperienceForNextLevel, Is.GreaterThan(World.TotalExperience));
        }

        [Test]
        public void FinishedQuest_IsTurnedInAndRewarded()
        {
            StartGame();

            Slay(World.SpawnEnemy(Goblin));
            Slay(World.SpawnEnemy(Goblin));

            Assert.That(World.CompletedQuests, Is.EqualTo(new[] { HuntQuest }));
            Assert.That(World.ActiveQuests, Is.Empty);
            Assert.That(World.TotalExperience, Is.EqualTo(40 + 40 + 100));
            Assert.That(World.Inventory.GetAllItems().Any(i => i.ItemId == Medal), Is.True);
        }

        [Test]
        public void LevelUp_GrowsStatsAndHeals()
        {
            StartGame();
            World.SpawnEnemy(Goblin);
            World.Attack(World.LivingEnemies.First(), World.PlayerId);
            Assume.That(World.Player.Health, Is.LessThan(World.Player.MaxHealth));

            Slay(World.LivingEnemies.First());
            Slay(World.SpawnEnemy(Goblin));

            Assert.That(World.Level, Is.GreaterThan(1));
            float expected = 100f + 20f * (World.Level - 1);
            Assert.That(World.Player.MaxHealth, Is.EqualTo(expected));
            Assert.That(World.Player.Health, Is.EqualTo(World.Player.MaxHealth));
            Assert.That(World.Player.Attack, Is.EqualTo(50f + 3f * (World.Level - 1)));
        }

        [Test]
        public void Waves_StartGrowAndWaitForTheDeadPlayer()
        {
            StartGame();

            Tick(0.5f);
            Assert.That(World.LivingEnemies, Is.Empty);
            Tick(0.7f);
            Assert.That(World.CurrentWave, Is.EqualTo(1));
            Assert.That(World.LivingEnemies.Count(), Is.EqualTo(2));

            foreach (int enemy in World.LivingEnemies.ToList())
                Slay(enemy);
            Tick(2.2f);
            Assert.That(World.CurrentWave, Is.EqualTo(2));
            Assert.That(World.LivingEnemies.Count(), Is.EqualTo(3));

            foreach (int enemy in World.LivingEnemies.ToList())
                Slay(enemy);
            _host.Container.Resolve<ILifeStateManager>().Kill(World.PlayerId, -1, "test");
            Tick(5f);
            Assert.That(World.CurrentWave, Is.EqualTo(2), "no waves while the player is dead");

            Assert.That(World.RevivePlayer(), Is.True);
            Assert.That(World.Player.Health, Is.EqualTo(World.Player.MaxHealth));
            Tick(2.2f);
            Assert.That(World.CurrentWave, Is.EqualTo(3));
            Assert.That(_waves.Select(w => w.Enemies), Is.EqualTo(new[] { 2, 3, 3 }));
        }

        [Test]
        public void Spawns_AreAnnouncedAndShutdownDespawnsEveryone()
        {
            StartGame();
            World.SpawnEnemy(Goblin);

            _host.Dispose();

            Assert.That(_despawned.Count, Is.EqualTo(2));
            Assert.That(_despawned.Count(d => d.IsPlayer), Is.EqualTo(1));
        }

        [Test]
        public void DisabledWaves_SpawnNothing()
        {
            _settings.Waves.Enabled = false;
            StartGame();

            Tick(10f);

            Assert.That(World.CurrentWave, Is.Zero);
        }

        [Test]
        public void Validate_GoodSettings_HasNoProblems()
        {
            Assert.That(_settings.Validate(_content), Is.Empty);
        }

        [Test]
        public void Validate_ExplainsBrokenContent()
        {
            _settings.Enemies[0].LootTableId = "missing_table";
            _settings.StartingQuests.Add("missing_quest");
            _content.Items.RemoveAll(i => i.ItemId == Medal);
            _content.Quests.Add(new Quest("other", "Other", "",
                new List<IQuestObjective> { new QuestObjective("o", ObjectiveType.Kill, "dragon", 1, "Slay a dragon") },
                new List<IQuestReward>(), new List<string>(), minimumLevel: 1, isRepeatable: false));

            var problems = _settings.Validate(_content);

            Assert.That(problems, Has.Some.Contains("missing_table"));
            Assert.That(problems, Has.Some.Contains("missing_quest"));
            Assert.That(problems, Has.Some.Contains("'medal'"));
            Assert.That(problems, Has.Some.Contains("'dragon'"));
        }

        [Test]
        public void Validate_NoPlayerAndWavesWithoutEnemies()
        {
            _settings.Player = null;
            _settings.Enemies.Clear();

            var problems = _settings.Validate(_content);

            Assert.That(problems, Has.Some.Contains("No player"));
            Assert.That(problems, Has.Some.Contains("no enemies"));
        }

        [Test]
        public void JoiningPlayer_EntersAtThePartyLevel()
        {
            StartGame();
            Slay(World.SpawnEnemy(Goblin));
            Slay(World.SpawnEnemy(Goblin));
            Assume.That(World.Level, Is.GreaterThan(1));

            int friend = World.AddPlayer();

            var actor = World.Get(friend);
            Assert.That(actor.IsPlayer, Is.True);
            Assert.That(actor.MaxHealth, Is.EqualTo(World.Player.MaxHealth));
            Assert.That(World.Players, Is.EqualTo(new[] { World.PlayerId, friend }));
            Assert.That(World.LivingPlayers, Is.EquivalentTo(new[] { World.PlayerId, friend }));
            Assert.That(_spawned.Last().IsPlayer, Is.True);
        }

        [Test]
        public void PartyMemberKill_CountsForTheParty()
        {
            StartGame();
            int friend = World.AddPlayer();
            var killed = new List<ActorKilledEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorKilledEvent>(killed.Add);

            int goblin = World.SpawnEnemy(Goblin);
            for (int hit = 0; hit < 50 && World.Get(goblin) != null; hit++)
                World.Attack(friend, goblin);

            Assert.That(killed.Single().ByPlayer, Is.True);
            Assert.That(World.TotalExperience, Is.EqualTo(40), "experience goes to the party");
            Assert.That(World.Inventory.Count, Is.EqualTo(1), "loot goes to the party backpack");
            Assert.That(World.ActiveQuests.Single().Progress.ObjectiveProgress["slay"], Is.EqualTo(1));
        }

        [Test]
        public void LevelUp_GrowsAndHealsTheWholeParty()
        {
            StartGame();
            int friend = World.AddPlayer();
            World.Attack(World.SpawnEnemy(Goblin), friend);
            Assume.That(World.Get(friend).Health, Is.LessThan(World.Get(friend).MaxHealth));

            foreach (int enemy in World.LivingEnemies.ToList())
                Slay(enemy);
            Slay(World.SpawnEnemy(Goblin));

            Assert.That(World.Level, Is.GreaterThan(1));
            var member = World.Get(friend);
            Assert.That(member.MaxHealth, Is.EqualTo(World.Player.MaxHealth));
            Assert.That(member.Attack, Is.EqualTo(World.Player.Attack));
            Assert.That(member.Health, Is.EqualTo(member.MaxHealth));
        }

        [Test]
        public void Waves_GoOnWhileAnyoneInThePartyStands()
        {
            StartGame();
            int friend = World.AddPlayer();
            var lifeStates = _host.Container.Resolve<ILifeStateManager>();
            lifeStates.Kill(World.PlayerId, -1, "test");

            Tick(1.5f);
            Assert.That(World.CurrentWave, Is.EqualTo(1), "the friend still stands");

            foreach (int enemy in World.LivingEnemies.ToList())
                lifeStates.Kill(enemy, friend, "test");
            lifeStates.Kill(friend, -1, "test");
            Tick(5f);
            Assert.That(World.CurrentWave, Is.EqualTo(1), "everyone is down");

            Assert.That(World.RevivePlayer(friend), Is.True);
            Tick(2.2f);
            Assert.That(World.CurrentWave, Is.EqualTo(2));
        }

        [Test]
        public void LeavingPlayer_IsRemoved_TheFirstPlayerCannotLeave()
        {
            StartGame();
            int friend = World.AddPlayer();

            Assert.That(World.RemovePlayer(friend), Is.True);
            Assert.That(World.Get(friend), Is.Null);
            Assert.That(World.Players, Is.EqualTo(new[] { World.PlayerId }));
            Assert.That(_despawned.Last().EntityId, Is.EqualTo(friend));
            Assert.That(() => World.RemovePlayer(World.PlayerId), Throws.InvalidOperationException);
        }
    
        /// <summary>
        /// Head hits (tag 1) hurt twice as much; effect damage is a flat amount.
        /// </summary>
        private sealed class ZoneCalculator : DamageCalculator
        {
            public readonly List<AttackContext> Seen = new List<AttackContext>();

            public override float CalculateDamage(float baseDamage, float defenseValue, AttackContext context)
            {
                Seen.Add(context);
                if (context.FromEffect)
                    return baseDamage;
                return context.HasAny(1) ? 20f : 10f;
            }
        }

        [Test]
        public void Attack_WithAContext_TheGamesCalculatorSeesTheWholeHit_AndTheEventCarriesIt()
        {
            var calculator = new ZoneCalculator();
            _settings.DamageCalculator = calculator;
            _settings.Enemies[0].MaxHealth = 100f;
            StartGame();
            var hits = new List<ActorDamagedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<ActorDamagedEvent>(hits.Add);
            int goblin = World.SpawnEnemy(Goblin);

            Assert.That(World.Attack(World.PlayerId, goblin), Is.EqualTo(10f), "no tags: a plain strike");
            Assert.That(World.Attack(World.PlayerId, goblin, new AttackContext(tags: 1, sourceId: 42)), Is.EqualTo(20f));

            var head = calculator.Seen.Last();
            Assert.That(head.AttackerId, Is.EqualTo(World.PlayerId));
            Assert.That(head.TargetId, Is.EqualTo(goblin));
            Assert.That(head.SourceId, Is.EqualTo(42));
            Assert.That(hits.Last().Context.Tags, Is.EqualTo(1));
            Assert.That(hits.Last().Context.SourceId, Is.EqualTo(42));
            Assert.That(hits.Last().Context.FromEffect, Is.False);
            Assert.That(hits.First().Context.Tags, Is.Zero);
        }

        [Test]
        public void DealDamage_FromAnEffect_IgnoresDefense_IsMarked_AndCanKill()
        {
            _settings.Enemies[0].Defense = 100f;
            StartGame();
            var hits = new List<ActorDamagedEvent>();
            var deaths = new List<DeathEvent>();
            var bus = _host.Container.Resolve<IEventBus>();
            bus.Subscribe<ActorDamagedEvent>(hits.Add);
            bus.Subscribe<DeathEvent>(deaths.Add);
            int goblin = World.SpawnEnemy(Goblin);

            Assert.That(World.DealDamage(World.PlayerId, goblin, 12f, AttackContext.Effect(sourceId: 7)), Is.EqualTo(12f),
                "the framework's calculator lets effect damage through armour");
            Assert.That(hits.Single().Context.FromEffect, Is.True);
            Assert.That(hits.Single().Context.SourceId, Is.EqualTo(7));
            Assert.That(World.DealDamage(World.PlayerId, goblin, 12f), Is.LessThan(12f), "a plain hit meets defense");

            World.DealDamage(World.PlayerId, goblin, 100f, AttackContext.Effect(7));

            Assert.That(World.Get(goblin), Is.Null);
            Assert.That(World.Kills, Is.EqualTo(1));
            Assert.That(deaths.Single().KillerEntityId, Is.EqualTo(World.PlayerId), "the poisoner gets the kill");
        }

        [Test]
        public void DealDamage_FromNobody_Hurts_ButNeverAnAlly()
        {
            StartGame();
            int friend = World.AddPlayer();

            Assert.That(World.DealDamage(-1, World.PlayerId, 30f), Is.GreaterThan(0f), "a trap hurts anyone");
            Assert.That(World.DealDamage(friend, World.PlayerId, 30f), Is.Zero, "a friend's effect does not");
            Assert.That(World.DealDamage(-1, World.PlayerId, float.NaN), Is.Zero);
            Assert.That(World.DealDamage(-1, 999, 5f), Is.Zero);
        }

        [Test]
        public void Revive_ByAnAlly_WithPartOfTheHealth()
        {
            StartGame();
            int friend = World.AddPlayer();
            World.DealDamage(-1, friend, 1000f);
            Assert.That(World.Get(friend).IsAlive(), Is.False);

            Assert.That(World.RevivePlayer(friend, World.PlayerId, 0.3f), Is.True);

            Assert.That(World.Get(friend).IsAlive(), Is.True);
            Assert.That(World.Get(friend).Health, Is.EqualTo(World.Get(friend).MaxHealth * 0.3f).Within(0.001f));
            Assert.That(World.Log.Last(), Does.Contain("brings"));
            Assert.That(World.RevivePlayer(friend, World.PlayerId, 0.3f), Is.False, "the living need no revive");
        }
    
        [Test]
        public void PerPlayer_EachHasTheirOwnLevelBackpackQuestsAndKills()
        {
            _settings.ProgressMode = ProgressMode.PerPlayer;
            StartGame();
            int friend = World.AddPlayer();
            int goblin = World.SpawnEnemy(Goblin);

            for (int round = 0; round < 50 && World.Get(goblin) != null; round++)
                World.Attack(friend, goblin);

            Assert.That(World.ExperienceOf(friend), Is.EqualTo(40));
            Assert.That(World.TotalExperience, Is.Zero, "the host earned nothing");
            Assert.That(World.InventoryOf(friend).Count, Is.EqualTo(1));
            Assert.That(World.Inventory.Count, Is.Zero);
            Assert.That(World.KillsOf(friend), Is.EqualTo(1));
            Assert.That(World.Kills, Is.Zero);
            Assert.That(World.ActiveQuestsOf(friend).Single().Progress.ObjectiveProgress["slay"], Is.EqualTo(1));
            Assert.That(World.ActiveQuests.Single().Progress.ObjectiveProgress.TryGetValue("slay", out int mine) ? mine : 0, Is.Zero);
            Assert.That(World.CreditOf(friend), Is.EqualTo(friend));
        }

        [Test]
        public void PerPlayer_APlayerBringsTheirProgress_GrowsAlone_AndTakesItHome()
        {
            _settings.ProgressMode = ProgressMode.PerPlayer;
            StartGame();
            var brought = new PlayerProgress { Level = 3, Kills = 9 };
            brought.Items.Add(Ear);

            int friend = World.AddPlayer(progress: brought);

            Assert.That(World.LevelOf(friend), Is.EqualTo(3));
            Assert.That(World.Get(friend).MaxHealth, Is.EqualTo(140f), "their character at their level");
            Assert.That(World.Level, Is.EqualTo(1));
            Assert.That(World.Player.MaxHealth, Is.EqualTo(100f));

            World.GrantExperience(friend, World.ExperienceForNextLevelOf(friend) - World.ExperienceOf(friend));
            Assert.That(World.LevelOf(friend), Is.EqualTo(4));
            Assert.That(World.Get(friend).MaxHealth, Is.EqualTo(160f));
            Assert.That(World.Level, Is.EqualTo(1), "only they grew");
            Assert.That(World.Player.MaxHealth, Is.EqualTo(100f));

            var home = World.CaptureProgress(friend);
            Assert.That(home.Level, Is.EqualTo(4));
            Assert.That(home.Kills, Is.EqualTo(9));
            Assert.That(home.Items, Is.EqualTo(new[] { Ear }));
            Assert.That(home.ActiveQuests.Select(q => q.QuestId), Is.EqualTo(new[] { HuntQuest }));
        }

        [Test]
        public void Shared_AJoinersProgressIsIgnored_AndTheyFillTheSharedBackpack()
        {
            StartGame();
            int friend = World.AddPlayer(progress: new PlayerProgress { Level = 9 });

            Assert.That(World.LevelOf(friend), Is.EqualTo(1));
            Assert.That(World.GiveItem(friend, Ear, 2), Is.True);
            Assert.That(World.Inventory.Count, Is.EqualTo(2));
            Assert.That(World.InventoryOf(friend), Is.SameAs(World.Inventory));
            Assert.That(World.GiveItem(999, Ear, 1), Is.False, "not in the party");
        }

        [Test]
        public void LootNotToTheBackpack_IsOnlyAnnounced_AndGoesToWhoeverPicksItUp()
        {
            _settings.ProgressMode = ProgressMode.PerPlayer;
            _settings.LootToBackpack = false;
            StartGame();
            var drops = new List<LootDroppedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<LootDroppedEvent>(drops.Add);
            int friend = World.AddPlayer();

            Slay(World.SpawnEnemy(Goblin));

            Assert.That(World.Inventory.Count, Is.Zero);
            Assert.That(drops.Single().ItemId, Is.EqualTo(Ear));
            Assert.That(World.GiveItem(friend, drops[0].ItemId, drops[0].Quantity), Is.True, "the friend picked it up");
            Assert.That(World.InventoryOf(friend).Count, Is.EqualTo(1));
            Assert.That(World.Inventory.Count, Is.Zero);
        }
    }
}
