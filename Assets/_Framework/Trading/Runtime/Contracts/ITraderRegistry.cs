using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Registry of all traders and their offers.
    /// Central database for trader definitions.
    /// </summary>
    public interface ITraderRegistry
    {
        /// <summary>
        /// Get a specific trader by ID.
        /// </summary>
        /// <param name="traderId">ID of the trader</param>
        /// <returns>Trader or null if not found</returns>
        ITrader? GetTrader(string traderId);

        /// <summary>
        /// Get all traders of a specific type.
        /// </summary>
        /// <param name="traderType">Type to filter by</param>
        /// <returns>List of matching traders</returns>
        IReadOnlyList<ITrader> GetTradersByType(TraderType traderType);

        /// <summary>
        /// Get all registered traders.
        /// </summary>
        IReadOnlyList<ITrader> GetAllTraders();

        /// <summary>
        /// Register a new trader.
        /// </summary>
        void RegisterTrader(ITrader trader);

        /// <summary>
        /// Check if a trader exists.
        /// </summary>
        bool HasTrader(string traderId);
    }
}
