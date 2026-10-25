using System;
using System.Collections.Generic;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Guild treasury state data.
    /// Tracks shared resources and transaction history.
    /// </summary>
    [Serializable]
    public struct GuildTreasuryData
    {
        /// <summary>
        /// Guild ID this treasury belongs to.
        /// </summary>
        public string GuildId;

        /// <summary>
        /// Total gold in the treasury.
        /// </summary>
        public long Gold;

        /// <summary>
        /// Maximum treasury capacity (gold).
        /// </summary>
        public long MaxCapacity;

        /// <summary>
        /// Stored items (item ID → quantity).
        /// </summary>
        public Dictionary<string, int> StoredItems;

        /// <summary>
        /// Transaction history (limited to recent transactions).
        /// </summary>
        public List<TreasuryTransaction> Transactions;

        /// <summary>
        /// Create new treasury data.
        /// </summary>
        public static GuildTreasuryData Create(string guildId, long maxCapacity = 1000000)
        {
            return new GuildTreasuryData
            {
                GuildId = guildId,
                Gold = 0,
                MaxCapacity = maxCapacity,
                StoredItems = new Dictionary<string, int>(),
                Transactions = new List<TreasuryTransaction>()
            };
        }

        /// <summary>
        /// Check if treasury is at maximum capacity.
        /// </summary>
        public bool IsFull()
        {
            return Gold >= MaxCapacity;
        }

        /// <summary>
        /// Check if treasury has sufficient gold.
        /// </summary>
        public bool HasGold(long amount)
        {
            return Gold >= amount;
        }

        /// <summary>
        /// Check if treasury has sufficient quantity of an item.
        /// </summary>
        public bool HasItem(string itemId, int quantity)
        {
            if (StoredItems == null)
                return false;

            return StoredItems.TryGetValue(itemId, out int stored) && stored >= quantity;
        }

        /// <summary>
        /// Add a transaction to the history (keeps last 100).
        /// </summary>
        public void RecordTransaction(TreasuryTransaction transaction)
        {
            if (Transactions == null)
                Transactions = new List<TreasuryTransaction>();

            Transactions.Add(transaction);

            // Keep only last 100 transactions
            if (Transactions.Count > 100)
                Transactions.RemoveAt(0);
        }
    }
}
