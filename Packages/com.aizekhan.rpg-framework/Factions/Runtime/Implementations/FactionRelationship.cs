using System;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Concrete implementation of IFactionRelationship.
    /// Defines how two factions relate to each other and how reputation changes cascade.
    /// </summary>
    public class FactionRelationship : IFactionRelationship
    {
        public string FactionId1 { get; private set; }
        public string FactionId2 { get; private set; }
        public FactionRelationType RelationType { get; private set; }
        public float ReputationModifier { get; private set; }

        /// <summary>
        /// Create a new faction relationship.
        /// </summary>
        public FactionRelationship(
            string factionId1,
            string factionId2,
            FactionRelationType relationType,
            float reputationModifier = 0f)
        {
            if (string.IsNullOrEmpty(factionId1))
                throw new ArgumentException("Faction ID 1 cannot be null or empty", nameof(factionId1));
            if (string.IsNullOrEmpty(factionId2))
                throw new ArgumentException("Faction ID 2 cannot be null or empty", nameof(factionId2));
            if (factionId1 == factionId2)
                throw new ArgumentException("Cannot create relationship between same faction");

            FactionId1 = factionId1;
            FactionId2 = factionId2;
            RelationType = relationType;
            ReputationModifier = Math.Clamp(reputationModifier, -1f, 1f);
        }

        public bool Involves(string factionId)
        {
            return FactionId1 == factionId || FactionId2 == factionId;
        }

        [System.Obsolete("Misspelt; use Involves")]
        public bool InvolvesFunction(string factionId) => Involves(factionId);

        public string GetOtherFaction(string knownFactionId)
        {
            if (FactionId1 == knownFactionId)
                return FactionId2;
            if (FactionId2 == knownFactionId)
                return FactionId1;
            return null;
        }

        /// <summary>
        /// Factory method to create a relationship with default modifier based on type.
        /// </summary>
        public static FactionRelationship Create(
            string factionId1,
            string factionId2,
            FactionRelationType relationType)
        {
            float modifier = relationType switch
            {
                FactionRelationType.Allied => 0.5f,
                FactionRelationType.Enemy => -0.75f,
                FactionRelationType.Rival => -0.35f,
                FactionRelationType.TradePartner => 0.25f,
                _ => 0f
            };

            return new FactionRelationship(factionId1, factionId2, relationType, modifier);
        }

        public override string ToString()
        {
            return $"Relationship[{FactionId1} ↔ {FactionId2}]: {RelationType} (modifier: {ReputationModifier:F2})";
        }
    }
}
