using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Combat;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Interaction;
using RPGFramework.Inventory.Runtime;
using RPGFramework.Items;
using RPGFramework.Localization;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Progression;
using RPGFramework.Quests;
using RPGFramework.Systems;
using RPGFramework.Trading;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// The rules of an action RPG on top of the framework modules. Everything that reacts to something is a bus
    /// subscription: an enemy dies → experience and loot for the killer (the Combat→Quests bridge advances Kill
    /// objectives); an objective completes → the quest is turned in; a quest completes → its rewards; a level-up →
    /// stat growth and a full heal. Every character entering or leaving is announced for the presentation.
    /// In co-op more players join the party (<see cref="AddPlayer"/>). With <see cref="ProgressMode.Shared"/> the party
    /// shares the first player's level, experience, backpack and quests, and every member's kills count for it; with
    /// <see cref="ProgressMode.PerPlayer"/> every player has their own, brought in when they join
    /// (<see cref="AddPlayer"/> with their saved progress) and taken home (<see cref="CaptureProgress(int)"/>).
    /// </summary>
    public sealed class ActorWorld : IActorWorld, IDisposable
    {
        private const string Category = "ActionRpg";
        private const int MaxLogLines = 12;

        /// <summary>
        /// Longest player name kept.
        /// </summary>
        public const int MaxNameLength = 16;

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
        private readonly List<int> _party = new List<int>();
        private readonly Dictionary<int, IInventoryContainer<IItem>> _inventories = new Dictionary<int, IInventoryContainer<IItem>>();
        private readonly Dictionary<int, int> _kills = new Dictionary<int, int>();
        private readonly List<string> _log = new List<string>();
        private readonly List<LogLine> _logLines = new List<LogLine>();
        // The hit that brought a character to 0, until its death is handled
        private readonly Dictionary<int, AttackContext> _killingHits = new Dictionary<int, AttackContext>();
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        public ActorWorld(ActionRpgSettings settings, IEntityManager entities, ILifeStateManager lifeStates,
            IDamageCalculator damage, IProgressionTracker progression, IQuestManager quests, IQuestRegistry questRegistry,
            ILootGenerator loot, IItemRegistry items, IEventBus bus, ILogger logger = null, ICurrencyWallet wallet = null)
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
            Wallet = wallet ?? new CurrencyWallet(bus);
            Interactions = new InteractionService(bus);
            Gear = new ActorGear(this, settings.GearSlots, bus);
            Skills = new ActorSkills(this, bus);
            Crafting = new ActorCrafting(this, bus);
            Dialogue = new ActorDialogue(this, entities, bus);
            Consumables = new ActorConsumables(this, bus);
            Reputation = new ActorReputation(this, entities, bus);
            _random = settings.Seed != 0 ? new Random(settings.Seed) : new Random();
            Hostility = HostilityRules.For(settings);
        }

        /// <summary>
        /// What the players wear: slots, items, bonuses and sets per hero (<see cref="ActorGear"/>).
        /// </summary>
        public ActorGear Gear { get; }

        /// <summary>
        /// What the players have learned in the skill tree (<see cref="ActionRpgContent.SkillTree"/>), and their points.
        /// </summary>
        public ActorSkills Skills { get; }

        /// <summary>
        /// The players' crafts from their backpacks (<see cref="ActionRpgContent.Recipes"/>).
        /// </summary>
        public ActorCrafting Crafting { get; }

        /// <summary>
        /// Conversations with conditions and effects (<see cref="ActionRpgContent.Dialogues"/>).
        /// </summary>
        public ActorDialogue Dialogue { get; }

        /// <summary>
        /// Potions, food and scrolls the players use from their backpacks, with their cooldowns.
        /// </summary>
        public ActorConsumables Consumables { get; }

        /// <summary>
        /// The players' standing with the game's factions (<see cref="ActionRpgContent.Factions"/>).
        /// </summary>
        public ActorReputation Reputation { get; }

        /// <summary>
        /// The players' achievements (<see cref="ActionRpgContent.Achievements"/>): their deeds counted by kind, what
        /// they unlocked. A profile apart from the saves: keep <c>Achievements.Capture(playerId)</c> where the game
        /// keeps its profile and give it back with <c>Restore</c>. Null when the game has no achievements.
        /// </summary>
        public RPGFramework.Achievements.AchievementTracker Achievements { get; internal set; }

        internal IEntityManager Entities => _entities;

        public string Loadout => Gear.LoadoutOf(PlayerId);

        public IItem WornIn(string slotId) => Gear.WornIn(PlayerId, slotId);

        public bool Equip(string itemId, string slotId = null) => IsStarted && Gear.Equip(PlayerId, itemId, slotId);

        public bool Unequip(string slotId) => IsStarted && Gear.Unequip(PlayerId, slotId);

        public bool UseLoadout(string loadout) => IsStarted && Gear.UseLoadout(PlayerId, loadout);

        /// <summary>
        /// Who may attack whom (<see cref="HostilityRules.For"/> the settings).
        /// </summary>
        public IHostilityRules Hostility { get; }

        /// <summary>
        /// The match running in this world (set by <see cref="PvpMatch"/> itself).
        /// </summary>
        internal PvpMatch Match { get; set; }

        public PvpStatus PvpStatus => _settings.Pvp != null ? Match?.Status : null;

        /// <summary>
        /// Status effects on characters; null unless the game uses them (<see cref="ActionRpgSettings.UseStatusEffects"/>).
        /// </summary>
        public ActorEffects Effects { get; internal set; }

        /// <summary>
        /// Abilities of characters; null unless the game uses them (<see cref="ActionRpgSettings.UseAbilities"/>).
        /// </summary>
        public ActorAbilities Abilities { get; internal set; }

        public IEnumerable<ActorEffect> EffectsOn(int entityId) =>
            Effects?.On(entityId) ?? Enumerable.Empty<ActorEffect>();

        public bool UseAbility(string abilityId, int targetId = -1) =>
            IsStarted && Abilities != null && Abilities.Use(PlayerId, abilityId, targetId).Success;

        public float CooldownLeft(string abilityId) => CooldownLeft(PlayerId, abilityId);

        public float CooldownLeft(int entityId, string abilityId) => Abilities?.CooldownLeft(entityId, abilityId) ?? 0f;

        public long CurrencyOf(string currency = RPGFramework.Trading.Currencies.Gold) => IsStarted ? CurrencyOf(PlayerId, currency) : 0;

        public bool TakeOffer(string offerId) => IsStarted && Trade(PlayerId, offerId).Success;

        public ActionRpgSettings Settings => _settings;
        public bool IsStarted => PlayerId >= 0;
        public int PlayerId { get; private set; } = -1;
        public Actor Player => PlayerId >= 0 && _actors.TryGetValue(PlayerId, out var player) ? player : null;
        public IInventoryContainer<IItem> Inventory => InventoryOf(PlayerId);

        /// <summary>
        /// The items the game knows (what loot, rewards and trades hand out).
        /// </summary>
        public IItemRegistry ItemRegistry => _items;

        /// <summary>
        /// Every character's resistances to damage elements: their own (<see cref="CharacterData.Resistances"/>) and
        /// their gear's ("resist:fire" bonuses); a game adds its own sources (a buff) with a source id.
        /// </summary>
        public DamageResistances Resistances { get; } = new DamageResistances();

        /// <summary>
        /// The game's content (set by the module): rolled gear is built from it.
        /// </summary>
        public ActionRpgContent Content { get; internal set; }

        /// <summary>
        /// An item by id from the registry; a rolled one (<see cref="RolledItems"/>) is built from the content and
        /// registered the first time it is asked for.
        /// </summary>
        public IItem ItemOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;
            var item = _items.GetItem(itemId);
            if (item != null || Content == null || !RolledItems.IsRolled(itemId))
                return item;
            item = Content.FindItem(itemId);
            if (item != null)
                _items.RegisterItem(item);
            return item;
        }
        public int Kills => KillsOf(PlayerId);
        public int Level => LevelOf(PlayerId);
        public long TotalExperience => ExperienceOf(PlayerId);

        /// <summary>
        /// Total experience at which the player reaches the next level.
        /// </summary>
        public long ExperienceForNextLevel => ExperienceForNextLevelOf(PlayerId);

        public ProgressMode ProgressMode => _settings.ProgressMode;

        /// <summary>
        /// Whose progress a player's deeds go to: their own (<see cref="ProgressMode.PerPlayer"/>) or the first
        /// player's, which the party shares. -1 for someone not in the party.
        /// </summary>
        public int ProgressOwnerOf(int playerId)
        {
            if (!_party.Contains(playerId))
                return -1;
            return _settings.ProgressMode == ProgressMode.PerPlayer ? playerId : PlayerId;
        }

        /// <summary>
        /// The backpack a player fills (the party's shared one, or their own); null for someone not in the party.
        /// </summary>
        public IInventoryContainer<IItem> InventoryOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 && _inventories.TryGetValue(owner, out var inventory) ? inventory : null;
        }

        public int KillsOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 && _kills.TryGetValue(owner, out int kills) ? kills : 0;
        }

        public int LevelOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 ? _progression.GetLevel(owner) : 0;
        }

        public long ExperienceOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 ? _progression.GetTotalXP(owner) : 0;
        }

        public long ExperienceForNextLevelOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 ? _progression.GetXPRequiredForLevel(_progression.GetLevel(owner) + 1) : 0;
        }

        /// <summary>
        /// Experience for a player (their own, or the party's); for game rules such as a reward split at a finish.
        /// </summary>
        /// <returns>False for someone not in the party.</returns>
        public bool GrantExperience(int playerId, long amount)
        {
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0 || amount <= 0)
                return false;
            _progression.AddExperience(owner, amount);
            return true;
        }

        /// <summary>
        /// Number of the last wave started (0 before the first one).
        /// </summary>
        public int CurrentWave { get; private set; }

        /// <summary>
        /// Recent happenings, newest last (for a HUD log).
        /// </summary>
        public IReadOnlyList<string> Log => _log;
        public IReadOnlyList<LogLine> LogLines => _logLines;

        /// <summary>
        /// Everyone in the world, the player included.
        /// </summary>
        public IEnumerable<Actor> Actors => _actors.Values;

        /// <summary>
        /// Fills <paramref name="ids"/> with everyone in the world without allocating (for systems that run every frame).
        /// </summary>
        internal void CopyActorIds(List<int> ids)
        {
            ids.Clear();
            foreach (int id in _actors.Keys)
                ids.Add(id);
        }

        public IEnumerable<int> LivingEnemies => _actors.Values.Where(a => !a.IsPlayer && a.IsAlive()).Select(a => a.EntityId);

        /// <summary>
        /// The party: the first player (<see cref="PlayerId"/>) and everyone who joined.
        /// </summary>
        public IReadOnlyList<int> Players => _party;

        public IEnumerable<int> LivingPlayers => _party.Where(id => Get(id)?.IsAlive() == true);

        /// <summary>
        /// Who a deed counts for: a party member's kills and quest progress count for whoever owns their progress
        /// (<see cref="ProgressOwnerOf"/>).
        /// </summary>
        public int CreditOf(int entityId) => _party.Contains(entityId) ? ProgressOwnerOf(entityId) : entityId;

        public Actor Get(int entityId) => _actors.TryGetValue(entityId, out var actor) ? actor : null;

        public bool AreHostile(int attackerId, int targetId)
        {
            var attacker = Get(attackerId);
            var target = Get(targetId);
            return attacker != null && target != null && attacker != target && Hostility.AreHostile(attacker, target);
        }

        /// <summary>
        /// Puts an actor on a side (null = none); <see cref="PvpMatch"/> does this for players.
        /// </summary>
        public void SetTeam(int entityId, string team)
        {
            var actor = Get(entityId) ?? throw new ArgumentException($"No actor {entityId}", nameof(entityId));
            if (string.IsNullOrEmpty(team))
                team = null;
            if (actor.Team == team)
                return;
            actor.Team = team;
            _bus.Publish(new ActorTeamChangedEvent(entityId, team));
        }

        /// <summary>
        /// Quest target id of an entity (its character id); used by the Kill-objective bridge.
        /// </summary>
        public string QuestTargetOf(int entityId) => Get(entityId)?.CharacterId;

        /// <summary>
        /// Quests the player has taken, with their progress.
        /// </summary>
        public IEnumerable<(IQuest Quest, IQuestProgress Progress)> ActiveQuests => ActiveQuestsOf(PlayerId);

        public IReadOnlyList<string> CompletedQuests => CompletedQuestsOf(PlayerId);

        public IEnumerable<(IQuest Quest, IQuestProgress Progress)> ActiveQuestsOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0
                ? _quests.GetActiveQuests(owner).Select(p => (_questRegistry.GetQuest(p.QuestId), p)).Where(q => q.Item1 != null)
                : Enumerable.Empty<(IQuest, IQuestProgress)>();
        }

        /// <summary>
        /// A party member takes a quest (their own, or the party's when progress is shared); false when it is
        /// unknown, taken already or done.
        /// </summary>
        public bool AcceptQuest(int playerId, string questId)
        {
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0 || string.IsNullOrEmpty(questId) || !_quests.AcceptQuest(owner, questId))
                return false;
            Write("kit.log.newQuest", ("quest", _questRegistry.GetQuest(questId)?.Title ?? questId));
            return true;
        }

        /// <summary>
        /// Hands in a quest whose objectives are done (with <see cref="ActionRpgSettings.CompleteQuestsAutomatically"/>
        /// off: a quest giver); false while something is open.
        /// </summary>
        public bool CompleteQuest(int playerId, string questId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 && !string.IsNullOrEmpty(questId) && _quests.CompleteQuest(owner, questId);
        }

        /// <summary>
        /// Advances a party member's quests by something the game counts (a talk: Interact, a place: Explore).
        /// </summary>
        /// <returns>How many objectives advanced.</returns>
        public int ReportQuestProgress(int playerId, ObjectiveType type, string targetId, int amount = 1)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 && amount > 0 ? _quests.ReportProgress(owner, type, targetId, amount) : 0;
        }

        public IReadOnlyList<string> CompletedQuestsOf(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 ? _quests.GetCompletedQuests(owner) : (IReadOnlyList<string>)Array.Empty<string>();
        }

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
            _party.Add(PlayerId);
            Add(new Actor(PlayerId, _settings.Player, isPlayer: true) { Seat = 0 });
            BeginProgress(PlayerId);
            Write("kit.log.enters", ("name", _settings.Player.DisplayName));
        }

        // A fresh progress: level 1, an empty backpack, the starting quests
        private void BeginProgress(int owner)
        {
            _inventories[owner] = new InventoryContainer<IItem>(_settings.InventoryCapacity, _bus, owner, "Backpack");
            _kills[owner] = 0;
            _progression.SetLevel(owner, 1);
            foreach (var questId in _settings.StartingQuests)
            {
                if (_quests.AcceptQuest(owner, questId))
                    Write("kit.log.newQuest", ("quest", _questRegistry.GetQuest(questId)?.Title ?? questId));
                else
                    _logger?.LogWarning($"Starting quest '{questId}' could not be accepted", Category);
            }
        }

        /// <summary>
        /// A player joins the party (co-op): a new player character at the party's level, in the first free seat.
        /// </summary>
        /// <param name="character">Their character; null plays the same character as the first player.</param>
        /// <param name="name">Their name (cleaned, made unique in the party); empty gives "Player 2" and so on.</param>
        /// <param name="seat">The seat they want (a player coming back); taken or negative gives the first free one.</param>
        /// <param name="progress">With <see cref="ProgressMode.PerPlayer"/>: the progress they bring (their own save);
        /// null starts them fresh. Ignored when the party shares progress.</param>
        public int AddPlayer(CharacterData character = null, string name = null, int seat = -1, PlayerProgress progress = null)
        {
            ThrowIfNotStarted();
            character = character ?? _settings.Player;
            if (seat < 0 || IsSeatTaken(seat))
            {
                seat = 0;
                while (IsSeatTaken(seat))
                    seat++;
            }
            int id = _entities.CreateEntity();
            _party.Add(id);
            bool own = _settings.ProgressMode == ProgressMode.PerPlayer;
            var player = new Actor(id, character, isPlayer: true, level: own ? 1 : Level) { Seat = seat };
            player.PlayerName = UniqueName(CleanName(name) ?? $"Player {seat + 1}", id);
            Add(player);
            if (own)
            {
                BeginProgress(id);
                if (progress != null)
                    RestoreProgress(id, progress);
            }
            Write("kit.log.joins", ("name", player.DisplayName));
            return id;
        }

        /// <summary>
        /// Someone in the party sits there.
        /// </summary>
        public bool IsSeatTaken(int seat) => _party.Any(id => Get(id)?.Seat == seat);

        /// <summary>
        /// Names a player (the first player gets the character's name until named).
        /// </summary>
        /// <returns>The name given: cleaned, and made unique in the party ("Sam 2").</returns>
        public string SetPlayerName(int playerId, string name)
        {
            var player = _party.Contains(playerId) ? Get(playerId) : null;
            if (player == null)
                throw new ArgumentException($"{playerId} is not a player in the party", nameof(playerId));
            string clean = CleanName(name);
            player.PlayerName = clean == null ? null : UniqueName(clean, playerId);
            return player.DisplayName;
        }

        /// <summary>
        /// A name as others may see it: one line, no control characters, at most <see cref="MaxNameLength"/> long;
        /// null when nothing is left.
        /// </summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            var clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (clean.Length > MaxNameLength)
                clean = clean.Substring(0, MaxNameLength).TrimEnd();
            return clean.Length > 0 ? clean : null;
        }

        private string UniqueName(string name, int playerId)
        {
            bool Taken(string candidate) => _party.Any(id => id != playerId
                && string.Equals(Get(id)?.DisplayName, candidate, StringComparison.OrdinalIgnoreCase));
            if (!Taken(name))
                return name;
            for (int number = 2; ; number++)
            {
                string suffix = " " + number;
                string candidate = (name.Length + suffix.Length > MaxNameLength
                    ? name.Substring(0, MaxNameLength - suffix.Length)
                    : name) + suffix;
                if (!Taken(candidate))
                    return candidate;
            }
        }

        /// <summary>
        /// A player who joined leaves the party. The first player cannot leave.
        /// </summary>
        public bool RemovePlayer(int playerId)
        {
            if (playerId == PlayerId)
                throw new InvalidOperationException("The first player cannot leave the party");
            var player = Get(playerId);
            if (player == null || !_party.Contains(playerId))
                return false;
            // Worn items go back into the backpack the party shares, or leave with the player
            Crafting.CancelAll(playerId);
            Dialogue.Forget(playerId);
            Consumables.Forget(playerId);
            Gear.Forget(playerId, backToBackpack: _settings.ProgressMode != ProgressMode.PerPlayer);
            Skills.Forget(playerId);
            _party.Remove(playerId);
            _inventories.Remove(playerId);
            _kills.Remove(playerId);
            if (_settings.ProgressMode == ProgressMode.PerPlayer)
                Wallet.Forget(playerId);
            Remove(playerId);
            Write("kit.log.leaves", ("name", player.DisplayName));
            return true;
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
            int level = Math.Max(1, (_settings.EnemyLevels == EnemyLevelMode.PlayerLevel ? Level : 1) + _settings.EnemyLevelOffset);
            var actor = new Actor(id, character, isPlayer: false, level: level);
            RollElite(actor);
            Add(actor);
            return id;
        }

        /// <summary>
        /// Makes a spawned enemy elite now with these affixes (a boss, a scripted champion); false when it is not an
        /// enemy here, is elite already or none of the affixes is in <see cref="ActionRpgContent.EliteAffixes"/>.
        /// </summary>
        public bool MakeElite(int entityId, IEnumerable<RolledAffix> affixes)
        {
            var actor = Get(entityId);
            if (actor == null || actor.IsPlayer || actor.IsElite || Content?.EliteAffixes == null)
                return false;
            var known = (affixes ?? Enumerable.Empty<RolledAffix>()).Where(a => Content.EliteAffixes.Find(a.AffixId) != null).ToList();
            if (known.Count == 0)
                return false;
            Elevate(actor, known);
            _bus.Publish(new EliteSpawnedEvent(actor.EntityId, actor.CharacterId, actor.EliteName, known.Select(a => a.AffixId).ToList()));
            return true;
        }

        private void RollElite(Actor actor)
        {
            var pool = Content?.EliteAffixes;
            if (pool == null || _settings.EliteChance <= 0f || _random.NextDouble() >= _settings.EliteChance)
                return;
            var affixes = pool.Roll(_random, Math.Max(1, Level), _settings.EliteRarity, new[] { actor.CharacterId });
            if (affixes.Count > 0)
                Elevate(actor, affixes);
        }

        // The affixes become the enemy's modifiers and name; it starts at its new full health
        private void Elevate(Actor actor, IReadOnlyList<RolledAffix> affixes)
        {
            var pool = Content.EliteAffixes;
            actor.EliteAffixes = affixes.ToList();
            actor.EliteName = pool.NameFor(actor.Character.DisplayName, affixes);
            for (int i = 0; i < affixes.Count; i++)
            {
                var definition = pool.Find(affixes[i].AffixId);
                if (definition == null)
                    continue;
                string id = "elite:" + i;
                if (definition.Stat.StartsWith(ActorGear.ResistancePrefix, StringComparison.Ordinal))
                    Resistances.Set(actor.EntityId, definition.Stat.Substring(ActorGear.ResistancePrefix.Length), affixes[i].Value, id);
                else if (Enum.TryParse(definition.Stat, false, out ActorStat stat) && Enum.IsDefined(typeof(ActorStat), stat))
                    actor.AddModifier(new RPGFramework.Stats.StatModifier<ActorStat, float>(id, stat, affixes[i].Value, definition.ModifierType));
            }
            actor.RestoreFullHealth();
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
            Write("kit.log.wave", ("wave", CurrentWave), ("count", enemies));
            for (int i = 0; i < enemies; i++)
                SpawnEnemy(pool[_random.Next(pool.Count)].Id);
            _bus.Publish(new WaveStartedEvent(CurrentWave, enemies));
        }

        /// <summary>
        /// A strike of the attacker on the target, with what kind of hit it is (tags such as a hit zone, the
        /// ability or weapon that dealt it). The damage calculator sees the context when it implements
        /// <see cref="IContextualDamageCalculator"/>.
        /// </summary>
        /// <returns>Damage dealt (0 when either side is missing or dead, or they are not hostile).</returns>
        public float Attack(int attackerId, int targetId, AttackContext context = default)
        {
            if (!CanStrike(attackerId, targetId))
                return 0f;
            // Someone else settles this one later (the attacked player's machine, in co-op)
            if (HoldAttack != null && HoldAttack(attackerId, targetId, context.Between(attackerId, targetId)))
                return 0f;
            return Strike(attackerId, targetId, context, 1f);
        }

        /// <summary>
        /// Takes an attack over before it lands (attacker, target, context); true when it is taken.
        /// </summary>
        internal Func<int, int, AttackContext, bool> HoldAttack { get; set; }

        internal bool CanStrike(int attackerId, int targetId)
        {
            var attacker = Get(attackerId);
            var target = Get(targetId);
            return attacker != null && target != null && attacker.IsAlive() && target.IsAlive() && AreHostile(attackerId, targetId);
        }

        // An attack as it lands, with a share of its damage (a partial block)
        internal float Strike(int attackerId, int targetId, AttackContext context, float share)
        {
            if (!CanStrike(attackerId, targetId))
                return 0f;
            var attacker = Get(attackerId);
            var target = Get(targetId);
            context = context.Between(attackerId, targetId);
            // The attacker's own chance; a hit the game already made critical is not rolled again
            if (!context.IsCritical && attacker.CriticalChance > 0f && _random.NextDouble() < attacker.CriticalChance)
                context = context.AsCritical(Math.Max(1f, attacker.CriticalDamage));
            if (target.Evasion > 0f && _random.NextDouble() < target.Evasion)
            {
                _bus.Publish(new ActorEvadedEvent(targetId, attackerId, context));
                return 0f;
            }
            float dealt = Hit(target, attacker.Attack, context, share);
            if (dealt > 0f && attacker.LifeSteal > 0f && attacker.IsAlive())
                Heal(attackerId, dealt * attacker.LifeSteal);
            return dealt;
        }

        /// <summary>
        /// Damage of a set amount that is not the attacker's strike: an effect ticking (poison, burning; use
        /// <see cref="AttackContext.Effect"/>), a trap, an explosion. The calculator still decides the final number
        /// (the framework's ignores defense for effect damage).
        /// </summary>
        /// <param name="attackerId">Who it counts for (kills, credit); -1 for nobody.</param>
        /// <returns>Damage dealt (0 when the target is missing or dead, or the attacker may not hurt it).</returns>
        public float DealDamage(int attackerId, int targetId, float amount, AttackContext context = default)
        {
            var target = Get(targetId);
            if (target == null || !target.IsAlive() || amount <= 0f || float.IsNaN(amount))
                return 0f;
            var attacker = Get(attackerId);
            if (attacker != null && attacker != target && !AreHostile(attackerId, targetId))
                return 0f;
            return Hit(target, amount, context.Between(attacker != null ? attackerId : -1, targetId), 1f);
        }

        /// <summary>
        /// Gives back health to a living character (a potion, healing over time).
        /// </summary>
        /// <returns>Health actually restored.</returns>
        public float Heal(int entityId, float amount)
        {
            var actor = Get(entityId);
            float restored = actor?.Heal(amount) ?? 0f;
            if (restored > 0f)
                _bus.Publish(new ActorHealedEvent(entityId, restored, actor.Health));
            return restored;
        }

        private readonly List<string> _hitElements = new List<string>();

        // The elements the hit's tags carry, against the target's resistances
        private float Resisted(int targetId, AttackContext context, float damage)
        {
            if (context.Tags == 0 || _settings.ElementTags.Count == 0)
                return damage;
            _hitElements.Clear();
            foreach (var pair in _settings.ElementTags)
            {
                if (pair.Value != 0 && context.HasAny(pair.Value))
                    _hitElements.Add(pair.Key);
            }
            return _hitElements.Count == 0 ? damage : Resistances.Apply(targetId, _hitElements, damage);
        }

        private float Hit(Actor target, float baseDamage, AttackContext context, float share)
        {
            // A crit or a power strike multiplies the strike; the calculator still sees the context (IsCritical)
            baseDamage *= context.Multiplier;
            float damage = _damage is IContextualDamageCalculator contextual
                ? contextual.CalculateDamage(baseDamage, target.Defense, context)
                : _damage.CalculateDamage(baseDamage, target.Defense);
            damage = Resisted(target.EntityId, context, damage);
            float dealt = target.ApplyDamage(damage * share);
            _bus.Publish(new ActorDamagedEvent(target.EntityId, context.AttackerId, dealt, target.Health, context));
            if (target.Health <= 0f)
            {
                _killingHits[target.EntityId] = context;
                _lifeStates.Kill(target.EntityId, context.AttackerId, context.FromEffect ? "effect" : "slain");
                // The death is handled during Kill; a refused kill (invulnerable) leaves nothing behind
                _killingHits.Remove(target.EntityId);
            }
            return dealt;
        }

        /// <summary>
        /// True while <see cref="RestoreProgress"/> replays saved quests: rules that grant rewards on
        /// <c>QuestCompletedEvent</c> should skip them then, the player already has them.
        /// </summary>
        public bool IsRestoring { get; private set; }

        /// <summary>
        /// The player's level, experience, backpack and quests, for saving.
        /// </summary>
        public PlayerProgress CaptureProgress()
        {
            ThrowIfNotStarted();
            return CaptureProgress(PlayerId);
        }

        /// <summary>
        /// A party member's progress (their own, or the party's when it is shared): what they take home.
        /// </summary>
        public PlayerProgress CaptureProgress(int playerId)
        {
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0)
                throw new ArgumentException($"{playerId} is not a player in the party", nameof(playerId));
            var progress = new PlayerProgress
            {
                Level = _progression.GetLevel(owner), TotalExperience = _progression.GetTotalXP(owner), Kills = KillsOf(owner)
            };
            progress.Items.AddRange(_inventories[owner].GetAllItems().Where(i => i != null).Select(i => i.ItemId));
            // A craft in progress is not saved; its ingredients are, as if it was called off
            foreach (int member in _party.Where(m => ProgressOwnerOf(m) == owner))
                progress.Items.AddRange(Crafting.IngredientsInProgress(member));
            progress.Currencies.AddRange(Wallet.BalancesOf(owner).Where(b => b.Value > 0).OrderBy(b => b.Key, StringComparer.Ordinal)
                .Select(b => new CurrencyRecord { Currency = b.Key, Amount = b.Value }));
            foreach (var active in _quests.GetActiveQuests(owner))
            {
                var record = new QuestRecord { QuestId = active.QuestId };
                record.Objectives.AddRange(active.ObjectiveProgress.Select(p => new ObjectiveRecord { ObjectiveId = p.Key, Progress = p.Value }));
                progress.ActiveQuests.Add(record);
            }
            progress.CompletedQuests.AddRange(_quests.GetCompletedQuests(owner));
            progress.Gear.AddRange(Gear.RecordsOf(playerId));
            progress.Loadout = Gear.LoadoutOf(playerId);
            progress.Skills.AddRange(Skills.RecordsOf(playerId));
            progress.DialogueVariables.AddRange(Dialogue.RecordsOf(playerId));
            progress.Reputations.AddRange(Reputation.RecordsOf(playerId));
            progress.BonusSkillPoints = Skills.BonusPointsOf(playerId);
            return progress;
        }

        /// <summary>
        /// Puts saved progress back: level and stats, experience, backpack, quests. Rewards of completed quests are
        /// not granted again. Items and quests that no longer exist in the content are skipped with a warning.
        /// </summary>
        public void RestoreProgress(PlayerProgress progress)
        {
            ThrowIfNotStarted();
            RestoreProgress(PlayerId, progress);
        }

        /// <summary>
        /// Puts a party member's progress back (with <see cref="ProgressMode.PerPlayer"/>: what they brought along).
        /// </summary>
        public void RestoreProgress(int playerId, PlayerProgress progress)
        {
            ThrowIfNotStarted();
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0)
                throw new ArgumentException($"{playerId} is not a player in the party", nameof(playerId));
            var inventory = _inventories[owner];
            foreach (int member in _party.Where(m => ProgressOwnerOf(m) == owner).ToList())
                Crafting.CancelAll(member);

            IsRestoring = true;
            try
            {
                int level = Math.Max(1, progress.Level);
                _progression.SetLevel(owner, level);
                long missing = progress.TotalExperience - _progression.GetTotalXP(owner);
                if (missing > 0)
                    _progression.AddExperience(owner, missing);
                var actor = Get(owner);
                foreach (ActorStat stat in Enum.GetValues(typeof(ActorStat)))
                    actor.SetBaseStat(stat, actor.Character.StatAt(stat, _progression.GetLevel(owner)));
                actor.RestoreFullHealth();
                _kills[owner] = Math.Max(0, progress.Kills);

                foreach (var currency in Wallet.BalancesOf(owner).Keys.ToList())
                    Wallet.SetBalance(owner, currency, 0);
                foreach (var record in progress.Currencies ?? new List<CurrencyRecord>())
                {
                    if (!string.IsNullOrEmpty(record?.Currency))
                        Wallet.SetBalance(owner, record.Currency, Math.Max(0, record.Amount));
                }

                inventory.Clear();
                foreach (var itemId in progress.Items ?? new List<string>())
                {
                    var item = ItemOf(itemId);
                    if (item == null)
                        _logger?.LogWarning($"Saved item '{itemId}' is no longer in the game; skipped", Category);
                    else if (!inventory.Add(item))
                        break;
                }
                Gear.Restore(playerId, progress.Gear, progress.Loadout);
                Skills.Restore(playerId, progress.Skills, progress.BonusSkillPoints);
                Dialogue.Restore(playerId, progress.DialogueVariables);
                Reputation.Restore(playerId, progress.Reputations);

                var completed = _quests.GetCompletedQuests(owner);
                foreach (var questId in progress.CompletedQuests ?? new List<string>())
                {
                    var quest = _questRegistry.GetQuest(questId);
                    if (quest == null || completed.Contains(questId))
                        continue;
                    if (_quests.GetQuestProgress(owner, questId) == null)
                        _quests.AcceptQuest(owner, questId);
                    foreach (var objective in quest.Objectives)
                        SetObjective(owner, questId, objective.Id, objective.RequiredCount);
                    _quests.CompleteQuest(owner, questId);
                }

                foreach (var record in progress.ActiveQuests ?? new List<QuestRecord>())
                {
                    if (_questRegistry.GetQuest(record.QuestId) == null)
                    {
                        _logger?.LogWarning($"Saved quest '{record.QuestId}' is no longer in the game; skipped", Category);
                        continue;
                    }
                    if (_quests.GetQuestProgress(owner, record.QuestId) == null && !_quests.AcceptQuest(owner, record.QuestId))
                        continue;
                    foreach (var objective in record.Objectives ?? new List<ObjectiveRecord>())
                        SetObjective(owner, record.QuestId, objective.ObjectiveId, objective.Progress);
                }
            }
            finally
            {
                IsRestoring = false;
            }
            if (owner == PlayerId)
                Write("kit.log.welcomeBack", ("level", Level), ("count", inventory.Count));
            else
                Write("kit.log.brings", ("name", Get(owner)?.DisplayName), ("level", _progression.GetLevel(owner)), ("count", inventory.Count));
        }

        private void SetObjective(int owner, string questId, string objectiveId, int target)
        {
            var current = _quests.GetQuestProgress(owner, questId);
            int done = current != null && current.ObjectiveProgress.TryGetValue(objectiveId, out int value) ? value : 0;
            if (target > done)
                _quests.UpdateObjectiveProgress(owner, questId, objectiveId, target - done);
        }

        public bool RevivePlayer() => _settings.Pvp == null && RevivePlayer(PlayerId);

        /// <summary>
        /// Brings a fallen party member back with full health.
        /// </summary>
        public bool RevivePlayer(int playerId) => RevivePlayer(playerId, -1, 1f);

        /// <summary>
        /// Brings a fallen party member back, raised by someone (an ally running through them; -1 for nobody) with
        /// part of their health.
        /// </summary>
        /// <param name="healthFraction">Share of max health they come back with (0..1; at least 1 health point).</param>
        public bool RevivePlayer(int playerId, int reviverId, float healthFraction)
        {
            if (!IsStarted || !_party.Contains(playerId))
                return false;
            if (!_lifeStates.Resurrect(playerId, reviverId, reviverId >= 0 ? "revived" : "respawn"))
                return false;
            var player = Get(playerId);
            if (player != null && healthFraction < 1f)
                player.SetHealth(Math.Max(1f, player.MaxHealth * Math.Max(0f, healthFraction)));
            if (reviverId >= 0 && reviverId != playerId)
                Write("kit.log.revives", ("reviver", Get(reviverId)?.DisplayName ?? "Someone"), ("name", player?.DisplayName));
            return true;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            Dialogue.Dispose();
            foreach (int id in new List<int>(_actors.Keys))
                Remove(id);
        }

        private void Add(Actor actor)
        {
            _actors.Add(actor.EntityId, actor);
            _lifeStates.RegisterEntity(actor.EntityId, actor);
            var elite = actor.IsElite ? actor.EliteAffixes.ToList() : null;
            // An elite's resistances (rolled before it was added) stay: SetBase replaces only the character's own
            Resistances.SetBase(actor.EntityId, actor.Character?.Resistances);
            _bus.Publish(new ActorSpawnedEvent(actor.EntityId, actor.CharacterId, actor.IsPlayer));
            if (elite != null)
                _bus.Publish(new EliteSpawnedEvent(actor.EntityId, actor.CharacterId, actor.EliteName, elite.Select(a => a.AffixId).ToList()));
        }

        private void Remove(int entityId)
        {
            if (!_actors.TryGetValue(entityId, out var actor))
                return;
            _actors.Remove(entityId);
            _lifeStates.UnregisterEntity(entityId);
            Resistances.Forget(entityId);
            if (_entities.Exists(entityId))
                _entities.DestroyEntity(entityId);
            _bus.Publish(new ActorDespawnedEvent(entityId, actor.CharacterId, actor.IsPlayer));
        }

        private void OnDeath(DeathEvent death)
        {
            _killingHits.TryGetValue(death.EntityId, out var killingHit);
            _killingHits.Remove(death.EntityId);
            var victim = Get(death.EntityId);
            if (victim == null)
                return;

            if (victim.IsPlayer)
            {
                var killer = Get(death.KillerEntityId);
                bool byPlayer = killer != null && killer.IsPlayer && killer != victim;
                if (!byPlayer)
                    Write("kit.log.falls", ("name", victim.DisplayName));
                _bus.Publish(new PlayerDefeatedEvent(victim.EntityId, death.KillerEntityId, byPlayer, killingHit));
                return;
            }

            bool byParty = _party.Contains(death.KillerEntityId);
            // Every kill counts on the shared tally; with own progress, only a player's own kills count for them
            int owner = byParty ? ProgressOwnerOf(death.KillerEntityId) : PlayerId;
            if (byParty || _settings.ProgressMode == ProgressMode.Shared)
                _kills[owner] = KillsOf(owner) + 1;
            _bus.Publish(new ActorKilledEvent(victim.EntityId, victim.CharacterId, death.KillerEntityId, byParty, killingHit));
            Remove(death.EntityId);
            if (!byParty)
                return;

            Write("kit.log.slain", ("name", victim.DisplayName));
            if (!string.IsNullOrEmpty(victim.Character.Faction) && victim.Character.ReputationOnKill != 0)
                Reputation.Change(death.KillerEntityId, victim.Character.Faction, victim.Character.ReputationOnKill);
            float reward = victim.IsElite ? Math.Max(0f, _settings.EliteRewardMultiplier) : 1f;
            long experience = victim.Character.ExperienceReward + victim.Character.ExperiencePerLevel * Math.Max(0, victim.Level - 1);
            if (experience > 0)
                _progression.AddExperience(owner, (long)Math.Round(experience * reward));

            if (string.IsNullOrEmpty(victim.Character.LootTableId))
                return;
            int rolls = victim.IsElite ? Math.Max(1, (int)Math.Round(reward)) : 1;
            for (int roll = 0; roll < rolls; roll++)
                DropLoot(victim.Character.LootTableId, death.KillerEntityId, owner, victim.EntityId, _settings.LootToBackpack);
        }

        // Rolls a table at the owner's level; money goes to the owner's wallet, items to the player's backpack. Without
        // the backpack the drops are only announced: the game puts them on the ground for someone to pick up
        private bool DropLoot(string tableId, int playerId, int owner, int sourceId, bool toBackpack)
        {
            var loot = _loot.GenerateLootById(tableId, _progression.GetLevel(owner), seed: _random.Next());
            if (loot.Drops == null)
                return false;
            _bus.Publish(new LootGeneratedEvent
            {
                TableId = loot.TableId, TriggerEntityId = playerId, SourceEntityId = sourceId, ItemCount = loot.TotalCount
            });
            foreach (var raw in loot.Drops)
            {
                foreach (var (itemId, quantity) in Rolled(raw.ItemId, raw.Quantity, owner))
                {
                    if (_settings.Currencies.Contains(itemId))
                    {
                        if (toBackpack)
                            Wallet.Add(owner, quantity, itemId, "loot:" + loot.TableId);
                    }
                    else if (toBackpack && !GiveItem(playerId, itemId, quantity))
                    {
                        continue;
                    }
                    _bus.Publish(new LootDroppedEvent
                    {
                        ItemId = itemId, Quantity = quantity, ItemEntityId = -1, SourceEntityId = sourceId
                    });
                }
            }
            return true;
        }

        // Gear rolls its affixes one piece at a time (ActionRpgContent.Affixes); anything else drops as it is
        private IEnumerable<(string ItemId, int Quantity)> Rolled(string itemId, int quantity, int owner)
        {
            var pool = Content?.Affixes;
            var gear = pool != null && !RolledItems.IsRolled(itemId) ? Content.FindGear(itemId) : null;
            if (gear == null)
            {
                yield return (itemId, quantity);
                yield break;
            }
            float luck = Content.LuckOf?.Invoke(owner) ?? 0f;
            var kinds = new[] { gear.Kind };
            for (int i = 0; i < quantity; i++)
                yield return (RolledItems.Roll(itemId, pool, Content.Rarities, _random, _progression.GetLevel(owner), luck, kinds), 1);
        }

        private void OnObjectiveProgressed(ObjectiveProgressedEvent progress)
        {
            if (!OwnsProgress(progress.EntityId) || IsRestoring)
                return;
            string title = _questRegistry.GetQuest(progress.QuestId)?.Title ?? progress.QuestId;
            Write("kit.log.progress", ("quest", title), ("done", progress.CurrentProgress), ("required", progress.RequiredProgress));
            // CompleteQuest refuses while any objective is still open
            if (progress.IsCompleted && _settings.CompleteQuestsAutomatically)
                _quests.CompleteQuest(progress.EntityId, progress.QuestId);
        }

        private void OnQuestCompleted(QuestCompletedEvent completed)
        {
            if (!OwnsProgress(completed.EntityId) || IsRestoring)
                return;
            Write("kit.log.questDone", ("quest", _questRegistry.GetQuest(completed.QuestId)?.Title ?? completed.QuestId));
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
                        GiveItem(completed.EntityId, reward.RewardId, reward.Amount);
                        break;
                    case RewardType.Currency:
                        GiveCurrency(completed.EntityId, reward.Amount,
                            string.IsNullOrEmpty(reward.RewardId) ? RPGFramework.Trading.Currencies.Gold : reward.RewardId, "quest:" + completed.QuestId);
                        break;
                    case RewardType.Custom when reward.RewardId != null && reward.RewardId.StartsWith(ActorReputation.Prefix, StringComparison.Ordinal):
                        Reputation.Change(completed.EntityId, reward.RewardId.Substring(ActorReputation.Prefix.Length), reward.Amount);
                        break;
                    default:
                        _logger?.LogWarning($"Reward type {reward.Type} is not handled by ActorWorld; subscribe to " +
                                            "QuestCompletedEvent in a game module to grant it", Category);
                        break;
                }
            }
        }

        // The entity holds a progress of its own: the first player, or anyone in the party when progress is per player
        private bool OwnsProgress(int entityId) => entityId == PlayerId
            || (_settings.ProgressMode == ProgressMode.PerPlayer && _party.Contains(entityId));

        private void OnLevelUp(LevelUpEvent levelUp)
        {
            if (_settings.ProgressMode == ProgressMode.PerPlayer && levelUp.EntityId != PlayerId && _party.Contains(levelUp.EntityId))
            {
                // Their own level: only they grow, by their own character
                var member = Get(levelUp.EntityId);
                foreach (ActorStat stat in Enum.GetValues(typeof(ActorStat)))
                    member.SetBaseStat(stat, member.Character.StatAt(stat, levelUp.NewLevel));
                member.RestoreFullHealth();
                Write("kit.log.memberLevel", ("name", member.DisplayName), ("level", levelUp.NewLevel));
                return;
            }
            if (levelUp.EntityId != PlayerId)
                return;
            // Stat growth for this level-up was already applied: StatGrowthPublisher subscribed before the world did
            Player.RestoreFullHealth();
            // Everyone else in the party grows with it, by their own character's growth
            foreach (int member in _party)
            {
                var actor = Get(member);
                if (actor == null || member == PlayerId || _settings.ProgressMode == ProgressMode.PerPlayer)
                    continue;
                foreach (ActorStat stat in Enum.GetValues(typeof(ActorStat)))
                    actor.SetBaseStat(stat, actor.Character.StatAt(stat, levelUp.NewLevel));
                actor.RestoreFullHealth();
            }
            Write("kit.log.levelUp", ("level", levelUp.NewLevel));
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
            if (added.EntityId == PlayerId && !IsRestoring)
                Write("kit.log.item", ("count", added.Quantity), ("item", added.Item.Name));
        }

        /// <summary>
        /// Puts items into the player's backpack.
        /// </summary>
        /// <returns>False when the item is unknown or the backpack filled up.</returns>
        public bool GiveItem(string itemId, int quantity = 1)
        {
            ThrowIfNotStarted();
            return GiveItem(PlayerId, itemId, quantity);
        }

        /// <summary>
        /// Every player's money (owner: <see cref="ProgressOwnerOf"/>, so a party that shares progress shares it).
        /// Loot and quest rewards in <see cref="ActionRpgSettings.Currencies"/> pay into it; shops pay from it.
        /// </summary>
        public ICurrencyWallet Wallet { get; }

        /// <summary>
        /// The shop rules (<see cref="Trade"/>); set by the module.
        /// </summary>
        public ITradingManager Trading { get; set; }

        /// <summary>
        /// How much of a currency a party member has (their own, or the party's); 0 for someone not in the party.
        /// </summary>
        public long CurrencyOf(int playerId, string currency = RPGFramework.Trading.Currencies.Gold)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 ? Wallet.Balance(owner, currency) : 0;
        }

        /// <summary>
        /// Pays money to a party member (a pickup, a reward the game grants).
        /// </summary>
        /// <returns>False for someone not in the party.</returns>
        public bool GiveCurrency(int playerId, long amount, string currency = RPGFramework.Trading.Currencies.Gold, string reason = null)
        {
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0)
                return false;
            Wallet.Add(owner, amount, currency, reason);
            return true;
        }

        /// <summary>
        /// Takes money from a party member, all or nothing.
        /// </summary>
        /// <returns>False for someone not in the party, or when they do not have enough.</returns>
        public bool SpendCurrency(int playerId, long amount, string currency = RPGFramework.Trading.Currencies.Gold, string reason = null)
        {
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 && Wallet.TrySpend(owner, amount, currency, reason);
        }

        /// <summary>
        /// A party member takes a trader's offer (a shop purchase or sale): money and items move together or not at
        /// all, from their own wallet and backpack or the party's.
        /// </summary>
        public TradeResultData Trade(int playerId, string offerId)
        {
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0)
                return TradeResultData.CreateFailure(TradeError.NotInParty, TradingManager.MessageOf(TradeError.NotInParty));
            if (Trading == null)
                return TradeResultData.CreateFailure(TradeError.NoTraders, TradingManager.MessageOf(TradeError.NoTraders));
            string refused = Reputation.WhyNotTrade(playerId, TraderOf(offerId));
            if (refused != null)
                return TradeResultData.CreateFailure(TradeError.ReputationTooLow, refused);
            TradingPlayer = playerId;
            try
            {
                return Trading.Trade(owner, offerId);
            }
            finally
            {
                TradingPlayer = -1;
            }
        }

        /// <summary>
        /// The gold a party member would get from an offer now (negative: what they would pay), with
        /// <see cref="TradePricing"/>: for the shop's price labels.
        /// </summary>
        public long GoldFor(int playerId, string offerId)
        {
            int owner = ProgressOwnerOf(playerId);
            if (owner < 0 || Trading == null)
                return 0;
            TradingPlayer = playerId;
            try
            {
                return Trading.GoldFor(owner, offerId);
            }
            finally
            {
                TradingPlayer = -1;
            }
        }

        /// <summary>
        /// Prices that depend on who trades (a discount from the hero's Trade stat): given the party member who
        /// trades, not the wallet's owner. Set by the game, or registered as <see cref="ITradePricing"/> in the
        /// container; null uses the offers' own gold amounts.
        /// </summary>
        public ITradePricing TradePricing { get; set; }

        /// <summary>
        /// The game's traders (set by the module): a faction's trader refuses players it hates.
        /// </summary>
        public ITraderRegistry Traders { get; internal set; }

        // The trader who makes an offer; null when unknown
        private string TraderOf(string offerId) =>
            Traders?.GetAllTraders().FirstOrDefault(t => t.GetOffer(offerId) != null)?.TraderId;

        // The party member whose trade is running (-1 otherwise): prices are theirs even when the party pays
        internal int TradingPlayer { get; private set; } = -1;

        /// <summary>
        /// The places players use: chests, doors, levers, traders (the scene's <c>InteractionPointView</c>s and any
        /// the game adds). The game's rules react to <see cref="InteractedEvent"/>; the host measures the distance.
        /// </summary>
        public IInteractionService Interactions { get; internal set; }

        /// <summary>
        /// The game's pause (set by the module): a rule pauses with <c>GamePause.Pause("vote")</c> and lifts it with
        /// <c>Resume("vote")</c>; systems then tick with no time passing.
        /// </summary>
        public IGamePause GamePause { get; internal set; }

        public bool IsPaused => GamePause?.IsPaused == true;

        public InteractionPoint NearestInteraction => IsStarted ? Interactions.NearestFor(PlayerId) : null;

        public bool Interact(string pointId = null) => IsStarted && Interact(PlayerId, pointId) == InteractionOutcome.Done;

        /// <summary>
        /// A party member who is alive uses a place (the nearest to them when <paramref name="pointId"/> is null).
        /// </summary>
        public InteractionOutcome Interact(int playerId, string pointId)
        {
            if (!_party.Contains(playerId) || Get(playerId)?.IsAlive() != true)
                return InteractionOutcome.Refused;
            return Interactions.Interact(playerId, pointId);
        }

        /// <summary>
        /// Rolls a loot table for a party member (a chest they opened, a reward): money in
        /// <see cref="ActionRpgSettings.Currencies"/> goes to their wallet, items to their backpack, at their level.
        /// </summary>
        /// <returns>False for someone not in the party or an unknown table.</returns>
        public bool GiveLoot(int playerId, string tableId)
        {
            ThrowIfNotStarted();
            int owner = ProgressOwnerOf(playerId);
            return owner >= 0 && !string.IsNullOrEmpty(tableId) && DropLoot(tableId, playerId, owner, -1, toBackpack: true);
        }

        /// <summary>
        /// Puts items into the backpack a party member fills (their own, or the party's): loot they picked up.
        /// </summary>
        /// <returns>False for someone not in the party, an unknown item, or a full backpack.</returns>
        /// <summary>
        /// Takes items out of a party member's backpack, all or none.
        /// </summary>
        /// <returns>False when there are not that many.</returns>
        public bool TakeItem(int playerId, string itemId, int quantity = 1)
        {
            var inventory = InventoryOf(playerId);
            if (inventory == null || quantity <= 0)
                return false;
            var matching = inventory.GetAllItems().Where(i => i != null && i.ItemId == itemId).Take(quantity).ToList();
            if (matching.Count < quantity)
                return false;
            foreach (var item in matching)
                inventory.Remove(item);
            return true;
        }

        public bool GiveItem(int playerId, string itemId, int quantity)
        {
            ThrowIfNotStarted();
            var inventory = InventoryOf(playerId);
            if (inventory == null)
                return false;
            var item = ItemOf(itemId);
            if (item == null)
            {
                _logger?.LogError($"Unknown item '{itemId}'", Category);
                return false;
            }

            for (int i = 0; i < quantity; i++)
            {
                if (!inventory.Add(item))
                {
                    Write("kit.log.backpackFull");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Adds a line to the log the HUD shows (for game rules: "Bounty paid", "Boss enraged").
        /// </summary>
        public void Announce(string line) => Write(new LogLine(default, line));

        /// <summary>
        /// Adds a line to translate (the game's key in its string tables) with its English.
        /// </summary>
        public void Announce(LocalizedText text, string english) => Write(new LogLine(text, english ?? text.Key));

        private void Write(string key, params (string Name, object Value)[] args) => Write(KitLog.Line(key, args));

        private void Write(LogLine line)
        {
            _log.Add(line.English);
            _logLines.Add(line);
            if (_log.Count > MaxLogLines)
            {
                _log.RemoveAt(0);
                _logLines.RemoveAt(0);
            }
            _logger?.LogInfo(line.English, Category);
        }

        private void ThrowIfNotStarted()
        {
            if (!IsStarted)
                throw new InvalidOperationException("Start the world first");
        }
    }
}
