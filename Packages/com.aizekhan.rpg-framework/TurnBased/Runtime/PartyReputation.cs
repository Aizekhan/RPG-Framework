using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Events;
using RPGFramework.Factions;

namespace RPGFramework.TurnBased
{
    /// <summary>
    /// The party's reputation with a faction changed (a beaten enemy, a quest).
    /// </summary>
    public readonly struct PartyReputationChangedEvent : IEvent
    {
        public PartyReputationChangedEvent(string factionId, int previous, int current, FactionStanding standing)
        {
            FactionId = factionId;
            Previous = previous;
            Current = current;
            Standing = standing;
        }

        public string FactionId { get; }
        public int Previous { get; }
        public int Current { get; }
        public FactionStanding Standing { get; }
    }

    /// <summary>
    /// The party's reputation with the game's factions (<see cref="TurnBasedContent.Factions"/>), -100..100 with its
    /// standing (Hated .. Exalted), kept on one entity for the whole party. A change spreads to related factions
    /// (<see cref="TurnBasedContent.FactionRelations"/>). Beating an enemy with a faction
    /// (<see cref="CombatantData.Faction"/>) changes it by <see cref="CombatantData.ReputationOnKill"/>; a quest's
    /// Custom reward "rep:&lt;faction&gt;" by its amount. Part of the save; a new run starts from the defaults.
    /// </summary>
    public sealed class PartyReputation
    {
        /// <summary>
        /// The prefix of a quest reward id that means a faction's reputation ("rep:guards").
        /// </summary>
        public const string Prefix = "rep:";

        private readonly IEntityManager _entities;
        private readonly IEventPublisher _events;
        private readonly FactionRegistry _registry = new FactionRegistry();
        private readonly FactionManager _manager;

        internal PartyReputation(IEntityManager entities, IEventPublisher events, TurnBasedContent content)
        {
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _events = events;
            _manager = new FactionManager(entities, _registry);
            foreach (var faction in content.Factions.Where(f => f != null && !_registry.HasFaction(f.FactionId)))
                _registry.RegisterFaction(faction);
            foreach (var relation in content.FactionRelations.Where(r => r != null))
                _registry.RegisterRelationship(relation);
        }

        public IFactionRegistry Factions => _registry;

        /// <summary>
        /// The entity the party's reputation is kept on; -1 before the run starts.
        /// </summary>
        public int HolderId { get; private set; } = -1;

        internal void Start()
        {
            if (HolderId < 0)
                HolderId = _entities.CreateEntity();
        }

        /// <summary>
        /// The party's reputation with the faction (its default when they never dealt with it); 0 for an unknown one.
        /// </summary>
        public int Of(string factionId) => HolderId < 0 || string.IsNullOrEmpty(factionId) ? 0 : _manager.GetReputation(HolderId, factionId);

        public FactionStanding StandingOf(string factionId) =>
            new FactionReputation(string.IsNullOrEmpty(factionId) ? "none" : factionId, Of(factionId)).Standing;

        public bool AtLeast(string factionId, FactionStanding standing) => StandingOf(factionId) >= standing;

        /// <summary>
        /// Changes the party's reputation with the faction, and with its allies and enemies; false before the run
        /// starts or for an unknown or inactive faction.
        /// </summary>
        public bool Change(string factionId, int delta)
        {
            var faction = string.IsNullOrEmpty(factionId) ? null : _registry.GetFaction(factionId);
            if (HolderId < 0 || faction == null || !faction.IsActive)
                return false;
            if (delta == 0)
                return true;
            var before = Snapshot();
            _manager.ModifyReputation(HolderId, factionId, delta);
            Announce(before);
            return true;
        }

        /// <summary>
        /// Sets the party's reputation with the faction (-100..100), without spreading to related factions.
        /// </summary>
        public bool Set(string factionId, int value)
        {
            if (HolderId < 0 || string.IsNullOrEmpty(factionId) || !_registry.HasFaction(factionId))
                return false;
            var before = Snapshot();
            _manager.SetReputation(HolderId, factionId, value);
            Announce(before);
            return true;
        }

        internal List<PartyReputationRecord> Records() =>
            HolderId < 0
                ? new List<PartyReputationRecord>()
                : _manager.GetAllReputations(HolderId).OrderBy(r => r.Key, StringComparer.Ordinal)
                    .Select(r => new PartyReputationRecord { FactionId = r.Key, Value = r.Value }).ToList();

        internal void Restore(IEnumerable<PartyReputationRecord> records)
        {
            if (HolderId < 0)
                return;
            if (_entities.HasComponent<FactionReputationData>(HolderId))
                _entities.RemoveComponent<FactionReputationData>(HolderId);
            foreach (var record in records ?? Enumerable.Empty<PartyReputationRecord>())
            {
                if (!string.IsNullOrEmpty(record?.FactionId) && _registry.HasFaction(record.FactionId))
                    _manager.SetReputation(HolderId, record.FactionId, record.Value);
            }
        }

        private Dictionary<string, int> Snapshot() =>
            _registry.GetAllFactions().ToDictionary(f => f.FactionId, f => _manager.GetReputation(HolderId, f.FactionId));

        private void Announce(Dictionary<string, int> before)
        {
            if (_events == null)
                return;
            foreach (var faction in before.Keys.OrderBy(f => f, StringComparer.Ordinal))
            {
                int now = _manager.GetReputation(HolderId, faction);
                if (now != before[faction])
                    _events.Publish(new PartyReputationChangedEvent(faction, before[faction], now, _manager.GetStanding(HolderId, faction)));
            }
        }
    }

    /// <summary>
    /// A faction reputation in a save.
    /// </summary>
    public sealed class PartyReputationRecord
    {
        public string FactionId { get; set; }
        public int Value { get; set; }
    }
}
