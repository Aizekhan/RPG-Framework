using System.Collections.Generic;
using System.Linq;
using RPGFramework.Events;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ITradingManager.
    /// Manages trading operations and state.
    /// Proposals are kept per player: two players proposing the same offer each have their own.
    /// Gold: with an <see cref="ICurrencyWallet"/>, trades pay from and into the wallet's gold (reason "trade:offerId")
    /// and <see cref="GetPlayerGold"/> is the wallet balance; without one, the manager keeps its own gold per player
    /// (1000 for a player it has not seen, or what <see cref="InitializePlayerGold"/> set).
    /// Items: with an <see cref="ITradeInventory"/>, accepting takes the requested items and gives the offered ones,
    /// all or nothing; without one, items are only listed in the result and the game moves them.
    /// Prices: with an <see cref="ITradePricing"/>, every trade costs what it says for the player trading (a discount
    /// from a stat); without one, the offer's <see cref="ITradeOffer.GoldAmount"/>.
    /// With an event publisher, raises TradeProposed, TradeDeclined, and TradeAccepted followed by TradeCompleted
    /// (accepting applies the trade at once). Timestamps are 0: the manager keeps no clock.
    /// </summary>
    public class TradingManager : ITradingManager
    {
        private readonly ITraderRegistry _traderRegistry;
        private readonly IEventPublisher _events;
        private readonly ICurrencyWallet _wallet;
        private readonly ITradeInventory _inventory;
        private readonly ITradePricing _pricing;
        private readonly Dictionary<int, ActiveTradesData> _playerTrades = new Dictionary<int, ActiveTradesData>();
        private readonly Dictionary<(int playerId, string offerId), (ITradeOffer offer, float timestamp)> _proposedTrades =
            new Dictionary<(int, string), (ITradeOffer, float)>();

        /// <param name="traderRegistry">Where offers are looked up.</param>
        /// <param name="events">Optional publisher for the trade events.</param>
        /// <param name="wallet">Optional wallet that holds the players' gold; the manager keeps its own when null.</param>
        /// <param name="inventory">Optional inventory that trades take items from and give items to.</param>
        /// <param name="pricing">Optional prices per player; the offers' own gold amounts when null.</param>
        public TradingManager(ITraderRegistry traderRegistry, IEventPublisher events = null,
            ICurrencyWallet wallet = null, ITradeInventory inventory = null, ITradePricing pricing = null)
        {
            _pricing = pricing;
            _traderRegistry = traderRegistry;
            _events = events;
            _wallet = wallet;
            _inventory = inventory;
        }

        public bool ProposeTradeOffer(int playerEntityId, string offerId)
        {
            return TryPropose(playerEntityId, offerId) == null;
        }

        public TradeResultData AcceptTrade(int playerEntityId, string offerId)
        {
            // Get proposed trade
            if (!_proposedTrades.TryGetValue((playerEntityId, offerId), out var proposed))
                return TradeResultData.CreateFailure("Trade not proposed");

            var offer = proposed.offer;
            var playerData = GetOrCreatePlayerData(playerEntityId);
            var trader = _traderRegistry.GetTrader(offer.TraderId);

            if (trader == null)
                return TradeResultData.CreateFailure("Trader not found");

            // Another player may have used up the offer since it was proposed
            if (!offer.CanBeUsedAgain())
                return TradeResultData.CreateFailure("Offer not available");

            // Check items
            if (_inventory != null && offer.RequestedItems.Any(i => !_inventory.HasItem(playerEntityId, i.ItemId, i.Quantity)))
                return TradeResultData.CreateFailure("Missing items");

            // Check gold (the price for this player, asked again: their stats may have changed since the proposal)
            long gold = GoldFor(playerEntityId, trader, offer);
            long totalGoldCost = gold < 0 ? -gold : 0;
            long availableGold = _wallet != null ? _wallet.Balance(playerEntityId) : playerData.AvailableGold;
            if (availableGold < totalGoldCost)
                return TradeResultData.CreateFailure("Insufficient gold");

            // Move items and gold, all or nothing
            string failure = Exchange(playerEntityId, offer, gold, ref playerData);
            if (failure != null)
                return TradeResultData.CreateFailure(failure);

            // Create result
            var result = TradeResultData.CreateSuccess(offerId, gold, 0f); // Timestamp handled by game layer
            result.ItemsReceived.AddRange(offer.OfferedItems);
            result.ItemsGiven.AddRange(offer.RequestedItems);

            int reputationGain = trader.GetReputationGain();
            playerData.AddReputation(offer.TraderId, reputationGain);
            playerData.RecordCompletedTrade(offerId);
            offer.IncrementUse();

            // Update player data (struct is value type, must write back)
            _playerTrades[playerEntityId] = playerData;

            // Clean up
            _proposedTrades.Remove((playerEntityId, offerId));

            _events?.Publish(new TradeAcceptedEvent
            {
                PlayerEntityId = playerEntityId,
                TraderId = offer.TraderId,
                TradeOfferId = offerId,
                ItemsReceivedCount = result.ItemsReceived.Count,
                ItemsGivenCount = result.ItemsGiven.Count,
                GoldChange = gold,
                ReputationChange = reputationGain,
                Timestamp = 0f
            });
            _events?.Publish(new TradeCompletedEvent
            {
                PlayerEntityId = playerEntityId,
                TraderId = offer.TraderId,
                TradeOfferId = offerId,
                TotalValue = gold < 0 ? -gold : gold,
                NewReputation = playerData.GetReputation(offer.TraderId),
                Timestamp = 0f
            });

            return result;
        }

        public TradeResultData Trade(int playerEntityId, string offerId)
        {
            string failure = TryPropose(playerEntityId, offerId);
            if (failure != null)
                return TradeResultData.CreateFailure(failure);

            var result = AcceptTrade(playerEntityId, offerId);
            if (!result.Success)
                Decline(playerEntityId, offerId, result.ErrorMessage);
            return result;
        }

        public void DeclineTrade(int playerEntityId, string offerId)
        {
            Decline(playerEntityId, offerId, null);
        }

        public IReadOnlyList<ITradeOffer> GetTraderOffers(string traderId, int playerEntityId)
        {
            var trader = _traderRegistry.GetTrader(traderId);
            if (trader == null)
                return new List<ITradeOffer>().AsReadOnly();

            return trader.GetAvailableOffers(playerEntityId);
        }

        public long GoldFor(int playerEntityId, string offerId)
        {
            foreach (var trader in _traderRegistry.GetAllTraders())
            {
                var offer = trader.GetOffer(offerId);
                if (offer != null)
                    return GoldFor(playerEntityId, trader, offer);
            }
            return 0;
        }

        private long GoldFor(int playerEntityId, ITrader trader, ITradeOffer offer) =>
            _pricing?.GoldFor(playerEntityId, trader, offer) ?? offer.GoldAmount;

        public long GetPlayerGold(int playerEntityId)
        {
            if (_wallet != null)
                return _wallet.Balance(playerEntityId);

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

        /// <summary>
        /// Checks the offer and stores the proposal; returns null on success, else why it failed.
        /// </summary>
        private string TryPropose(int playerEntityId, string offerId)
        {
            // Find the offer
            ITradeOffer offer = null;
            ITrader trader = null;

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
                return "Offer not found";

            // Check if can use again
            if (!offer.CanBeUsedAgain())
                return "Offer not available";

            // Check conditions (simplified - game layer handles specifics)
            var playerData = GetOrCreatePlayerData(playerEntityId);
            int playerRep = playerData.GetReputation(trader.TraderId);
            if (!trader.MeetsReputationRequirement(playerRep))
                return "Reputation too low";

            // Store proposed trade
            _proposedTrades[(playerEntityId, offerId)] = (offer, 0f); // Timestamp handled by game layer

            _events?.Publish(new TradeProposedEvent
            {
                PlayerEntityId = playerEntityId,
                TraderId = trader.TraderId,
                TradeOfferId = offerId,
                OfferedItemCount = offer.OfferedItems.Count,
                RequestedItemCount = offer.RequestedItems.Count,
                GoldAmount = GoldFor(playerEntityId, trader, offer),
                Timestamp = 0f
            });
            return null;
        }

        private void Decline(int playerEntityId, string offerId, string reason)
        {
            if (!_proposedTrades.TryGetValue((playerEntityId, offerId), out var proposed))
                return;

            _proposedTrades.Remove((playerEntityId, offerId));

            _events?.Publish(new TradeDeclinedEvent
            {
                PlayerEntityId = playerEntityId,
                TraderId = proposed.offer.TraderId,
                TradeOfferId = offerId,
                Reason = reason,
                Timestamp = 0f
            });
        }

        /// <summary>
        /// Takes the requested items, gives the offered items and moves the gold. When a step fails, undoes the
        /// steps before it and returns why; returns null when everything moved.
        /// </summary>
        private string Exchange(int playerEntityId, ITradeOffer offer, long gold, ref ActiveTradesData playerData)
        {
            var taken = new List<TradeItem>();
            var given = new List<TradeItem>();

            if (_inventory != null)
            {
                foreach (var item in offer.RequestedItems)
                {
                    if (!_inventory.RemoveItem(playerEntityId, item.ItemId, item.Quantity))
                        return Undo(playerEntityId, taken, given, "Missing items");
                    taken.Add(item);
                }

                foreach (var item in offer.OfferedItems)
                {
                    if (!_inventory.AddItem(playerEntityId, item.ItemId, item.Quantity))
                        return Undo(playerEntityId, taken, given, "Could not give items");
                    given.Add(item);
                }
            }

            string reason = "trade:" + offer.OfferId;
            if (_wallet != null)
            {
                if (gold > 0)
                    _wallet.Add(playerEntityId, gold, Currencies.Gold, reason);
                else if (gold < 0 && !_wallet.TrySpend(playerEntityId, -gold, Currencies.Gold, reason))
                    return Undo(playerEntityId, taken, given, "Insufficient gold");
            }
            else
            {
                if (gold > 0)
                    playerData.AddGold(gold);
                else if (gold < 0 && !playerData.SpendGold(-gold))
                    return Undo(playerEntityId, taken, given, "Insufficient gold");
            }

            return null;
        }

        private string Undo(int playerEntityId, List<TradeItem> taken, List<TradeItem> given, string failure)
        {
            for (int i = given.Count - 1; i >= 0; i--)
                _inventory.RemoveItem(playerEntityId, given[i].ItemId, given[i].Quantity);
            for (int i = taken.Count - 1; i >= 0; i--)
                _inventory.AddItem(playerEntityId, taken[i].ItemId, taken[i].Quantity);
            return failure;
        }

        private ActiveTradesData GetOrCreatePlayerData(int playerEntityId)
        {
            if (!_playerTrades.TryGetValue(playerEntityId, out var data))
            {
                data = new ActiveTradesData(1000); // Default starting gold (only used without a wallet)
                _playerTrades[playerEntityId] = data;
            }
            return data;
        }

        /// <summary>
        /// Initialize player with starting gold (for testing). Resets the player's trading data; with a wallet, sets
        /// the wallet's gold balance.
        /// </summary>
        public void InitializePlayerGold(int playerEntityId, long goldAmount)
        {
            _playerTrades[playerEntityId] = new ActiveTradesData(goldAmount);
            _wallet?.SetBalance(playerEntityId, Currencies.Gold, goldAmount);
        }

        /// <summary>
        /// Get all player trades data (for testing/debugging). With a wallet, its gold fields are not used.
        /// </summary>
        public ActiveTradesData GetPlayerTradesData(int playerEntityId)
        {
            return GetOrCreatePlayerData(playerEntityId);
        }
    }
}
