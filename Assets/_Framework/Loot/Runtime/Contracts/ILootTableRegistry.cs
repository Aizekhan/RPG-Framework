using System.Collections.Generic;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Registry for storing and retrieving loot tables.
    /// Acts as single source of truth for all loot tables in the game.
    /// </summary>
    public interface ILootTableRegistry
    {
        /// <summary>
        /// Get loot table by ID.
        /// </summary>
        /// <param name="tableId">Unique table identifier</param>
        /// <returns>Loot table, or null if not found</returns>
        ILootTable GetTable(string tableId);

        /// <summary>
        /// Check if table exists in registry.
        /// </summary>
        /// <param name="tableId">Table ID to check</param>
        /// <returns>True if registered, false otherwise</returns>
        bool HasTable(string tableId);

        /// <summary>
        /// Register a loot table.
        /// Replaces existing table if same TableId already registered.
        /// </summary>
        /// <param name="table">Loot table to register</param>
        void RegisterTable(ILootTable table);

        /// <summary>
        /// Unregister a loot table.
        /// </summary>
        /// <param name="tableId">Table ID to remove</param>
        /// <returns>True if removed, false if not found</returns>
        bool UnregisterTable(string tableId);

        /// <summary>
        /// Get all registered tables.
        /// </summary>
        /// <returns>Read-only list of all loot tables</returns>
        IReadOnlyList<ILootTable> GetAllTables();

        /// <summary>
        /// Clear all registered tables.
        /// Use with caution - typically only needed for testing or full reloads.
        /// </summary>
        void Clear();
    }
}
