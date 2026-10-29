using System;
using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Concrete implementation of IFactionManager.
    /// Manages entity reputations with factions, including relationship cascades.
    /// Integrates with ECS for persistent storage.
    /// </summary>
    public class FactionManager : IFactionManager
    {
        private readonly IEntityManager _entityManager;
        private readonly IFactionRegistry _registry;

        public FactionManager(IEntityManager entityManager, IFactionRegistry registry)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int GetReputation(int entityId, string factionId)
        {
            var faction = _registry.GetFaction(factionId);
            if (faction == null)
                return 0;

            if (!_entityManager.TryGetComponent<FactionReputationData>(entityId, out var data) || !data.HasReputation(factionId))
                return faction.DefaultReputation;

            return data.GetReputation(factionId);
        }

        public FactionStanding GetStanding(int entityId, string factionId)
        {
            int reputation = GetReputation(entityId, factionId);
            var factionRep = new FactionReputation(factionId, reputation);
            return factionRep.Standing;
        }

        public void ModifyReputation(int entityId, string factionId, int delta)
        {
            if (delta == 0)
                return;

            var faction = _registry.GetFaction(factionId);
            if (faction == null || !faction.IsActive)
                return;

            // Get or create reputation data component
            FactionReputationData data;
            if (!_entityManager.TryGetComponent<FactionReputationData>(entityId, out data))
            {
                data = FactionReputationData.Create();
                _entityManager.AddComponent(entityId, data);
            }

            // Modify reputation for target faction, from its default when the entity never dealt with it
            data.SetReputation(factionId, CurrentOf(data, faction) + delta);

            // Apply relationship cascades
            ApplyRelationshipCascades(entityId, factionId, delta, data);

            // Update component
            _entityManager.AddComponent(entityId, data);
        }

        public void SetReputation(int entityId, string factionId, int value)
        {
            var faction = _registry.GetFaction(factionId);
            if (faction == null)
                return;

            // Get or create reputation data component
            FactionReputationData data;
            if (!_entityManager.TryGetComponent<FactionReputationData>(entityId, out data))
            {
                data = FactionReputationData.Create();
            }

            data.SetReputation(factionId, value);
            _entityManager.AddComponent(entityId, data);
        }

        public IReadOnlyDictionary<string, int> GetAllReputations(int entityId)
        {
            if (!_entityManager.TryGetComponent<FactionReputationData>(entityId, out var data))
                return new Dictionary<string, int>();

            return new Dictionary<string, int>(data.Reputations);
        }

        public bool HasMinimumStanding(int entityId, string factionId, FactionStanding minimumStanding)
        {
            var currentStanding = GetStanding(entityId, factionId);
            return currentStanding >= minimumStanding;
        }

        public void InitializeEntityReputations(int entityId)
        {
            var data = FactionReputationData.Create();
            var allFactions = _registry.GetAllFactions();

            foreach (var faction in allFactions)
            {
                if (faction.IsActive)
                {
                    data.SetReputation(faction.FactionId, faction.DefaultReputation);
                }
            }

            _entityManager.AddComponent(entityId, data);
        }

        private static int CurrentOf(FactionReputationData data, IFaction faction) =>
            data.HasReputation(faction.FactionId) ? data.GetReputation(faction.FactionId) : faction.DefaultReputation;

        /// <summary>
        /// Apply reputation changes to related factions based on relationships.
        /// </summary>
        private void ApplyRelationshipCascades(
            int entityId,
            string sourceFactionId,
            int delta,
            FactionReputationData data)
        {
            var relationships = _registry.GetRelationshipsForFaction(sourceFactionId);

            foreach (var relationship in relationships)
            {
                string otherFactionId = relationship.GetOtherFaction(sourceFactionId);
                if (otherFactionId == null)
                    continue;

                var otherFaction = _registry.GetFaction(otherFactionId);
                if (otherFaction == null || !otherFaction.IsActive)
                    continue;

                // Calculate cascaded reputation change
                int cascadeDelta = (int)(delta * relationship.ReputationModifier);
                if (cascadeDelta != 0)
                {
                    data.SetReputation(otherFactionId, CurrentOf(data, otherFaction) + cascadeDelta);
                }
            }
        }
    }
}
