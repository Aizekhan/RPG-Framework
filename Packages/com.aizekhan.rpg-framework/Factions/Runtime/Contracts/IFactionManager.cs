using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Factions
{
    /// <summary>
    /// Main manager for the Faction system.
    /// Handles entity reputation with factions, reputation changes, and relationship effects.
    /// Integrates with ECS for entity-specific faction data.
    /// </summary>
    public interface IFactionManager
    {
        /// <summary>
        /// Get an entity's reputation with a specific faction.
        /// Returns default reputation if entity has no established reputation yet.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <param name="factionId">Faction to check reputation with.</param>
        /// <returns>Current reputation value (-100 to +100).</returns>
        int GetReputation(int entityId, string factionId);

        /// <summary>
        /// Get an entity's standing with a specific faction.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <param name="factionId">Faction to check standing with.</param>
        /// <returns>Current faction standing.</returns>
        FactionStanding GetStanding(int entityId, string factionId);

        /// <summary>
        /// Modify an entity's reputation with a faction.
        /// Automatically applies relationship modifiers to related factions.
        /// </summary>
        /// <param name="entityId">Entity whose reputation changes.</param>
        /// <param name="factionId">Faction to modify reputation with.</param>
        /// <param name="delta">Amount to change (positive or negative).</param>
        void ModifyReputation(int entityId, string factionId, int delta);

        /// <summary>
        /// Set an entity's reputation with a faction to a specific value.
        /// Does NOT trigger relationship cascades.
        /// </summary>
        /// <param name="entityId">Entity whose reputation to set.</param>
        /// <param name="factionId">Faction to set reputation with.</param>
        /// <param name="value">New reputation value.</param>
        void SetReputation(int entityId, string factionId, int value);

        /// <summary>
        /// Get all factions an entity has established reputation with.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <returns>Dictionary of faction ID → reputation value.</returns>
        IReadOnlyDictionary<string, int> GetAllReputations(int entityId);

        /// <summary>
        /// Check if an entity has at least a certain standing with a faction.
        /// Useful for quest/dialogue requirements.
        /// </summary>
        /// <param name="entityId">Entity to check.</param>
        /// <param name="factionId">Faction to check.</param>
        /// <param name="minimumStanding">Minimum required standing.</param>
        /// <returns>True if entity meets or exceeds the standing requirement.</returns>
        bool HasMinimumStanding(int entityId, string factionId, FactionStanding minimumStanding);

        /// <summary>
        /// Initialize faction reputation for a new entity.
        /// Sets default reputations for all active factions.
        /// </summary>
        /// <param name="entityId">Entity to initialize.</param>
        void InitializeEntityReputations(int entityId);
    }
}
