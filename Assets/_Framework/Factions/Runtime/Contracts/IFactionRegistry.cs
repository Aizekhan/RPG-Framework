using System.Collections.Generic;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Registry for all available factions in the game.
    /// Manages faction definitions and relationships between factions.
    /// </summary>
    public interface IFactionRegistry
    {
        /// <summary>
        /// Register a new faction to the registry.
        /// </summary>
        /// <param name="faction">Faction to register.</param>
        void RegisterFaction(IFaction faction);

        /// <summary>
        /// Get a faction by ID.
        /// </summary>
        /// <param name="factionId">Unique faction identifier.</param>
        /// <returns>The faction, or null if not found.</returns>
        IFaction GetFaction(string factionId);

        /// <summary>
        /// Check if a faction exists in the registry.
        /// </summary>
        /// <param name="factionId">Faction ID to check.</param>
        /// <returns>True if faction exists.</returns>
        bool HasFaction(string factionId);

        /// <summary>
        /// Get all registered factions.
        /// </summary>
        /// <returns>Read-only collection of all factions.</returns>
        IReadOnlyList<IFaction> GetAllFactions();

        /// <summary>
        /// Get all factions of a specific type.
        /// </summary>
        /// <param name="factionType">Type of factions to retrieve.</param>
        /// <returns>Factions matching the type.</returns>
        IReadOnlyList<IFaction> GetFactionsByType(FactionType factionType);

        /// <summary>
        /// Register a relationship between two factions.
        /// </summary>
        /// <param name="relationship">Faction relationship to register.</param>
        void RegisterRelationship(IFactionRelationship relationship);

        /// <summary>
        /// Get the relationship between two factions.
        /// </summary>
        /// <param name="factionId1">First faction ID.</param>
        /// <param name="factionId2">Second faction ID.</param>
        /// <returns>The relationship, or null if no explicit relationship exists (neutral).</returns>
        IFactionRelationship GetRelationship(string factionId1, string factionId2);

        /// <summary>
        /// Get all relationships involving a specific faction.
        /// </summary>
        /// <param name="factionId">Faction ID.</param>
        /// <returns>All relationships this faction is part of.</returns>
        IReadOnlyList<IFactionRelationship> GetRelationshipsForFaction(string factionId);
    }
}
