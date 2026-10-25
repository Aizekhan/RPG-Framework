using System.Collections.Generic;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Manages shared guild resources and treasury.
    /// Handles deposits, withdrawals, and resource tracking.
    /// </summary>
    public interface IGuildTreasury
    {
        /// <summary>
        /// Guild ID this treasury belongs to.
        /// </summary>
        string GuildId { get; }

        /// <summary>
        /// Total gold in the treasury.
        /// </summary>
        long Gold { get; }

        /// <summary>
        /// Maximum treasury capacity.
        /// </summary>
        long MaxCapacity { get; }

        /// <summary>
        /// Check if treasury is at maximum capacity.
        /// </summary>
        /// <returns>True if treasury is full.</returns>
        bool IsFull();

        /// <summary>
        /// Deposit gold into the treasury.
        /// </summary>
        /// <param name="amount">Amount to deposit.</param>
        /// <param name="depositorId">Entity making the deposit.</param>
        /// <returns>True if deposit was successful.</returns>
        bool DepositGold(long amount, int depositorId);

        /// <summary>
        /// Withdraw gold from the treasury.
        /// </summary>
        /// <param name="amount">Amount to withdraw.</param>
        /// <param name="withdrawerId">Entity making the withdrawal.</param>
        /// <returns>True if withdrawal was successful.</returns>
        bool WithdrawGold(long amount, int withdrawerId);

        /// <summary>
        /// Get all items stored in the treasury.
        /// </summary>
        /// <returns>Dictionary of item ID → quantity.</returns>
        IReadOnlyDictionary<string, int> GetStoredItems();

        /// <summary>
        /// Deposit an item into the treasury.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="quantity">Quantity to deposit.</param>
        /// <param name="depositorId">Entity making the deposit.</param>
        /// <returns>True if deposit was successful.</returns>
        bool DepositItem(string itemId, int quantity, int depositorId);

        /// <summary>
        /// Withdraw an item from the treasury.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="quantity">Quantity to withdraw.</param>
        /// <param name="withdrawerId">Entity making the withdrawal.</param>
        /// <returns>True if withdrawal was successful.</returns>
        bool WithdrawItem(string itemId, int quantity, int withdrawerId);

        /// <summary>
        /// Check if treasury has sufficient gold.
        /// </summary>
        /// <param name="amount">Amount to check.</param>
        /// <returns>True if treasury has the amount.</returns>
        bool HasGold(long amount);

        /// <summary>
        /// Check if treasury has sufficient quantity of an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="quantity">Quantity to check.</param>
        /// <returns>True if treasury has the item in sufficient quantity.</returns>
        bool HasItem(string itemId, int quantity);

        /// <summary>
        /// Get transaction history for the treasury.
        /// </summary>
        /// <param name="limit">Maximum number of recent transactions to return.</param>
        /// <returns>List of recent transactions.</returns>
        IReadOnlyList<TreasuryTransaction> GetTransactionHistory(int limit = 50);
    }

    /// <summary>
    /// Represents a single treasury transaction.
    /// </summary>
    public struct TreasuryTransaction
    {
        /// <summary>
        /// Type of transaction.
        /// </summary>
        public TreasuryTransactionType Type { get; set; }

        /// <summary>
        /// Entity that performed the transaction.
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// Amount of gold involved (0 if item transaction).
        /// </summary>
        public long GoldAmount { get; set; }

        /// <summary>
        /// Item ID involved (null if gold transaction).
        /// </summary>
        public string ItemId { get; set; }

        /// <summary>
        /// Item quantity involved (0 if gold transaction).
        /// </summary>
        public int ItemQuantity { get; set; }

        /// <summary>
        /// Timestamp of the transaction.
        /// </summary>
        public System.DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Types of treasury transactions.
    /// </summary>
    public enum TreasuryTransactionType
    {
        /// <summary>
        /// Gold deposited into treasury.
        /// </summary>
        GoldDeposit = 0,

        /// <summary>
        /// Gold withdrawn from treasury.
        /// </summary>
        GoldWithdraw = 1,

        /// <summary>
        /// Item deposited into treasury.
        /// </summary>
        ItemDeposit = 2,

        /// <summary>
        /// Item withdrawn from treasury.
        /// </summary>
        ItemWithdraw = 3
    }
}
