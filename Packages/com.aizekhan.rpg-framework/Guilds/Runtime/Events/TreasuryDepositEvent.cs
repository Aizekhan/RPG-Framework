using System;
using RPGFramework.Events;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when resources are deposited into the guild treasury.
    /// Published by <see cref="GuildTreasury"/> (DepositGold / DepositItem) when it was given an event publisher
    /// and the deposit succeeded.
    /// </summary>
    public class TreasuryDepositEvent : IEvent
    {
        /// <summary>
        /// Guild ID.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Entity ID of the member who made the deposit.
        /// </summary>
        public int DepositorId { get; }

        /// <summary>
        /// Amount of gold deposited (0 if item deposit).
        /// </summary>
        public long GoldAmount { get; }

        /// <summary>
        /// Item ID deposited (null if gold deposit).
        /// </summary>
        public string ItemId { get; }

        /// <summary>
        /// Item quantity deposited (0 if gold deposit).
        /// </summary>
        public int ItemQuantity { get; }

        /// <summary>
        /// Timestamp of the deposit.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a gold deposit event.
        /// </summary>
        public static TreasuryDepositEvent GoldDeposit(string guildId, int depositorId, long amount)
        {
            return new TreasuryDepositEvent(guildId, depositorId, amount, null, 0);
        }

        /// <summary>
        /// Create an item deposit event.
        /// </summary>
        public static TreasuryDepositEvent ItemDeposit(string guildId, int depositorId, string itemId, int quantity)
        {
            return new TreasuryDepositEvent(guildId, depositorId, 0, itemId, quantity);
        }

        private TreasuryDepositEvent(
            string guildId,
            int depositorId,
            long goldAmount,
            string itemId,
            int itemQuantity)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            DepositorId = depositorId;
            GoldAmount = goldAmount;
            ItemId = itemId;
            ItemQuantity = itemQuantity;
            Timestamp = DateTime.UtcNow;
        }
    }
}
