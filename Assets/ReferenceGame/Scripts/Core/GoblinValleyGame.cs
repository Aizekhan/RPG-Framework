using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Combat;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.Quests;

namespace ReferenceGame
{
    /// <summary>
    /// Goblin Valley rules, engine-independent. Gameplay services come from the framework modules; every rule that
    /// reacts to something happening is an event-bus subscription:
    /// a goblin dies → XP and loot for the killer (the framework bridge advances the Kill objective);
    /// an objective completes → the quest is turned in; a quest completes → its rewards are granted;
    /// a level-up → stat growth is applied and the player is healed.
    /// </summary>
    public sealed class GoblinValleyGame : IDisposable
    {
        private const string Category = "GoblinValley";
        private const int MaxLogLines = 12;

        private readonly IEntityManager _entities;
        private readonly ILifeStateManager _lifeStates;
        private readonly IDamageCalculator _damage;
        private readonly IProgressionTracker _progression;
        private readonly IStatGrowthFormula _statGrowth;
        private readonly IQuestManager _quests;
        private readonly ILootGenerator _loot;
        private readonly IItemRegistry _items;
        private readonly IEventBus _bus;
        private readonly ILogger _logger;
        private readonly Random _random;
        private readonly Dictionary<int, Combatant> _combatants = new Dictionary<int, Combatant>();
        private readonly List<string> _log = new List<string>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public GoblinValleyGame(IEntityManager entities, ILifeStateManager lifeStates, IDamageCalculator damage,
            IProgressionTracker progression, IStatGrowthFormula statGrowth, IQuestManager quests, ILootGenerator loot,
            IItemRegistry items, IEventBus bus, ILogger logger, int? seed = null)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _lifeStates = lifeStates ?? throw new ArgumentNullException(nameof(lifeStates));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _statGrowth = statGrowth ?? throw new ArgumentNullException(nameof(statGrowth));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _logger = logger;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public bool IsStarted => PlayerId >= 0;
        public int PlayerId { get; private set; } = -1;
        public Combatant Player => PlayerId >= 0 ? _combatants[PlayerId] : null;
        public IInventoryContainer<IItem> Inventory { get; private set; }
        public int GoblinsKilled { get; private set; }
        public int Level => IsStarted ? _progression.GetLevel(PlayerId) : 0;
        public long TotalXp => IsStarted ? _progression.GetTotalXP(PlayerId) : 0;
        public IReadOnlyList<string> Log => _log;

        /// <summary>
        /// Number of the last wave started (0 before the first one).
        /// </summary>
        public int CurrentWave { get; private set; }

        /// <summary>
        /// Everyone in the valley, the player included.
        /// </summary>
        public IEnumerable<Combatant> Combatants => _combatants.Values;

        public IEnumerable<int> LivingGoblins
        {
            get
            {
                foreach (var combatant in _combatants.Values)
                {
                    if (combatant.Kind == GoblinValleyContent.GoblinKind && combatant.IsAlive())
                        yield return combatant.EntityId;
                }
            }
        }

        public bool QuestCompleted =>
            IsStarted && _quests.GetCompletedQuests(PlayerId).Contains(GoblinValleyContent.ClearTheValley);

        /// <summary>
        /// Subscribes the rules, creates the player and gives them the valley quest.
        /// </summary>
        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("The game is already started");

            _bus.Subscribe<DeathEvent>(OnDeath).AddTo(_subscriptions);
            _bus.Subscribe<ObjectiveProgressedEvent>(OnObjectiveProgressed).AddTo(_subscriptions);
            _bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted).AddTo(_subscriptions);
            _bus.Subscribe<LevelUpEvent>(OnLevelUp).AddTo(_subscriptions);
            _bus.Subscribe<StatIncreasedEvent>(OnStatIncreased).AddTo(_subscriptions);
            _bus.Subscribe<ItemAddedEvent<IItem>>(OnItemAdded).AddTo(_subscriptions);

            PlayerId = Spawn(GoblinValleyContent.PlayerKind,
                _statGrowth.CalculateStatAtLevel(nameof(CombatStat.MaxHealth), 1),
                _statGrowth.CalculateStatAtLevel(nameof(CombatStat.Attack), 1),
                _statGrowth.CalculateStatAtLevel(nameof(CombatStat.Defense), 1));
            _progression.SetLevel(PlayerId, 1);
            Inventory = new InventoryContainer<IItem>(capacity: 40, _bus, PlayerId, "Backpack");

            if (!_quests.AcceptQuest(PlayerId, GoblinValleyContent.ClearTheValley))
                throw new InvalidOperationException($"Quest '{GoblinValleyContent.ClearTheValley}' could not be accepted");
            Write("A hero enters Goblin Valley. Quest: Clear the Valley");
        }

        public int SpawnGoblin()
        {
            ThrowIfNotStarted();
            int id = Spawn(GoblinValleyContent.GoblinKind, GoblinValleyContent.GoblinHealth, GoblinValleyContent.GoblinAttack,
                GoblinValleyContent.GoblinDefense);
            Write("A goblin appears");
            return id;
        }

        /// <summary>
        /// Starts the next wave with this many goblins.
        /// </summary>
        public void StartWave(int goblins)
        {
            ThrowIfNotStarted();
            if (goblins <= 0)
                throw new ArgumentOutOfRangeException(nameof(goblins));

            CurrentWave++;
            Write($"Wave {CurrentWave}: {goblins} goblins");
            for (int i = 0; i < goblins; i++)
                Spawn(GoblinValleyContent.GoblinKind, GoblinValleyContent.GoblinHealth, GoblinValleyContent.GoblinAttack,
                    GoblinValleyContent.GoblinDefense);
            _bus.Publish(new WaveStartedEvent(CurrentWave, goblins));
        }

        public Combatant Get(int entityId) => _combatants.TryGetValue(entityId, out var combatant) ? combatant : null;

        /// <summary>
        /// What an entity counts as for quests ("goblin"), or null; used by the Kill-objective bridge.
        /// </summary>
        public string KindOf(int entityId) => Get(entityId)?.Kind;

        /// <returns>Damage dealt (0 when either side is missing or dead).</returns>
        public float Attack(int attackerId, int targetId)
        {
            var attacker = Get(attackerId);
            var target = Get(targetId);
            if (attacker == null || target == null || !attacker.IsAlive() || !target.IsAlive())
                return 0f;

            float dealt = target.ApplyDamage(_damage.CalculateDamage(attacker.Attack, target.Defense));
            if (target.Health <= 0f)
                _lifeStates.Kill(targetId, attackerId, "slain");
            return dealt;
        }

        /// <summary>
        /// The player attacks the first living goblin, which strikes back if it survives.
        /// </summary>
        /// <returns>False when there is no goblin to fight or the player is dead.</returns>
        public bool PlayerAttacksNearestGoblin()
        {
            if (!IsStarted || !Player.IsAlive())
                return false;

            foreach (int goblin in LivingGoblins)
            {
                Attack(PlayerId, goblin);
                if (Get(goblin) != null)
                    Attack(goblin, PlayerId);
                return true;
            }
            return false;
        }

        public bool RevivePlayer() => IsStarted && _lifeStates.Resurrect(PlayerId, -1, "respawn");

        public void Dispose()
        {
            _subscriptions.Dispose();
            foreach (int id in new List<int>(_combatants.Keys))
                Despawn(id);
        }

        private int Spawn(string kind, float maxHealth, float attack, float defense)
        {
            int id = _entities.CreateEntity();
            var combatant = new Combatant(id, kind, maxHealth, attack, defense);
            _combatants.Add(id, combatant);
            _lifeStates.RegisterEntity(id, combatant);
            _bus.Publish(new CombatantSpawnedEvent(id, kind));
            return id;
        }

        private void Despawn(int entityId)
        {
            if (!_combatants.TryGetValue(entityId, out var combatant))
                return;
            _combatants.Remove(entityId);
            _lifeStates.UnregisterEntity(entityId);
            if (_entities.Exists(entityId))
                _entities.DestroyEntity(entityId);
            _bus.Publish(new CombatantDespawnedEvent(entityId, combatant.Kind));
        }

        private void OnDeath(DeathEvent death)
        {
            var victim = Get(death.EntityId);
            if (victim == null)
                return;

            if (victim.Kind == GoblinValleyContent.PlayerKind)
            {
                Write("The hero falls. Press R to respawn");
                return;
            }

            if (victim.Kind != GoblinValleyContent.GoblinKind)
                return;

            GoblinsKilled++;
            Despawn(death.EntityId);
            if (death.KillerEntityId != PlayerId)
                return;

            Write("Goblin slain");
            _progression.AddExperience(PlayerId, GoblinValleyContent.GoblinKillXp);

            var loot = _loot.GenerateLootById(GoblinValleyContent.GoblinLootTable, Level, seed: _random.Next());
            if (loot.Drops == null)
                return;
            foreach (var drop in loot.Drops)
                GiveItem(drop.ItemId, drop.Quantity);
        }

        private void OnObjectiveProgressed(ObjectiveProgressedEvent progress)
        {
            Write($"{progress.QuestId}: {progress.CurrentProgress}/{progress.RequiredProgress}");
            // Turn in as soon as every objective is done (CompleteQuest refuses while any is still open)
            if (progress.IsCompleted)
                _quests.CompleteQuest(progress.EntityId, progress.QuestId);
        }

        private void OnQuestCompleted(QuestCompletedEvent completed)
        {
            Write($"Quest complete: {completed.QuestId}");
            if (completed.Rewards == null || completed.EntityId != PlayerId)
                return;

            foreach (var reward in completed.Rewards)
            {
                switch (reward.Type)
                {
                    case RewardType.Experience:
                        _progression.AddExperience(completed.EntityId, reward.Amount);
                        break;
                    case RewardType.Item:
                        GiveItem(reward.RewardId, reward.Amount);
                        break;
                    default:
                        _logger.LogWarning($"Reward type {reward.Type} is not used in Goblin Valley", Category);
                        break;
                }
            }
        }

        private void OnLevelUp(LevelUpEvent levelUp)
        {
            if (levelUp.EntityId != PlayerId)
                return;
            // Stat growth for this level-up was already applied: StatGrowthPublisher subscribed before the game did
            Player.RestoreFullHealth();
            Write($"Level up! Level {levelUp.NewLevel}");
        }

        private void OnStatIncreased(StatIncreasedEvent increased)
        {
            var combatant = Get(increased.EntityId);
            if (combatant == null || !Enum.TryParse(increased.StatId, out CombatStat stat))
                return;
            combatant.SetBaseStat(stat, increased.NewValue);
        }

        private void OnItemAdded(ItemAddedEvent<IItem> added)
        {
            if (added.EntityId == PlayerId)
                Write($"+{added.Quantity} {added.Item.Name}");
        }

        private void GiveItem(string itemId, int quantity)
        {
            var item = _items.GetItem(itemId);
            if (item == null)
            {
                _logger.LogError($"Unknown item '{itemId}'", Category);
                return;
            }

            for (int i = 0; i < quantity; i++)
            {
                if (!Inventory.Add(item))
                {
                    Write("Backpack is full");
                    return;
                }
            }
        }

        private void Write(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveAt(0);
            _logger.LogInfo(line, Category);
        }

        private void ThrowIfNotStarted()
        {
            if (!IsStarted)
                throw new InvalidOperationException("Start the game first");
        }
    }
}
