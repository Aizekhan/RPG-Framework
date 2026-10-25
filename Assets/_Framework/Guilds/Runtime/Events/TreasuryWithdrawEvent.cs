using System;

namespace RPGFramework.Guilds.Runtime
{
    /// <summary>
    /// Event raised when resources are withdrawn from the guild treasury.
    /// </summary>
    public class TreasuryWithdrawEvent
    {
        /// <summary>
        /// Guild ID.
        /// </summary>
        public string GuildId { get; }

        /// <summary>
        /// Entity ID of the member who made the withdrawal.
        /// </summary>
        public int WithdrawerId { get; }

        /// <summary>
        /// Amount of gold withdrawn (0 if item withdrawal).
        /// </summary>
        public long GoldAmount { get; }

        /// <summary>
        /// Item ID withdrawn (null if gold withdrawal).
        /// </summary>
        public string ItemId { get; }

        /// <summary>
        /// Item quantity withdrawn (0 if gold withdrawal).
        /// </summary>
        public int ItemQuantity { get; }

        /// <summary>
        /// Timestamp of the withdrawal.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a gold withdrawal event.
        /// </summary>
        public static TreasuryWithdrawEvent GoldWithdraw(string guildId, int withdrawerId, long amount)
        {
            return new TreasuryWithdrawEvent(guildId, withdrawerId, amount, null, 0);
        }

        /// <summary>
        /// Create an item withdrawal event.
        /// </summary>
        public static TreasuryWithdrawEvent ItemWithdraw(string guildId, int withdrawerId, string itemId, int quantity)
        {
            return new TreasuryWithdrawEvent(guildId, withdrawerId, 0, itemId, quantity);
        }

        private TreasuryWithdrawEvent(
            string guildId,
            int withdrawerId,
            long goldAmount,
            string itemId,
            int itemQuantity)
        {
            GuildId = guildId ?? throw new ArgumentNullException(nameof(guildId));
            WithdrawerId = withdrawerId;
            GoldAmount = goldAmount;
            ItemId = itemId;
            ItemQuantity = itemQuantity;
            Timestamp = DateTime.UtcNow;
        }
    }
}
