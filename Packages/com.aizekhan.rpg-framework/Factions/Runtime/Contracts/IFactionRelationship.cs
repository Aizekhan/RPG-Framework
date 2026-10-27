namespace RPGFramework.Factions
{
    /// <summary>
    /// Represents the relationship between two factions.
    /// Defines how factions interact with each other (allied, at war, neutral).
    /// </summary>
    public interface IFactionRelationship
    {
        /// <summary>
        /// ID of the first faction in this relationship.
        /// </summary>
        string FactionId1 { get; }

        /// <summary>
        /// ID of the second faction in this relationship.
        /// </summary>
        string FactionId2 { get; }

        /// <summary>
        /// Type of relationship between the two factions.
        /// </summary>
        FactionRelationType RelationType { get; }

        /// <summary>
        /// Reputation modifier applied when gaining reputation with one faction
        /// affects reputation with the related faction.
        ///
        /// Examples:
        /// - Allied factions: +0.5 (gaining 100 rep with faction A gives +50 with allied faction B)
        /// - Enemy factions: -0.75 (gaining 100 rep with faction A loses -75 with enemy faction B)
        /// - Neutral: 0.0 (no effect)
        /// </summary>
        float ReputationModifier { get; }

        /// <summary>
        /// Check if this relationship involves a specific faction.
        /// </summary>
        /// <param name="factionId">Faction ID to check.</param>
        /// <returns>True if this faction is part of the relationship.</returns>
        bool Involves(string factionId);

        /// <summary>
        /// Get the other faction ID in this relationship.
        /// </summary>
        /// <param name="knownFactionId">One of the faction IDs.</param>
        /// <returns>The other faction ID, or null if knownFactionId is not part of this relationship.</returns>
        string GetOtherFaction(string knownFactionId);
    }
}
