using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Concrete implementation of IGuildTreasury.
    /// Manages shared guild resources and transaction tracking.
    /// </summary>
    public class GuildTreasury : IGuildTreasury
    {
        private GuildTreasuryData _data;
        private readonly IEventPublisher _events;

        public string GuildId => _data.GuildId;
        public long Gold => _data.Gold;
        public long MaxCapacity => _data.MaxCapacity;

        /// <summary>
        /// Create a new guild treasury.
        /// </summary>
        /// <param name="events">Optional publisher for <see cref="TreasuryDepositEvent"/> and <see cref="TreasuryWithdrawEvent"/>.</param>
        public GuildTreasury(GuildTreasuryData data, IEventPublisher events = null)
        {
            _data = data;
            _events = events;
        }

        public bool IsFull()
        {
            return _data.IsFull();
        }

        public bool DepositGold(long amount, int depositorId)
        {
            if (amount <= 0)
                return false;

            if (_data.Gold + amount > _data.MaxCapacity)
                return false;

            _data.Gold += amount;

            // Record transaction
            _data.RecordTransaction(new TreasuryTransaction
            {
                Type = TreasuryTransactionType.GoldDeposit,
                EntityId = depositorId,
                GoldAmount = amount,
                ItemId = null,
                ItemQuantity = 0,
                Timestamp = DateTime.UtcNow
            });

            _events?.Publish(TreasuryDepositEvent.GoldDeposit(_data.GuildId, depositorId, amount));
            return true;
        }

        public bool WithdrawGold(long amount, int withdrawerId)
        {
            if (amount <= 0)
                return false;

            if (_data.Gold < amount)
                return false;

            _data.Gold -= amount;

            // Record transaction
            _data.RecordTransaction(new TreasuryTransaction
            {
                Type = TreasuryTransactionType.GoldWithdraw,
                EntityId = withdrawerId,
                GoldAmount = amount,
                ItemId = null,
                ItemQuantity = 0,
                Timestamp = DateTime.UtcNow
            });

            _events?.Publish(TreasuryWithdrawEvent.GoldWithdraw(_data.GuildId, withdrawerId, amount));
            return true;
        }

        public IReadOnlyDictionary<string, int> GetStoredItems()
        {
            return _data.StoredItems ?? new Dictionary<string, int>();
        }

        public bool DepositItem(string itemId, int quantity, int depositorId)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return false;

            if (_data.StoredItems == null)
                _data.StoredItems = new Dictionary<string, int>();

            if (_data.StoredItems.ContainsKey(itemId))
            {
                _data.StoredItems[itemId] += quantity;
            }
            else
            {
                _data.StoredItems[itemId] = quantity;
            }

            // Record transaction
            _data.RecordTransaction(new TreasuryTransaction
            {
                Type = TreasuryTransactionType.ItemDeposit,
                EntityId = depositorId,
                GoldAmount = 0,
                ItemId = itemId,
                ItemQuantity = quantity,
                Timestamp = DateTime.UtcNow
            });

            _events?.Publish(TreasuryDepositEvent.ItemDeposit(_data.GuildId, depositorId, itemId, quantity));
            return true;
        }

        public bool WithdrawItem(string itemId, int quantity, int withdrawerId)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return false;

            if (!_data.HasItem(itemId, quantity))
                return false;

            _data.StoredItems[itemId] -= quantity;

            // Remove item entry if quantity reaches 0
            if (_data.StoredItems[itemId] <= 0)
            {
                _data.StoredItems.Remove(itemId);
            }

            // Record transaction
            _data.RecordTransaction(new TreasuryTransaction
            {
                Type = TreasuryTransactionType.ItemWithdraw,
                EntityId = withdrawerId,
                GoldAmount = 0,
                ItemId = itemId,
                ItemQuantity = quantity,
                Timestamp = DateTime.UtcNow
            });

            _events?.Publish(TreasuryWithdrawEvent.ItemWithdraw(_data.GuildId, withdrawerId, itemId, quantity));
            return true;
        }

        public bool HasGold(long amount)
        {
            return _data.HasGold(amount);
        }

        public bool HasItem(string itemId, int quantity)
        {
            return _data.HasItem(itemId, quantity);
        }

        public IReadOnlyList<TreasuryTransaction> GetTransactionHistory(int limit = 50)
        {
            if (_data.Transactions == null || _data.Transactions.Count == 0)
                return new List<TreasuryTransaction>();

            return _data.Transactions
                .OrderByDescending(t => t.Timestamp)
                .Take(limit)
                .ToList();
        }

        /// <summary>
        /// Get the internal treasury data.
        /// Internal method for persistence.
        /// </summary>
        internal GuildTreasuryData GetData()
        {
            return _data;
        }
    }
}
