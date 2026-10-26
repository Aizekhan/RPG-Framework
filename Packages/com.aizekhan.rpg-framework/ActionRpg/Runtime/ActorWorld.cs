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

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The rules of an action RPG on top of the framework modules. Everything that reacts to something is a bus
    /// subscription: an enemy dies → experience and loot for the killer (the Combat→Quests bridge advances Kill
    /// objectives); an objective completes → the quest is turned in; a quest completes → its rewards; a level-up →
    /// stat growth and a full heal. Every character entering or leaving is announced for the presentation.
    /// </summary>
    public sealed class ActorWorld : IDisposable
    {
        private const string Category = "ActionRpg";
        private const int MaxLogLines = 12;

        private readonly ActionRpgSettings _settings;
        private readonly IEntityManager _entities;
        private readonly ILifeStateManager _lifeStates;
        private readonly IDamageCalculator _damage;
        private readonly IProgressionTracker _progression;
        private readonly IQuestManager _quests;
        private readonly IQuestRegistry _questRegistry;
        private readonly ILootGenerator _loot;
        private readonly IItemRegistry _items;
        private readonly IEventBus _bus;
        private readonly ILogger _logger;
        private readonly Random _random;
        private readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();
        private readonly List<string> _log = new List<string>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public ActorWorld(ActionRpgSettings settings, IEntityManager entities, ILifeStateManager lifeStates,
            IDamageCalculator damage, IProgressionTracker progression, IQuestManager quests, IQuestRegistry questRegistry,
            ILootGenerator loot, IItemRegistry items, IEventBus bus, ILogger logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _lifeStates = lifeStates ?? throw new ArgumentNullException(nameof(lifeStates));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _quests = quests ?? throw new ArgumentNullException(nameof(quests));
            _questRegistry = questRegistry ?? throw new ArgumentNullException(nameof(questRegistry));
            _loot = loot ?? throw new ArgumentNullException(nameof(loot));
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _logger = logger;
            _random = settings.Seed != 0 ? new Random(settings.Seed) : new Random();
        }

        public ActionRpgSettings Settings => _settings;
        public bool IsStarted => PlayerId >= 0;
        public int PlayerId { get; private set; } = -1;
        public Actor Player => PlayerId >= 0 && _actors.TryGetValue(PlayerId, out var player) ? player : null;
        public IInventoryContainer<IItem> Inventory { get; private set; }
        public int Kills { get; private set; }
        public int Level => IsStarted ? _progression.GetLevel(PlayerId) : 0;
        public long TotalExperience => IsStarted ? _progression.GetTotalXP(PlayerId) : 0;

        /// <summary>
        /// Total experience at which the player reaches the next level.
        /// </summary>
        public long ExperienceForNextLevel => IsStarted ? _progression.GetXPRequiredForLevel(Level + 1) : 0;

        /// <summary>
        /// Number of the last wave started (0 before the first one).
        /// </summary>
        public int CurrentWave { get; private set; }

        /// <summary>
        /// Recent happenings, newest last (for a HUD log).
        /// </summary>
        public IReadOnlyList<string> Log => _log;

        /// <summary>
        /// Everyone in the world, the player included.
        /// </summary>
        public IEnumerable<Actor> Actors => _actors.Values;

        public IEnumerable<int> LivingEnemies => _actors.Values.Where(a => !a.IsPlayer && a.IsAlive()).Select(a => a.EntityId);

        public Actor Get(int entityId) => _actors.TryGetValue(entityId, out var actor) ? actor : null;

        /// <summary>
        /// Quest target id of an entity (its character id); used by the Kill-objective bridge.
        /// </summary>
        public string QuestTargetOf(int entityId) => Get(entityId)?.CharacterId;

        /// <summary>
        /// Quests the player has taken, with their progress.
        /// </summary>
        public IEnumerable<(IQuest Quest, IQuestProgress Progress)> ActiveQuests =>
            IsStarted
                ? _quests.GetActiveQuests(PlayerId).Select(p => (_questRegistry.GetQuest(p.QuestId), p)).Where(q => q.Item1 != null)
                : Enumerable.Empty<(IQuest, IQuestProgress)>();

        public IReadOnlyList<string> CompletedQuests =>
            IsStarted ? _quests.GetCompletedQuests(PlayerId) : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Subscribes the rules, creates the player and gives them the starting quests.
        /// </summary>
        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("The world is already started");
            if (_settings.Player == null)
                throw new InvalidOperationException("ActionRpgSettings.Player is not set");

            _bus.Subscribe<DeathEvent>(OnDeath).AddTo(_subscriptions);
            _bus.Subscribe<ObjectiveProgressedEvent>(OnObjectiveProgressed).AddTo(_subscriptions);
            _bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted).AddTo(_subscriptions);
            _bus.Subscribe<LevelUpEvent>(OnLevelUp).AddTo(_subscriptions);
            _bus.Subscribe<StatIncreasedEvent>(OnStatIncreased).AddTo(_subscriptions);
            _bus.Subscribe<ItemAddedEvent<IItem>>(OnItemAdded).AddTo(_subscriptions);

            PlayerId = _entities.CreateEntity();
            Inventory = new InventoryContainer<IItem>(_settings.InventoryCapacity, _bus, PlayerId, "Backpack");
            Add(new Actor(PlayerId, _settings.Player, isPlayer: true));
            _progression.SetLevel(PlayerId, 1);

            foreach (var questId in _settings.StartingQuests)
            {
                if (_quests.AcceptQuest(PlayerId, questId))
                    Write($"New quest: {_questRegistry.GetQuest(questId)?.Title ?? questId}");
                else
                    _logger?.LogWarning($"Starting quest '{questId}' could not be accepted", Category);
            }
            Write($"{_settings.Player.DisplayName} enters the world");
        }

        /// <summary>
        /// Spawns one enemy of this character id.
        /// </summary>
        public int SpawnEnemy(string characterId)
        {
            ThrowIfNotStarted();
            var character = _settings.Enemies.FirstOrDefault(e => e != null && e.Id == characterId)
                            ?? throw new ArgumentException($"No enemy with id '{characterId}' in the settings", nameof(characterId));
            int id = _entities.CreateEntity();
            Add(new Actor(id, character, isPlayer: false));
            return id;
        }

        /// <summary>
        /// Starts the next wave with this many enemies, picked at random from the settings.
        /// </summary>
        public void StartWave(int enemies)
        {
            ThrowIfNotStarted();
            if (enemies <= 0)
                throw new ArgumentOutOfRangeException(nameof(enemies));
            var pool = _settings.Enemies.Where(e => e != null).ToList();
            if (pool.Count == 0)
                throw new InvalidOperationException("No enemies in the settings");

            CurrentWave++;
            Write($"Wave {CurrentWave}: {enemies} enemies");
            for (int i = 0; i < enemies; i++)
                SpawnEnemy(pool[_random.Next(pool.Count)].Id);
            _bus.Publish(new WaveStartedEvent(CurrentWave, enemies));
        }

        /// <returns>Damage dealt (0 when either side is missing or dead).</returns>
        public float Attack(int attackerId, int targetId)
        {
            var attacker = Get(attackerId);
            var target = Get(targetId);
            if (attacker == null || target == null || !attacker.IsAlive() || !target.IsAlive())
                return 0f;

            float dealt = target.ApplyDamage(_damage.CalculateDamage(attacker.Attack, target.Defense));
            _bus.Publish(new ActorDamagedEvent(targetId, attackerId, dealt, target.Health));
            if (target.Health <= 0f)
                _lifeStates.Kill(targetId, attackerId, "slain");
            return dealt;
        }

        public bool RevivePlayer() => IsStarted && _lifeStates.Resurrect(PlayerId, -1, "respawn");

        public void Dispose()
        {
            _subscriptions.Dispose();
            foreach (int id in new List<int>(_actors.Keys))
                Remove(id);
        }

        private void Add(Actor actor)
        {
            _actors.Add(actor.EntityId, actor);
            _lifeStates.RegisterEntity(actor.EntityId, actor);
            _bus.Publish(new ActorSpawnedEvent(actor.EntityId, actor.CharacterId, actor.IsPlayer));
        }

        private void Remove(int entityId)
        {
            if (!_actors.TryGetValue(entityId, out var actor))
                return;
            _actors.Remove(entityId);
            _lifeStates.UnregisterEntity(entityId);
            if (_entities.Exists(entityId))
                _entities.DestroyEntity(entityId);
            _bus.Publish(new ActorDespawnedEvent(entityId, actor.CharacterId, actor.IsPlayer));
        }

        private void OnDeath(DeathEvent death)
        {
            var victim = Get(death.EntityId);
            if (victim == null)
                return;

            if (victim.IsPlayer)
            {
                Write($"{victim.DisplayName} falls");
                return;
            }

            Kills++;
            _bus.Publish(new ActorKilledEvent(victim.EntityId, victim.CharacterId, death.KillerEntityId,
                death.KillerEntityId == PlayerId));
            Remove(death.EntityId);
            if (death.KillerEntityId != PlayerId)
                return;

            Write($"{victim.DisplayName} slain");
            if (victim.Character.ExperienceReward > 0)
                _progression.AddExperience(PlayerId, victim.Character.ExperienceReward);

            if (string.IsNullOrEmpty(victim.Character.LootTableId))
                return;
            var loot = _loot.GenerateLootById(victim.Character.LootTableId, Level, seed: _random.Next());
            if (loot.Drops == null)
                return;
            foreach (var drop in loot.Drops)
                GiveItem(drop.ItemId, drop.Quantity);
        }

        private void OnObjectiveProgressed(ObjectiveProgressedEvent progress)
        {
            if (progress.EntityId != PlayerId)
                return;
            string title = _questRegistry.GetQuest(progress.QuestId)?.Title ?? progress.QuestId;
            Write($"{title}: {progress.CurrentProgress}/{progress.RequiredProgress}");
            // CompleteQuest refuses while any objective is still open
            if (progress.IsCompleted && _settings.CompleteQuestsAutomatically)
                _quests.CompleteQuest(progress.EntityId, progress.QuestId);
        }

        private void OnQuestCompleted(QuestCompletedEvent completed)
        {
            if (completed.EntityId != PlayerId)
                return;
            Write($"Quest complete: {_questRegistry.GetQuest(completed.QuestId)?.Title ?? completed.QuestId}");
            if (completed.Rewards == null)
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
                        _logger?.LogWarning($"Reward type {reward.Type} is not handled by ActorWorld; subscribe to " +
                                            "QuestCompletedEvent in a game module to grant it", Category);
                        break;
                }
            }
        }

        private void OnLevelUp(LevelUpEvent levelUp)
        {
            if (levelUp.EntityId != PlayerId)
                return;
            // Stat growth for this level-up was already applied: StatGrowthPublisher subscribed before the world did
            Player.RestoreFullHealth();
            Write($"Level up! Level {levelUp.NewLevel}");
        }

        private void OnStatIncreased(StatIncreasedEvent increased)
        {
            var actor = Get(increased.EntityId);
            if (actor == null || !Enum.TryParse(increased.StatId, out ActorStat stat))
                return;
            actor.SetBaseStat(stat, increased.NewValue);
        }

        private void OnItemAdded(ItemAddedEvent<IItem> added)
        {
            if (added.EntityId == PlayerId)
                Write($"+{added.Quantity} {added.Item.Name}");
        }

        /// <summary>
        /// Puts items into the player's backpack.
        /// </summary>
        /// <returns>False when the item is unknown or the backpack filled up.</returns>
        public bool GiveItem(string itemId, int quantity = 1)
        {
            ThrowIfNotStarted();
            var item = _items.GetItem(itemId);
            if (item == null)
            {
                _logger?.LogError($"Unknown item '{itemId}'", Category);
                return false;
            }

            for (int i = 0; i < quantity; i++)
            {
                if (!Inventory.Add(item))
                {
                    Write("Backpack is full");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Adds a line to the log the HUD shows (for game rules: "Bounty paid", "Boss enraged").
        /// </summary>
        public void Announce(string line) => Write(line);

        private void Write(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveAt(0);
            _logger?.LogInfo(line, Category);
        }

        private void ThrowIfNotStarted()
        {
            if (!IsStarted)
                throw new InvalidOperationException("Start the world first");
        }
    }
}
