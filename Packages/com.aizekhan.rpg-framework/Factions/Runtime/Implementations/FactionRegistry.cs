using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Concrete implementation of IFactionRegistry.
    /// Manages all factions and their relationships in the game.
    /// </summary>
    public class FactionRegistry : IFactionRegistry
    {
        private readonly Dictionary<string, IFaction> _factions;
        private readonly List<IFactionRelationship> _relationships;

        public FactionRegistry()
        {
            _factions = new Dictionary<string, IFaction>();
            _relationships = new List<IFactionRelationship>();
        }

        public void RegisterFaction(IFaction faction)
        {
            if (faction == null)
                throw new ArgumentNullException(nameof(faction));

            if (_factions.ContainsKey(faction.FactionId))
                throw new InvalidOperationException($"Faction '{faction.FactionId}' is already registered");

            _factions[faction.FactionId] = faction;
        }

        public IFaction GetFaction(string factionId)
        {
            return _factions.TryGetValue(factionId, out var faction) ? faction : null;
        }

        public bool HasFaction(string factionId)
        {
            return _factions.ContainsKey(factionId);
        }

        public IReadOnlyList<IFaction> GetAllFactions()
        {
            return _factions.Values.ToList();
        }

        public IReadOnlyList<IFaction> GetFactionsByType(FactionType factionType)
        {
            return _factions.Values
                .Where(f => f.FactionType == factionType)
                .ToList();
        }

        public void RegisterRelationship(IFactionRelationship relationship)
        {
            if (relationship == null)
                throw new ArgumentNullException(nameof(relationship));

            if (!HasFaction(relationship.FactionId1))
                throw new InvalidOperationException($"Faction '{relationship.FactionId1}' not found in registry");
            if (!HasFaction(relationship.FactionId2))
                throw new InvalidOperationException($"Faction '{relationship.FactionId2}' not found in registry");

            // Remove existing relationship if any
            var existing = GetRelationship(relationship.FactionId1, relationship.FactionId2);
            if (existing != null)
                _relationships.Remove(existing);

            _relationships.Add(relationship);
        }

        public IFactionRelationship GetRelationship(string factionId1, string factionId2)
        {
            return _relationships.FirstOrDefault(r =>
                (r.FactionId1 == factionId1 && r.FactionId2 == factionId2) ||
                (r.FactionId1 == factionId2 && r.FactionId2 == factionId1));
        }

        public IReadOnlyList<IFactionRelationship> GetRelationshipsForFaction(string factionId)
        {
            return _relationships
                .Where(r => r.Involves(factionId))
                .ToList();
        }
    }
}
