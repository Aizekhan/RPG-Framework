using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Loot
{
    /// <summary>
    /// Default implementation of ILootTableRegistry.
    /// Thread-safe singleton or can be injected via DI.
    /// </summary>
    public class LootTableRegistry : ILootTableRegistry
    {
        private readonly Dictionary<string, ILootTable> _tables;

        /// <summary>
        /// Create a new loot table registry.
        /// </summary>
        public LootTableRegistry()
        {
            _tables = new Dictionary<string, ILootTable>();
        }

        /// <inheritdoc/>
        public ILootTable GetTable(string tableId)
        {
            return _tables.TryGetValue(tableId, out var table) ? table : null;
        }

        /// <inheritdoc/>
        public bool HasTable(string tableId)
        {
            return _tables.ContainsKey(tableId);
        }

        /// <inheritdoc/>
        public void RegisterTable(ILootTable table)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrEmpty(table.TableId))
                throw new ArgumentException("Table ID cannot be null or empty", nameof(table));

            _tables[table.TableId] = table;
        }

        /// <inheritdoc/>
        public bool UnregisterTable(string tableId)
        {
            return _tables.Remove(tableId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<ILootTable> GetAllTables()
        {
            return _tables.Values.ToList();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _tables.Clear();
        }
    }
}
