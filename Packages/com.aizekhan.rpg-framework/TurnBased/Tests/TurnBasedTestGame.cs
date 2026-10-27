using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.StatusEffects;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// A small JRPG for the tests, run through the real modules (GameHost + <see cref="TurnBasedComposition"/>):
    /// a knight, a mage and a cleric against goblins, a goblin shaman and an ogre. Change <see cref="Settings"/> and
    /// <see cref="Content"/> before <see cref="Start"/>. Damage maths has no misses and no spread unless a test
    /// sets another formula, so the numbers are exact:
    /// knight hits a goblin for 18, the mage for 4, the cleric for 6; a goblin hits the knight for 7, the mage for 10,
    /// the cleric for 9; Fire deals 26 to a goblin.
    /// </summary>
    internal sealed class TurnBasedTestGame : IDisposable
    {
        public const string Knight = "knight";
        public const string Mage = "mage";
        public const string Cleric = "cleric";
        public const string Goblin = "goblin";
        public const string Shaman = "shaman";
        public const string Ogre = "ogre";

        public const string PowerStrike = "power_strike";
        public const string Fire = "fire";
        public const string Blizzard = "blizzard";
        public const string PoisonDart = "poison_dart";
        public const string StunBolt = "stun_bolt";
        public const string Heal = "heal";
        public const string Raise = "raise";
        public const string Bless = "bless";
        public const string Mend = "mend";
        public const string Spark = "spark";

        public const string Poison = "poison";
        public const string Stun = "stun";
        public const string AttackUp = "attack_up";

        public const string Potion = "potion";
        public const string Ether = "ether";
        public const string Phoenix = "phoenix_down";
        public const string Antidote = "antidote";
        public const string Bomb = "bomb";
        public const string PoisonVial = "poison_vial";
        public const string Ear = "goblin_ear";

        private Logger _logger;

        public TurnBasedTestGame(int seed = 7)
        {
            Settings = new TurnBasedSettings
            {
                Seed = seed,
                FleeChance = 0.5f,
                Formula = new DefaultBattleFormula { MissChance = 0f, Variance = 0f }
            };
            Settings.Party.Add(new CombatantData
            {
                Id = Knight, DisplayName = "Knight", MaxHealth = 120, MaxMana = 10, Attack = 20, Defense = 10, Magic = 2, Speed = 8,
                HealthPerLevel = 20, ManaPerLevel = 2, AttackPerLevel = 3, DefensePerLevel = 2, MagicPerLevel = 1, SpeedPerLevel = 1,
                AbilityIds = { PowerStrike }
            });
            Settings.Party.Add(new CombatantData
            {
                Id = Mage, DisplayName = "Mage", MaxHealth = 70, MaxMana = 40, Attack = 6, Defense = 4, Magic = 18, Speed = 12,
                HealthPerLevel = 10, ManaPerLevel = 5, AttackPerLevel = 1, DefensePerLevel = 1, MagicPerLevel = 3, SpeedPerLevel = 1,
                AbilityIds = { Fire, Blizzard, PoisonDart, StunBolt }
            });
            Settings.Party.Add(new CombatantData
            {
                Id = Cleric, DisplayName = "Cleric", MaxHealth = 90, MaxMana = 30, Attack = 8, Defense = 6, Magic = 14, Speed = 10,
                HealthPerLevel = 15, ManaPerLevel = 4, AttackPerLevel = 1, DefensePerLevel = 1, MagicPerLevel = 2, SpeedPerLevel = 1,
                AbilityIds = { Heal, Raise, Bless }
            });

            Settings.Enemies.Add(new CombatantData
            {
                Id = Goblin, DisplayName = "Goblin", MaxHealth = 40, MaxMana = 0, Attack = 12, Defense = 4, Magic = 0, Speed = 9,
                ExperienceReward = 50, Gold = 5, LootTableId = Goblin
            });
            Settings.Enemies.Add(new CombatantData
            {
                Id = Shaman, DisplayName = "Goblin Shaman", MaxHealth = 35, MaxMana = 30, Attack = 6, Defense = 3, Magic = 12, Speed = 11,
                ExperienceReward = 60, Gold = 10, AbilityIds = { Mend, Spark }
            });
            Settings.Enemies.Add(new CombatantData
            {
                Id = Ogre, DisplayName = "Ogre", MaxHealth = 150, MaxMana = 0, Attack = 25, Defense = 8, Magic = 0, Speed = 5,
                ExperienceReward = 200, Gold = 50
            });

            Settings.Encounters.Add(new EncounterData { Id = "pack", DisplayName = "Goblin Pack", EnemyIds = { Goblin, Goblin } });
            Settings.Encounters.Add(new EncounterData { Id = "camp", DisplayName = "Shaman Camp", EnemyIds = { Goblin, Shaman } });
            Settings.Encounters.Add(new EncounterData { Id = "lair", DisplayName = "Ogre Lair", EnemyIds = { Ogre }, CanFlee = false });

            Settings.StartingItems.AddRange(new[] { Potion, Potion, Ether, Phoenix, Antidote, Bomb, PoisonVial });
            Settings.StartingGold = 20;

            Content = new TurnBasedContent();
            Content.Items.Add(new Consumable(Potion, "Potion", "", ItemEffects.RestoreHealth, 50f));
            Content.Items.Add(new Consumable(Ether, "Ether", "", ItemEffects.RestoreMana, 20f));
            Content.Items.Add(new Consumable(Phoenix, "Phoenix Down", "", ItemEffects.Revive, 0.5f));
            Content.Items.Add(new Consumable(Antidote, "Antidote", "", ItemEffects.Cure, 0f));
            Content.Items.Add(new Consumable(Bomb, "Bomb", "", ItemEffects.Damage, 30f));
            Content.Items.Add(new Consumable(PoisonVial, "Poison Vial", "", Poison, 0f));
            Content.Items.Add(new Item(Ear, "Goblin Ear", "", ItemType.Crafting, maxStackSize: 99));
            Content.LootTables.Add(LootTableBuilder.Create(Goblin, "Goblin").AddGuaranteedDrop(Ear).WithDrops(0, 0).Build());

            Content.Abilities.Add(Ability(PowerStrike, TargetType.Enemy, mana: 4, cooldown: 2));
            Content.Abilities.Add(Ability(Fire, TargetType.Enemy, mana: 6));
            Content.Abilities.Add(Ability(Blizzard, TargetType.Area, mana: 12));
            Content.Abilities.Add(Ability(PoisonDart, TargetType.Enemy, mana: 2));
            Content.Abilities.Add(Ability(StunBolt, TargetType.Enemy, mana: 5));
            Content.Abilities.Add(Ability(Heal, TargetType.Ally, mana: 5));
            Content.Abilities.Add(Ability(Raise, TargetType.Ally, mana: 15));
            Content.Abilities.Add(Ability(Bless, TargetType.None, mana: 6));
            Content.Abilities.Add(Ability(Mend, TargetType.Ally, mana: 5));
            Content.Abilities.Add(Ability(Spark, TargetType.Enemy, mana: 3));

            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = PowerStrike, Scope = TargetScope.SingleEnemy, Kind = AbilityKind.Damage, Power = 1.5f, ScalesWith = CombatStat.Attack });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Fire, Scope = TargetScope.SingleEnemy, Kind = AbilityKind.Damage, Power = 1.5f });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Blizzard, Scope = TargetScope.AllEnemies, Kind = AbilityKind.Damage, Power = 1f });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = PoisonDart, Scope = TargetScope.SingleEnemy, Kind = AbilityKind.Support, StatusEffectIds = { Poison } });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = StunBolt, Scope = TargetScope.SingleEnemy, Kind = AbilityKind.Support, StatusEffectIds = { Stun } });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Heal, Scope = TargetScope.SingleAlly, Kind = AbilityKind.Heal, Power = 1.5f });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Raise, Scope = TargetScope.SingleAlly, Kind = AbilityKind.Heal, Power = 1f, Revives = true });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Bless, Scope = TargetScope.AllAllies, Kind = AbilityKind.Support, StatusEffectIds = { AttackUp } });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Mend, Scope = TargetScope.SingleAlly, Kind = AbilityKind.Heal, Power = 1.5f });
            Content.AbilityEffects.Add(new AbilityEffectData { AbilityId = Spark, Scope = TargetScope.SingleEnemy, Kind = AbilityKind.Damage, Power = 1f });

            // Durations count the affected combatant's own turns; ticks happen at the start of its turn
            Content.StatusEffects.Add(StatusEffectData.CreateDoT(Poison, "Poison", 3f, 1f, 5f));
            var stun = StatusEffectData.Create(Stun, "Stun", EffectType.StatusCondition);
            stun.Duration = 1f;
            stun.Modifiers.Add(EffectModifierData.Create(ModifierType.Control, Combatant.Stun, 1f));
            Content.StatusEffects.Add(stun);
            Content.StatusEffects.Add(StatusEffectData.CreateStatModifier(AttackUp, "Attack Up", EffectType.Buff, 3f,
                nameof(CombatStat.Attack), 1.5f, ModifierOperation.Multiply));
        }

        public TurnBasedSettings Settings { get; }
        public TurnBasedContent Content { get; }
        public GameHost Host { get; private set; }
        public TurnBasedModule Module { get; private set; }
        public TurnBasedWorld World => Module.World;
        public IEventBus Bus => Host.Container.Resolve<IEventBus>();

        public CombatantData KnightData => Settings.Party[0];
        public CombatantData MageData => Settings.Party[1];
        public CombatantData ClericData => Settings.Party[2];
        public CombatantData GoblinData => Settings.Enemies[0];

        public TurnBasedWorld Start()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            Host = new GameHost(_logger);
            Host.Start(builder => Module = TurnBasedComposition.Configure(builder, Settings, Content));
            return World;
        }

        public Combatant Member(string characterId) => World.Party.First(m => m.CharacterId == characterId);

        /// <summary>
        /// Enemies wait (defend) on every turn, so a test controls everything that happens.
        /// </summary>
        public void EnemiesDefend() => Settings.EnemyAi = new ScriptedAi((battle, actor) => BattleAction.Defend());

        /// <summary>
        /// Enemies attack the party member with this character id while it stands, else defend.
        /// </summary>
        public void EnemiesAttack(string characterId) => Settings.EnemyAi = new ScriptedAi((battle, actor) =>
        {
            var target = battle.Party.FirstOrDefault(p => p.CharacterId == characterId && p.IsAlive);
            return target != null ? BattleAction.Attack(target.Id) : BattleAction.Defend();
        });

        /// <summary>
        /// Every party member who is not this one defends until it is that one's turn.
        /// </summary>
        public static void DefendUntil(Battle battle, string characterId)
        {
            for (int guard = 0; guard < 100 && !battle.IsOver && battle.Current.CharacterId != characterId; guard++)
                Expect(battle.Perform(BattleAction.Defend()));
        }

        public static ActionResult Expect(ActionResult result)
        {
            if (!result.Done)
                throw new InvalidOperationException("Refused: " + result.Refusal);
            return result;
        }

        /// <summary>
        /// Plays a battle with a simple party script: the cleric heals anyone under half health, the mage casts Fire
        /// while it has the mana, everyone else attacks the weakest enemy.
        /// </summary>
        public static void PlayToTheEnd(Battle battle)
        {
            for (int guard = 0; guard < 500 && !battle.IsOver; guard++)
            {
                var actor = battle.Current;
                var foe = battle.LivingOpponents(actor).OrderBy(e => e.Health).First();
                var hurt = battle.LivingAllies(actor).Where(a => a.HealthFraction < 0.5f).OrderBy(a => a.HealthFraction).FirstOrDefault();
                BattleAction action;
                if (actor.CharacterId == Cleric && hurt != null && battle.CanPerform(BattleAction.Ability(Heal, hurt.Id)))
                    action = BattleAction.Ability(Heal, hurt.Id);
                else if (actor.CharacterId == Mage && battle.CanPerform(BattleAction.Ability(Fire, foe.Id)))
                    action = BattleAction.Ability(Fire, foe.Id);
                else
                    action = BattleAction.Attack(foe.Id);
                Expect(battle.Perform(action));
            }
        }

        public void Dispose()
        {
            Host?.Dispose();
            _logger?.Dispose();
        }

        private static IAbility Ability(string id, TargetType target, float mana, float cooldown = 0f) =>
            new Ability(new AbilityMetadata(id, char.ToUpperInvariant(id[0]) + id.Substring(1).Replace('_', ' '), AbilityType.Active, cooldown),
                new List<ResourceCost> { new ResourceCost("MP", mana) }, CastInfo.Instant, target);
    }

    /// <summary>
    /// An AI that does what the test says.
    /// </summary>
    internal sealed class ScriptedAi : IBattleAi
    {
        private readonly Func<Battle, Combatant, BattleAction> _choose;

        public ScriptedAi(Func<Battle, Combatant, BattleAction> choose) => _choose = choose;

        public BattleAction Choose(Battle battle, Combatant actor, Random random) => _choose(battle, actor);
    }
}
