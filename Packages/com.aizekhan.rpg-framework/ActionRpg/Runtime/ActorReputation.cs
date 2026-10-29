using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Factions;

namespace RPGFramework.ActionRpg
{
    /// <summary>
    /// A player's reputation with a faction changed (a kill, a quest, a dialogue choice).
    /// </summary>
    public readonly struct ReputationChangedEvent : IEvent
    {
        public ReputationChangedEvent(int playerId, string factionId, int previous, int current, FactionStanding standing)
        {
            PlayerId = playerId;
            FactionId = factionId;
            Previous = previous;
            Current = current;
            Standing = standing;
        }

        public int PlayerId { get; }
        public string FactionId { get; }
        public int Previous { get; }
        public int Current { get; }
        public FactionStanding Standing { get; }
    }

    /// <summary>
    /// The players' reputation with the game's factions (<see cref="ActionRpgContent.Factions"/>), -100..100 with its
    /// standing (Hated .. Exalted). A change spreads to related factions (<see cref="ActionRpgContent.FactionRelations"/>:
    /// allies gain with it, enemies lose). Killing an enemy with a faction (<see cref="CharacterData.Faction"/>) changes
    /// it by <see cref="CharacterData.ReputationOnKill"/>; a quest's Custom reward "rep:&lt;faction&gt;" by its amount;
    /// a dialogue effect <see cref="DialogueEffectKind.ChangeReputation"/>, and conditions read "rep:&lt;faction&gt;".
    /// With shared progress the party shares its reputation. Part of the player's save.
    /// </summary>
    public sealed class ActorReputation
    {
        /// <summary>
        /// The prefix of a quest reward id or dialogue stat that means a faction's reputation ("rep:guards").
        /// </summary>
        public const string Prefix = "rep:";

        private readonly ActorWorld _world;
        private readonly IEventPublisher _events;
        private readonly FactionRegistry _registry = new FactionRegistry();
        private readonly FactionManager _manager;
        private readonly Dictionary<string, TraderFaction> _traders = new Dictionary<string, TraderFaction>();

        internal ActorReputation(ActorWorld world, IEntityManager entities, IEventPublisher events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events;
            _manager = new FactionManager(entities, _registry);
        }

        public IFactionRegistry Factions => _registry;

        internal void Register(IFaction faction)
        {
            if (faction != null && !_registry.HasFaction(faction.FactionId))
                _registry.RegisterFaction(faction);
        }

        internal void Register(IFactionRelationship relation)
        {
            if (relation != null)
                _registry.RegisterRelationship(relation);
        }

        /// <summary>
        /// The player's reputation with the faction (its default when they never dealt with it); 0 for an unknown faction.
        /// </summary>
        public int Of(int playerId, string factionId)
        {
            int owner = _world.ProgressOwnerOf(playerId);
            return owner < 0 || string.IsNullOrEmpty(factionId) ? 0 : _manager.GetReputation(owner, factionId);
        }

        public FactionStanding StandingOf(int playerId, string factionId) =>
            new FactionReputation(string.IsNullOrEmpty(factionId) ? "none" : factionId, Of(playerId, factionId)).Standing;

        public bool AtLeast(int playerId, string factionId, FactionStanding standing) => StandingOf(playerId, factionId) >= standing;

        /// <summary>
        /// Every faction the player has a reputation with, and its value.
        /// </summary>
        public IReadOnlyDictionary<string, int> AllOf(int playerId)
        {
            int owner = _world.ProgressOwnerOf(playerId);
            return owner < 0 ? new Dictionary<string, int>() : _manager.GetAllReputations(owner);
        }

        /// <summary>
        /// Changes the player's reputation with the faction, and with its allies and enemies; false for someone not in
        /// the party or an unknown or inactive faction.
        /// </summary>
        public bool Change(int playerId, string factionId, int delta)
        {
            int owner = _world.ProgressOwnerOf(playerId);
            var faction = string.IsNullOrEmpty(factionId) ? null : _registry.GetFaction(factionId);
            if (owner < 0 || faction == null || !faction.IsActive)
                return false;
            if (delta == 0)
                return true;
            var before = Snapshot(owner);
            _manager.ModifyReputation(owner, factionId, delta);
            Announce(owner, before);
            return true;
        }

        /// <summary>
        /// Sets the player's reputation with the faction (-100..100), without spreading to related factions.
        /// </summary>
        public bool Set(int playerId, string factionId, int value)
        {
            int owner = _world.ProgressOwnerOf(playerId);
            if (owner < 0 || string.IsNullOrEmpty(factionId) || !_registry.HasFaction(factionId))
                return false;
            var before = Snapshot(owner);
            _manager.SetReputation(owner, factionId, value);
            Announce(owner, before);
            return true;
        }

        internal List<ReputationRecord> RecordsOf(int playerId)
        {
            int owner = _world.ProgressOwnerOf(playerId);
            if (owner < 0)
                return new List<ReputationRecord>();
            return _manager.GetAllReputations(owner).OrderBy(r => r.Key, StringComparer.Ordinal)
                .Select(r => new ReputationRecord { FactionId = r.Key, Value = r.Value }).ToList();
        }

        internal void Restore(int playerId, IEnumerable<ReputationRecord> records)
        {
            int owner = _world.ProgressOwnerOf(playerId);
            if (owner < 0)
                return;
            foreach (var faction in _manager.GetAllReputations(owner).Keys.ToList())
                _manager.SetReputation(owner, faction, _registry.GetFaction(faction)?.DefaultReputation ?? 0);
            foreach (var record in records ?? Enumerable.Empty<ReputationRecord>())
            {
                if (!string.IsNullOrEmpty(record?.FactionId) && _registry.HasFaction(record.FactionId))
                    _manager.SetReputation(owner, record.FactionId, record.Value);
            }
        }

        internal void Register(TraderFaction trader)
        {
            if (trader != null)
                _traders[trader.TraderId] = trader;
        }

        /// <summary>
        /// The faction a trader belongs to (<see cref="ActionRpgContent.TraderFactions"/>); null for none.
        /// </summary>
        public TraderFaction TraderFactionOf(string traderId) =>
            !string.IsNullOrEmpty(traderId) && _traders.TryGetValue(traderId, out var trader) ? trader : null;

        /// <summary>
        /// Why a faction's trader will not deal with the player (their standing is below the trader's minimum), or null.
        /// </summary>
        public string WhyNotTrade(int playerId, string traderId)
        {
            var trader = TraderFactionOf(traderId);
            if (trader == null)
                return null;
            return StandingOf(playerId, trader.FactionId) < trader.MinimumStanding ? "They will not trade with you" : null;
        }

        /// <summary>
        /// A faction's trader's price for the player: each standing above Neutral takes
        /// <see cref="TraderFaction.PriceStep"/> off what they pay and adds it to what they get, each below adds it to
        /// what they pay and takes it off what they get. Gold as in an offer: negative is paid, positive is received.
        /// </summary>
        public long PriceFor(int playerId, string traderId, long gold)
        {
            var trader = TraderFactionOf(traderId);
            if (trader == null || gold == 0)
                return gold;
            int steps = (int)StandingOf(playerId, trader.FactionId) - (int)FactionStanding.Neutral;
            double factor = gold < 0 ? 1.0 - trader.PriceStep * steps : 1.0 + trader.PriceStep * steps;
            return (long)Math.Round(gold * Math.Max(0.0, factor), MidpointRounding.AwayFromZero);
        }

        private Dictionary<string, int> Snapshot(int owner) =>
            _registry.GetAllFactions().ToDictionary(f => f.FactionId, f => _manager.GetReputation(owner, f.FactionId));

        private void Announce(int owner, Dictionary<string, int> before)
        {
            if (_events == null)
                return;
            foreach (var faction in before.Keys.OrderBy(f => f, StringComparer.Ordinal))
            {
                int now = _manager.GetReputation(owner, faction);
                if (now != before[faction])
                    _events.Publish(new ReputationChangedEvent(owner, faction, before[faction], now, _manager.GetStanding(owner, faction)));
            }
        }
    }

    /// <summary>
    /// A trader who belongs to a faction (<see cref="ActionRpgContent.TraderFactions"/>): they refuse players whose
    /// standing is below <see cref="MinimumStanding"/>, and their prices move with the player's standing
    /// (<see cref="ActorReputation.PriceFor"/>).
    /// </summary>
    public sealed class TraderFaction
    {
        public TraderFaction(string traderId, string factionId, FactionStanding minimumStanding = FactionStanding.Unfriendly, float priceStep = 0.05f)
        {
            if (string.IsNullOrEmpty(traderId))
                throw new ArgumentException("A trader faction needs the trader's id.", nameof(traderId));
            if (string.IsNullOrEmpty(factionId))
                throw new ArgumentException("A trader faction needs the faction's id.", nameof(factionId));
            TraderId = traderId;
            FactionId = factionId;
            MinimumStanding = minimumStanding;
            PriceStep = Math.Max(0f, priceStep);
        }

        public string TraderId { get; }
        public string FactionId { get; }
        public FactionStanding MinimumStanding { get; }

        /// <summary>
        /// The share of the price each standing away from Neutral moves it (0.05: Honored pays 10% less).
        /// </summary>
        public float PriceStep { get; }
    }

    /// <summary>
    /// A faction reputation in a save.
    /// </summary>
    public sealed class ReputationRecord
    {
        public string FactionId { get; set; }
        public int Value { get; set; }
    }
}
