using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// ECS-based manager for trading operations.
    /// Handles trade validation, execution, and state management.
    /// </summary>
    public interface ITradingManager
    {
        /// <summary>
        /// Propose a trade to a player.
        /// Checks that the offer exists, can be used again and that the player has the trader's reputation.
        /// Each player has their own proposals: two players can propose the same offer.
        /// </summary>
        /// <param name="playerEntityId">Entity receiving the trade</param>
        /// <param name="offerId">ID of the offer to propose</param>
        /// <returns>True if proposal was accepted and can proceed</returns>
        bool ProposeTradeOffer(int playerEntityId, string offerId);

        /// <summary>
        /// Accept a trade this player proposed and execute it: moves the gold (through the wallet when the manager
        /// has one), takes the requested items and gives the offered ones (when the manager has a trade inventory),
        /// and records reputation. All or nothing: a failed trade changes nothing and stays proposed.
        /// </summary>
        /// <param name="playerEntityId">Entity accepting the trade</param>
        /// <param name="offerId">ID of the offer to accept</param>
        /// <returns>Result of the trade execution; on failure, ErrorMessage says why ("Missing items",
        /// "Insufficient gold", ...)</returns>
        TradeResultData AcceptTrade(int playerEntityId, string offerId);

        /// <summary>
        /// Propose and accept an offer in one call, as a shop purchase or sale does.
        /// A trade that fails is not left proposed.
        /// </summary>
        /// <param name="playerEntityId">Entity trading</param>
        /// <param name="offerId">ID of the offer</param>
        /// <returns>Result of the trade; on failure, ErrorMessage says why (including "Offer not found",
        /// "Offer not available" or "Reputation too low" when it could not be proposed)</returns>
        TradeResultData Trade(int playerEntityId, string offerId);

        /// <summary>
        /// Decline a trade this player proposed.
        /// </summary>
        /// <param name="playerEntityId">Entity declining the trade</param>
        /// <param name="offerId">ID of the offer to decline</param>
        void DeclineTrade(int playerEntityId, string offerId);

        /// <summary>
        /// Get all available offers from a specific trader.
        /// </summary>
        /// <param name="traderId">ID of the trader</param>
        /// <param name="playerEntityId">Entity to check conditions against</param>
        /// <returns>List of available offers</returns>
        IReadOnlyList<ITradeOffer> GetTraderOffers(string traderId, int playerEntityId);

        /// <summary>
        /// The gold this player would get from the offer now (negative: what they would pay), with the game's
        /// <see cref="ITradePricing"/>; 0 for an unknown offer. For a shop's price labels.
        /// </summary>
        long GoldFor(int playerEntityId, string offerId);

        /// <summary>
        /// Get player's current gold balance (the wallet's gold when the manager has a wallet).
        /// </summary>
        long GetPlayerGold(int playerEntityId);

        /// <summary>
        /// Get player's reputation with a specific trader.
        /// </summary>
        int GetPlayerReputation(int playerEntityId, string traderId);

        /// <summary>
        /// Set player's reputation with a trader (for admin/testing).
        /// </summary>
        void SetPlayerReputation(int playerEntityId, string traderId, int reputation);

        /// <summary>
        /// Get number of times a player has completed a specific trade.
        /// </summary>
        int GetTradeUseCount(int playerEntityId, string offerId);

        /// <summary>
        /// Get all active trades for a player.
        /// </summary>
        IReadOnlyList<string> GetActiveTradeIds(int playerEntityId);
    }
}
