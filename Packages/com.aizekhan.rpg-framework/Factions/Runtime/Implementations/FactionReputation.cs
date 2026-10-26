using System;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Concrete implementation of IFactionReputation.
    /// Manages an entity's reputation value with a specific faction and calculates standing.
    /// </summary>
    public class FactionReputation : IFactionReputation
    {
        public string FactionId { get; private set; }
        public int Reputation { get; private set; }
        public FactionStanding Standing => CalculateStanding(Reputation);

        /// <summary>
        /// Create a new faction reputation instance.
        /// </summary>
        public FactionReputation(string factionId, int initialReputation = 0)
        {
            if (string.IsNullOrEmpty(factionId))
                throw new ArgumentException("Faction ID cannot be null or empty", nameof(factionId));

            FactionId = factionId;
            Reputation = Math.Clamp(initialReputation, -100, 100);
        }

        public void ModifyReputation(int delta)
        {
            Reputation = Math.Clamp(Reputation + delta, -100, 100);
        }

        public void SetReputation(int value)
        {
            Reputation = Math.Clamp(value, -100, 100);
        }

        public FactionStanding CalculateStanding(int reputationValue)
        {
            // Hated: -100 to -75
            if (reputationValue <= -75)
                return FactionStanding.Hated;

            // Hostile: -74 to -50
            if (reputationValue <= -50)
                return FactionStanding.Hostile;

            // Unfriendly: -49 to -25
            if (reputationValue <= -25)
                return FactionStanding.Unfriendly;

            // Friendly: +25 to +49
            if (reputationValue >= 75)
                return FactionStanding.Revered;

            // Honored: +50 to +74
            if (reputationValue >= 50)
                return FactionStanding.Honored;

            // Friendly: +25 to +49
            if (reputationValue >= 25)
                return FactionStanding.Friendly;

            // Neutral: -24 to +24 (default)
            return FactionStanding.Neutral;
        }

        public override string ToString()
        {
            return $"FactionReputation[{FactionId}]: {Reputation} ({Standing})";
        }
    }
}
