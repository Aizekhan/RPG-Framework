using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ITradingManager.
    /// Manages trading operations and state.
    /// </summary>
    public class TradingManager : ITradingManager
    {
        private readonly ITraderRegistry _traderRegistry;
        private readonly Dictionary<int, ActiveTradesData> _playerTrades = new Dictionary<int, ActiveTradesData>();
        private readonly Dictionary<string, (int playerId, ITradeOffer offer, float timestamp)> _proposedTrades = new Dictionary<string, (int, ITradeOffer, float)>();

        public TradingManager(ITraderRegistry traderRegistry)
        {
            _traderRegistry = traderRegistry;
        }

        public bool ProposeTradeOffer(int playerEntityId, string offerId)
        {
            // Find the offer
            ITradeOffer? offer = null;
            ITrader? trader = null;

            foreach (var t in _traderRegistry.GetAllTraders())
            {
                var o = t.GetOffer(offerId);
                if (o != null)
                {
                    offer = o;
                    trader = t;
                    break;
                }
            }

            if (offer == null || trader == null)
                return false;

            // Check if can use again
            if (!offer.CanBeUsedAgain())
                return false;

            // Check conditions (simplified - game layer handles specifics)
            var playerData = GetOrCreatePlayerData(playerEntityId);
            int playerRep = playerData.GetReputation(trader.TraderId);
            if (!trader.MeetsReputationRequirement(playerRep))
                return false;

            // Store proposed trade
            _proposedTrades[offerId] = (playerEntityId, offer, 0f); // Timestamp handled by game layer
            return true;
        }

        public TradeResultData AcceptTrade(int playerEntityId, string offerId)
        {
            // Get proposed trade
            if (!_proposedTrades.TryGetValue(offerId, out var proposed))
                return TradeResultData.CreateFailure("Trade not proposed");

            if (proposed.playerId != playerEntityId)
                return TradeResultData.CreateFailure("Wrong entity for this trade");

            var offer = proposed.offer;
            var playerData = GetOrCreatePlayerData(playerEntityId);
            var trader = _traderRegistry.GetTrader(offer.TraderId);

            if (trader == null)
                return TradeResultData.CreateFailure("Trader not found");

            // Check gold
            long totalGoldCost = offer.GoldAmount < 0 ? -offer.GoldAmount : 0;
            if (playerData.AvailableGold < totalGoldCost)
                return TradeResultData.CreateFailure("Insufficient gold");

            // Create result
            var result = TradeResultData.CreateSuccess(offerId, offer.GoldAmount, 0f); // Timestamp handled by game layer
            result.ItemsReceived.AddRange(offer.OfferedItems);
            result.ItemsGiven.AddRange(offer.RequestedItems);

            // Apply trade
            if (offer.GoldAmount > 0)
                playerData.AddGold(offer.GoldAmount);
            else if (offer.GoldAmount < 0)
                playerData.SpendGold(-offer.GoldAmount);

            playerData.AddReputation(offer.TraderId, trader.GetReputationGain());
            playerData.RecordCompletedTrade(offerId);
            offer.IncrementUse();

            // Update player data (struct is value type, must write back)
            _playerTrades[playerEntityId] = playerData;

            // Clean up
            _proposedTrades.Remove(offerId);

            return result;
        }

        public void DeclineTrade(int playerEntityId, string offerId)
        {
            _proposedTrades.Remove(offerId);
        }

        public IReadOnlyList<ITradeOffer> GetTraderOffers(string traderId, int playerEntityId)
        {
            var trader = _traderRegistry.GetTrader(traderId);
            if (trader == null)
                return new List<ITradeOffer>().AsReadOnly();

            return trader.GetAvailableOffers(playerEntityId);
        }

        public long GetPlayerGold(int playerEntityId)
        {
            var data = GetOrCreatePlayerData(playerEntityId);
            return data.AvailableGold;
        }

        public int GetPlayerReputation(int playerEntityId, string traderId)
        {
            var data = GetOrCreatePlayerData(playerEntityId);
            return data.GetReputation(traderId);
        }

        public void SetPlayerReputation(int playerEntityId, string traderId, int reputation)
        {
            var data = GetOrCreatePlayerData(playerEntityId);
            data.TraderReputation[traderId] = reputation;
            _playerTrades[playerEntityId] = data;
        }

        public int GetTradeUseCount(int playerEntityId, string offerId)
        {
            // This would need tracking per offer - simplified for framework
            return 0;
        }

        public IReadOnlyList<string> GetActiveTradeIds(int playerEntityId)
        {
            var data = GetOrCreatePlayerData(playerEntityId);
            return data.ActiveTradeIds.AsReadOnly();
        }

        private ActiveTradesData GetOrCreatePlayerData(int playerEntityId)
        {
            if (!_playerTrades.TryGetValue(playerEntityId, out var data))
            {
                data = new ActiveTradesData(1000); // Default starting gold
                _playerTrades[playerEntityId] = data;
            }
            return data;
        }

        /// <summary>
        /// Initialize player with starting gold (for testing).
        /// </summary>
        public void InitializePlayerGold(int playerEntityId, long goldAmount)
        {
            _playerTrades[playerEntityId] = new ActiveTradesData(goldAmount);
        }

        /// <summary>
        /// Get all player trades data (for testing/debugging).
        /// </summary>
        public ActiveTradesData GetPlayerTradesData(int playerEntityId)
        {
            return GetOrCreatePlayerData(playerEntityId);
        }
    }
}
