namespace RPGFramework.Factions
{
    /// <summary>
    /// Represents a faction - an organization, group, or entity that players can build reputation with.
    /// Examples: Guilds, Kingdoms, Clans, Religions, Criminal Organizations.
    /// </summary>
    public interface IFaction
    {
        /// <summary>
        /// Unique identifier for this faction.
        /// </summary>
        string FactionId { get; }

        /// <summary>
        /// Display name of the faction.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the faction's purpose, beliefs, or goals.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Type/category of this faction (e.g., Guild, Kingdom, Clan).
        /// </summary>
        FactionType FactionType { get; }

        /// <summary>
        /// Base reputation value for new entities (default starting reputation).
        /// Typically 0 (neutral), but some factions might start hostile (-50) or friendly (+25).
        /// </summary>
        int DefaultReputation { get; }

        /// <summary>
        /// Whether this faction is currently active/available in the game.
        /// Inactive factions might be disabled temporarily or unlocked later.
        /// </summary>
        bool IsActive { get; }
    }
}
