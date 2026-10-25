using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ITraderRegistry.
    /// Central database of all traders.
    /// </summary>
    public class TraderRegistry : ITraderRegistry
    {
        private readonly Dictionary<string, ITrader> _traders = new Dictionary<string, ITrader>();

        public ITrader? GetTrader(string traderId)
        {
            return _traders.TryGetValue(traderId, out var trader) ? trader : null;
        }

        public IReadOnlyList<ITrader> GetTradersByType(TraderType traderType)
        {
            return _traders.Values
                .Where(t => t.TraderType == traderType)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<ITrader> GetAllTraders()
        {
            return _traders.Values.ToList().AsReadOnly();
        }

        public void RegisterTrader(ITrader trader)
        {
            if (_traders.ContainsKey(trader.TraderId))
            {
                _traders[trader.TraderId] = trader;
            }
            else
            {
                _traders.Add(trader.TraderId, trader);
            }
        }

        public bool HasTrader(string traderId)
        {
            return _traders.ContainsKey(traderId);
        }

        /// <summary>
        /// Clear all registered traders (for testing).
        /// </summary>
        public void Clear()
        {
            _traders.Clear();
        }
    }
}
