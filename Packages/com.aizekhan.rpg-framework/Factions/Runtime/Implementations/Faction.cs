using System;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Concrete implementation of IFaction.
    /// Represents a faction with basic properties and configuration.
    /// </summary>
    public class Faction : IFaction
    {
        public string FactionId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public FactionType FactionType { get; private set; }
        public int DefaultReputation { get; private set; }
        public bool IsActive { get; private set; }

        /// <summary>
        /// Create a new faction.
        /// </summary>
        public Faction(
            string factionId,
            string name,
            string description,
            FactionType factionType,
            int defaultReputation = 0,
            bool isActive = true)
        {
            if (string.IsNullOrEmpty(factionId))
                throw new ArgumentException("Faction ID cannot be null or empty", nameof(factionId));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Faction name cannot be null or empty", nameof(name));

            FactionId = factionId;
            Name = name;
            Description = description ?? string.Empty;
            FactionType = factionType;
            DefaultReputation = Math.Clamp(defaultReputation, -100, 100);
            IsActive = isActive;
        }

        /// <summary>
        /// Activate this faction (make it available).
        /// </summary>
        public void Activate()
        {
            IsActive = true;
        }

        /// <summary>
        /// Deactivate this faction (make it unavailable).
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// Factory method to create a faction with default values.
        /// </summary>
        public static Faction Create(
            string factionId,
            string name,
            FactionType factionType,
            string description = "")
        {
            return new Faction(factionId, name, description, factionType);
        }

        public override string ToString()
        {
            return $"Faction[{FactionId}]: {Name} ({FactionType}) - Default Rep: {DefaultReputation}";
        }
    }
}
