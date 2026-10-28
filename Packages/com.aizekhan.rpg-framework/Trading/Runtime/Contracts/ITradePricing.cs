namespace RPGFramework.Trading
{
    /// <summary>
    /// What an offer costs or pays this player: the game's prices that depend on who trades (a haggling stat, a
    /// faction discount, the trader's <see cref="ITrader.PriceModifier"/>). The trading manager asks it when a trade
    /// is proposed, checked and paid, and for <see cref="ITradingManager.GoldFor"/>. Without one an offer's
    /// <see cref="ITradeOffer.GoldAmount"/> is used as it is.
    /// </summary>
    public interface ITradePricing
    {
        /// <summary>
        /// The gold the player gets from this trade (negative: what they pay), like <see cref="ITradeOffer.GoldAmount"/>.
        /// </summary>
        long GoldFor(int playerEntityId, ITrader trader, ITradeOffer offer);
    }
}
