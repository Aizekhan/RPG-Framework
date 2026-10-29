using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Abilities;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.StatusEffects;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// A turn-based run on top of the framework modules: the party (levels and experience in Progression, health and
    /// mana carried from fight to fight), the party inventory and gold, and the encounters fought one after another.
    /// <para>Victory: every party member still standing gets the full experience of every beaten enemy (Progression;
    /// a level-up raises the member's stats by its per-level growth, and health and mana by as much as their
    /// maximums grew), the party gets their gold, and their loot tables are rolled (Loot) into the inventory. Then
    /// <see cref="TurnBasedSettings.RestAfterVictory"/> gives back that share of health and mana (and gets the
    /// fallen up); with no rest the fallen stay down until revived. Winning the last encounter completes the run.</para>
    /// <para>Defeat ends the run (<see cref="Restart"/> begins a new one). Fleeing keeps the party as it is and the
    /// same encounter comes next.</para>
    /// <para>Status effects (StatusEffects) and cooldowns (Abilities) last one battle.</para>
    /// </summary>
    public sealed class TurnBasedWorld : ITurnBasedWorld, IDisposable
    {
        /// <summary>
        /// Every combatant's resistances to damage elements: their own (<see cref="CombatantData.Resistances"/>);
        /// a game adds its own sources (gear, a buff) with a source id.
        /// </summary>
        public RPGFramework.Combat.DamageResistances Resistances { get; } = new RPGFramework.Combat.DamageResistances();

        private const string Category = "TurnBased";
        private const int MaxLogLines = 20;

        /// <summary>
        /// Growth profiles of the heroes in the <see cref="IStatGrowthFormula"/> are this plus the hero's data id.
        /// </summary>
        public const string GrowthProfilePrefix = "TurnBased.";

        private readonly TurnBasedSettings _settings;
        private readonly IEntityManager _entities;
        private readonly IProgressionTracker _progression;
        private readonly ILootGenerator _loot;
        private readonly IItemRegistry _items;
        private readonly IStatGrowthFormula _growth;
        private readonly Dictionary<string, AbilityEffectData> _abilityEffects = new Dictionary<string, AbilityEffectData>();
        private readonly List<Combatant> _party = new List<Combatant>();
        private readonly List<string> _log = new List<string>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private BattleRewards _collecting;
        private bool _restoring;

        public TurnBasedWorld(TurnBasedSettings settings, TurnBasedContent content, IEntityManager entities,
            IProgressionTracker progression, ILootGenerator loot, IItemRegistry items, IAbilityRegistry abilities,
            ICooldownTracker cooldowns, IEventBus bus, ILogger logger = null, IStatGrowthFormula growth = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            Cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
            Bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Logger = logger;
            _growth = growth;
            content = content ?? new TurnBasedContent();

            Random = settings.Seed != 0 ? new Random(settings.Seed) : new Random();
            Formula = settings.Formula ?? new DefaultBattleFormula();
            StatusEffects = new StatusEffectManager(entities, bus, new EffectTarget(this));
            // Battles have no distances: every target is in range
            Executor = new AbilityExecutor(abilities, cooldowns, new TargetValidator(), bus,
                distanceCheck: (caster, target) => 0f,
                relationshipCheck: (caster, target) => Battle != null && Battle.AreEnemies(caster, target),
                resourceMaximum: ResourceMaximum);

            foreach (var effect in content.StatusEffects.Where(e => e != null))
            {
                if (!StatusEffects.RegisterEffect(effect))
                    Logger?.LogWarning($"Status effect '{effect.Id}' is in the setup twice; the first one is used", Category);
            }
            foreach (var effect in content.AbilityEffects.Where(e => e != null && !string.IsNullOrEmpty(e.AbilityId)))
            {
                if (_abilityEffects.ContainsKey(effect.AbilityId))
                    Logger?.LogWarning($"Ability '{effect.AbilityId}' has two effects; the first one is used", Category);
                else
                    _abilityEffects[effect.AbilityId] = effect;
            }
        }

        public TurnBasedSettings Settings => _settings;
        public bool IsStarted { get; private set; }
        public IReadOnlyList<Combatant> Party => _party;
        public IInventoryContainer<IItem> Inventory { get; private set; }
        public int Gold { get; private set; }
        public int EncounterIndex { get; private set; }

        public EncounterData NextEncounter =>
            EncounterIndex >= 0 && EncounterIndex < _settings.Encounters.Count ? _settings.Encounters[EncounterIndex] : null;

        public Battle Battle { get; private set; }
        public bool IsInBattle => Battle != null && !Battle.IsOver;
        public bool IsRunOver { get; private set; }
        public bool IsRunComplete => IsStarted && EncounterIndex >= _settings.Encounters.Count;
        public IReadOnlyList<string> Log => _log;

        /// <summary>
        /// Status effects on combatants (their definitions, what is on whom). Durations count the combatant's turns.
        /// </summary>
        public StatusEffectManager StatusEffects { get; }

        public IAbilityRegistry Abilities { get; }

        internal ICooldownTracker Cooldowns { get; }
        internal IAbilityExecutor Executor { get; }
        internal IEventBus Bus { get; }
        internal ILogger Logger { get; }
        internal Random Random { get; }
        internal IBattleFormula Formula { get; }
        internal IBattleAi DefaultAi { get; } = new SimpleBattleAi();

        public Combatant GetMember(int memberId) => _party.FirstOrDefault(m => m.Id == memberId);

        public int LevelOf(int memberId) => _progression.GetLevel(memberId);
        public long ExperienceOf(int memberId) => _progression.GetTotalXP(memberId);
        public long ExperienceForNextLevel(int memberId) => _progression.GetXPRequiredForLevel(LevelOf(memberId) + 1);

        /// <summary>
        /// What an ability does in battle; null when it has no effect data.
        /// </summary>
        public AbilityEffectData EffectOf(string abilityId) =>
            abilityId != null && _abilityEffects.TryGetValue(abilityId, out var effect) ? effect : null;

        /// <summary>
        /// Subscribes the rules, creates the party at level 1 with full health and gives the starting items and gold.
        /// </summary>
        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("The run is already started");
            if (_settings.Party.Count(p => p != null) == 0)
                throw new InvalidOperationException("TurnBasedSettings.Party is empty");

            Bus.Subscribe<LevelUpEvent>(OnLevelUp).AddTo(_subscriptions);

            int slot = 0;
            foreach (var data in _settings.Party.Where(p => p != null).Take(TurnBasedSettings.MaxPartySize))
            {
                int id = _entities.CreateEntity();
                _progression.SetLevel(id, 1);
                RegisterGrowth(id, data);
                _party.Add(new Combatant(id, data, BattleSide.Party, slot++, data.DisplayName, 1));
                Resistances.SetBase(id, data.Resistances);
            }
            Inventory = new InventoryContainer<IItem>(_settings.InventoryCapacity, Bus, -1, "Party");
            IsStarted = true;
            StockUp();
            Write($"The party sets out: {string.Join(", ", _party.Select(m => m.Name))}");
        }

        public string WhyCannotStartBattle()
        {
            if (!IsStarted)
                return "The run has not started.";
            if (IsInBattle)
                return "A battle is already being fought.";
            if (IsRunOver)
                return "The party was defeated; restart the run.";
            var encounter = NextEncounter;
            if (encounter == null)
                return "Every encounter has been won.";
            if (_party.All(m => !m.IsAlive))
                return "Nobody in the party can fight.";
            if (encounter.EnemyIds == null || encounter.EnemyIds.Count == 0)
                return $"{encounter.DisplayName} has no enemies.";
            var missing = encounter.EnemyIds.FirstOrDefault(id => _settings.FindEnemy(id) == null);
            return missing != null ? $"{encounter.DisplayName} lists '{missing}', which is not an enemy in the setup." : null;
        }

        /// <exception cref="InvalidOperationException">With <see cref="WhyCannotStartBattle"/>'s reason.</exception>
        public Battle StartBattle()
        {
            string why = WhyCannotStartBattle();
            if (why != null)
                throw new InvalidOperationException(why);

            var encounter = NextEncounter;
            var enemies = new List<Combatant>();
            var seen = new Dictionary<string, int>();
            for (int slot = 0; slot < encounter.EnemyIds.Count; slot++)
            {
                string enemyId = encounter.EnemyIds[slot];
                var data = _settings.FindEnemy(enemyId);
                string name = data.DisplayName;
                if (encounter.EnemyIds.Count(id => id == enemyId) > 1)
                {
                    seen.TryGetValue(enemyId, out int count);
                    seen[enemyId] = count + 1;
                    name = $"{data.DisplayName} {(char)('A' + count % 26)}";
                }
                var enemy = new Combatant(_entities.CreateEntity(), data, BattleSide.Enemies, slot, name, encounter.EnemyLevel);
                Resistances.SetBase(enemy.Id, data.Resistances);
                enemies.Add(enemy);
            }

            foreach (var member in _party)
            {
                member.IsDefending = false;
                member.TurnsTaken = 0;
            }
            Battle = new Battle(this, encounter, EncounterIndex, _party.ToList(), enemies);
            Battle.Start();
            return Battle;
        }

        public void Restart()
        {
            if (!IsStarted)
                throw new InvalidOperationException("Start the run first");
            EndBattleQuietly();
            foreach (var member in _party)
            {
                _progression.SetLevel(member.Id, 1);
                member.ClearEffects();
                member.SetLevel(1);
                member.RestoreFully();
            }
            Inventory.Clear();
            Gold = 0;
            EncounterIndex = 0;
            IsRunOver = false;
            StockUp();
            Write("A new run begins");
        }

        /// <summary>
        /// Puts items into the party inventory.
        /// </summary>
        /// <returns>False when the item is unknown or the inventory filled up.</returns>
        public bool GiveItem(string itemId, int quantity = 1)
        {
            ThrowIfNotStarted();
            var item = _items.GetItem(itemId);
            if (item == null)
            {
                Logger?.LogError($"Unknown item '{itemId}'", Category);
                return false;
            }
            for (int i = 0; i < quantity; i++)
            {
                if (!Inventory.Add(item))
                {
                    Write("The inventory is full");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// How many of an item the party carries.
        /// </summary>
        public int CountOf(string itemId) => Inventory?.GetAllItems().Count(i => i != null && i.ItemId == itemId) ?? 0;

        // ---- Saving ----

        /// <summary>
        /// The party's levels, experience, health and mana, the inventory, gold and the encounter reached, for saving.
        /// A battle in progress is not part of it.
        /// </summary>
        public PartyProgress CaptureProgress()
        {
            ThrowIfNotStarted();
            var progress = new PartyProgress { EncounterIndex = EncounterIndex, Gold = Gold, RunOver = IsRunOver };
            progress.Items.AddRange(Inventory.GetAllItems().Where(i => i != null).Select(i => i.ItemId));
            foreach (var member in _party)
            {
                progress.Members.Add(new MemberRecord
                {
                    CharacterId = member.CharacterId, Level = member.Level, TotalExperience = ExperienceOf(member.Id),
                    Health = member.Health, Mana = member.Mana
                });
            }
            return progress;
        }

        /// <summary>
        /// Puts saved progress back. Members are matched by character id; saved items and members that no longer
        /// exist are skipped with a warning. Not while a battle is being fought.
        /// </summary>
        public void RestoreProgress(PartyProgress progress)
        {
            ThrowIfNotStarted();
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));
            if (IsInBattle)
                throw new InvalidOperationException("Finish the battle before loading");

            _restoring = true;
            try
            {
                foreach (var record in progress.Members ?? new List<MemberRecord>())
                {
                    var member = _party.FirstOrDefault(m => m.CharacterId == record.CharacterId);
                    if (member == null)
                    {
                        Logger?.LogWarning($"Saved party member '{record.CharacterId}' is no longer in the party; skipped", Category);
                        continue;
                    }
                    _progression.SetLevel(member.Id, Math.Max(1, record.Level));
                    long missing = record.TotalExperience - _progression.GetTotalXP(member.Id);
                    if (missing > 0)
                        _progression.AddExperience(member.Id, missing);
                    member.ClearEffects();
                    member.SetLevel(_progression.GetLevel(member.Id));
                    member.SetVitals(record.Health, record.Mana);
                }

                Inventory.Clear();
                foreach (var itemId in progress.Items ?? new List<string>())
                {
                    var item = _items.GetItem(itemId);
                    if (item == null)
                        Logger?.LogWarning($"Saved item '{itemId}' is no longer in the game; skipped", Category);
                    else if (!Inventory.Add(item))
                        break;
                }

                Gold = Math.Max(0, progress.Gold);
                EncounterIndex = Math.Max(0, Math.Min(progress.EncounterIndex, _settings.Encounters.Count));
                IsRunOver = progress.RunOver;
                Battle = null;
            }
            finally
            {
                _restoring = false;
            }
            Write($"Welcome back: encounter {Math.Min(EncounterIndex + 1, _settings.Encounters.Count)} of {_settings.Encounters.Count}");
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            EndBattleQuietly();
            foreach (var member in _party)
            {
                _growth?.AssignProfile(member.Id, null);
                if (_entities.Exists(member.Id))
                    _entities.DestroyEntity(member.Id);
            }
        }

        // ---- Called by the battle ----

        internal void OnBattleEnded(Battle battle)
        {
            var rewards = BattleRewards.None;
            if (battle.Outcome == BattleOutcome.Victory)
            {
                rewards = new BattleRewards();
                _collecting = rewards;
                try
                {
                    GrantRewards(battle, rewards);
                }
                finally
                {
                    _collecting = null;
                }
                EncounterIndex++;
                Rest();
            }
            else if (battle.Outcome == BattleOutcome.Defeat)
            {
                IsRunOver = true;
                Write("The run is over");
            }

            RemoveEnemies(battle);
            Bus.Publish(new BattleEndedEvent(battle.Encounter.Id, battle.Outcome, rewards));

            if (battle.Outcome == BattleOutcome.Victory && IsRunComplete)
            {
                Write("Every encounter is won!");
                Bus.Publish(new RunCompletedEvent(EncounterIndex, Gold));
            }
        }

        internal string ItemName(string itemId) => _items.GetItem(itemId)?.Name ?? itemId;

        internal void Write(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveAt(0);
            Logger?.LogInfo(line, Category);
        }

        // ---- Rules ----

        private void GrantRewards(Battle battle, BattleRewards rewards)
        {
            var beaten = battle.Enemies;
            rewards.Experience = beaten.Sum(e => Math.Max(0L, e.Data.ExperienceReward));
            rewards.Gold = beaten.Sum(e => Math.Max(0, e.Data.Gold));
            Gold += rewards.Gold;

            int level = Math.Max(1, (int)Math.Round(_party.Average(m => (double)m.Level)));
            foreach (var enemy in beaten.Where(e => !string.IsNullOrEmpty(e.Data.LootTableId)))
                RollLoot(enemy, level, rewards);

            Write($"Victory: {rewards.Experience} XP, {rewards.Gold} gold" +
                  (rewards.Items.Count > 0 ? ", " + string.Join(", ", rewards.Items.Select(ItemName)) : ""));

            if (rewards.Experience <= 0)
                return;
            foreach (var member in _party.Where(m => m.IsAlive))
                _progression.AddExperience(member.Id, rewards.Experience);
        }

        private void RollLoot(Combatant enemy, int level, BattleRewards rewards)
        {
            var loot = _loot.GenerateLootById(enemy.Data.LootTableId, level, seed: Random.Next());
            if (loot.Drops == null)
            {
                Logger?.LogWarning($"Loot table '{enemy.Data.LootTableId}' of {enemy.Name} is not registered", Category);
                return;
            }
            Bus.Publish(new LootGeneratedEvent
            {
                TableId = loot.TableId, TriggerEntityId = _party[0].Id, SourceEntityId = enemy.Id, ItemCount = loot.TotalCount
            });
            foreach (var drop in loot.Drops)
            {
                int given = 0;
                for (; given < drop.Quantity; given++)
                {
                    if (!GiveItem(drop.ItemId))
                        break;
                    rewards.AddItem(drop.ItemId);
                }
                if (given > 0)
                {
                    Bus.Publish(new LootDroppedEvent
                    {
                        ItemId = drop.ItemId, Quantity = given, ItemEntityId = -1, SourceEntityId = enemy.Id
                    });
                }
            }
        }

        private void Rest()
        {
            float share = Math.Max(0f, Math.Min(1f, _settings.RestAfterVictory));
            if (share <= 0f)
                return;
            foreach (var member in _party)
            {
                int health = Math.Max(1, Combatant.Round(member.MaxHealth * share));
                if (member.IsAlive)
                    member.Heal(health);
                else
                    member.Revive(health);
                member.RestoreMana(Combatant.Round(member.MaxMana * share));
            }
            Write("The party rests");
        }

        private void OnLevelUp(LevelUpEvent levelUp)
        {
            var member = GetMember(levelUp.EntityId);
            if (member == null || _restoring)
                return;
            member.SetLevel(levelUp.NewLevel);
            _collecting?.AddLevelUp(member.Id);
            Write($"{member.Name} reaches level {levelUp.NewLevel}!");
            Bus.Publish(new MemberLevelUpEvent(member.Id, member.CharacterId, levelUp.OldLevel, levelUp.NewLevel));
        }

        private void StockUp()
        {
            Gold = Math.Max(0, _settings.StartingGold);
            foreach (var itemId in _settings.StartingItems)
                GiveItem(itemId);
        }

        // A battle that is dropped (restart, shutdown): no outcome, no rewards, no events of its own
        private void EndBattleQuietly()
        {
            if (Battle == null)
                return;
            // A finished battle already cleaned up after itself (and its enemies' entities are gone)
            foreach (var combatant in Battle.IsOver ? Enumerable.Empty<Combatant>() : Battle.Combatants)
            {
                StatusEffects.RemoveAllEffects(combatant.Id);
                combatant.ClearEffects();
                Cooldowns.ClearAllCooldowns(combatant.Id);
                combatant.IsDefending = false;
                combatant.TurnsTaken = 0;
            }
            RemoveEnemies(Battle);
            Battle = null;
        }

        // Each hero grows by its own data: a growth profile per hero kind, so Progression publishes the stat gains
        private void RegisterGrowth(int memberId, CombatantData data)
        {
            if (_growth == null)
                return;
            string profileId = GrowthProfilePrefix + data.Id;
            foreach (CombatStat stat in Enum.GetValues(typeof(CombatStat)))
                _growth.RegisterStatGrowth(profileId, data.Growth(stat));
            _growth.AssignProfile(memberId, profileId);
        }

        private void RemoveEnemies(Battle battle)
        {
            foreach (var enemy in battle.Enemies)
            {
                // Entity ids come back for new entities; a new enemy must not inherit this one's ability state
                Executor.UnregisterEntity(enemy.Id);
                Resistances.Forget(enemy.Id);
                if (_entities.Exists(enemy.Id))
                    _entities.DestroyEntity(enemy.Id);
            }
        }

        private Combatant Find(int entityId)
        {
            var inBattle = Battle?.Get(entityId);
            return inBattle ?? GetMember(entityId);
        }

        private float ResourceMaximum(int entityId, string resourceType)
        {
            var combatant = Find(entityId);
            if (combatant == null)
                return 0f;
            switch (Resources.Parse(resourceType))
            {
                case Resource.Health: return combatant.MaxHealth;
                case Resource.Mana: return combatant.MaxMana;
                default: return 0f;
            }
        }

        private void ThrowIfNotStarted()
        {
            if (!IsStarted)
                throw new InvalidOperationException("Start the run first");
        }

        /// <summary>
        /// Where the StatusEffects module puts what effects do: stat modifiers on the combatant's stats (by
        /// <see cref="CombatStat"/> name), Health/Mana resource changes, and controls (Stun, Silence).
        /// </summary>
        private sealed class EffectTarget : IStatusEffectTarget
        {
            private readonly TurnBasedWorld _world;

            public EffectTarget(TurnBasedWorld world) => _world = world;

            public void ApplyStatModifier(int entityId, string modifierId, string statId, ModifierOperation operation, float value)
            {
                var combatant = _world.Find(entityId);
                if (combatant == null)
                    return;
                if (CombatStats.TryParse(statId, out var stat))
                    combatant.SetModifier(modifierId, stat, operation, value);
                else
                    _world.Logger?.LogWarning($"A status effect changes '{statId}', which is not a combatant stat; ignored", Category);
            }

            public void RemoveStatModifier(int entityId, string modifierId) => _world.Find(entityId)?.RemoveModifier(modifierId);

            public bool TryGetResource(int entityId, string resourceId, out float value)
            {
                value = 0f;
                var combatant = _world.Find(entityId);
                if (combatant == null)
                    return false;
                switch (Resources.Parse(resourceId))
                {
                    case Resource.Health: value = combatant.Health; return true;
                    case Resource.Mana: value = combatant.Mana; return true;
                }
                if (!CombatStats.TryParse(resourceId, out var stat))
                    return false;
                value = combatant.Stat(stat);
                return true;
            }

            public void ChangeResource(int entityId, string resourceId, float amount)
            {
                var combatant = _world.Find(entityId);
                if (combatant == null || !combatant.IsAlive)
                    return;
                int change = Combatant.Round(Math.Abs(amount));
                switch (Resources.Parse(resourceId))
                {
                    case Resource.Health:
                        if (amount < 0f)
                        {
                            combatant.TakeDamage(change);
                            if (!combatant.IsAlive && _world.IsInBattle)
                                _world.Battle.Defeated(combatant, -1);
                        }
                        else
                        {
                            combatant.Heal(change);
                        }
                        break;
                    case Resource.Mana:
                        if (amount < 0f)
                            combatant.SpendMana(change);
                        else
                            combatant.RestoreMana(change);
                        break;
                }
            }

            public void SetMovementMultiplier(int entityId, float multiplier)
            {
                // Nobody moves in a turn-based battle
            }

            public void SetControl(int entityId, string controlId, bool active) => _world.Find(entityId)?.SetControl(controlId, active);
        }
    }
}
