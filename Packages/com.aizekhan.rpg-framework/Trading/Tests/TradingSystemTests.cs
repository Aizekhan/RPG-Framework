using NUnit.Framework;
using RPGFramework.Trading;
using System.Collections.Generic;

namespace RPGFramework.Trading.Tests
{
    [TestFixture]
    public class TradingSystemTests
    {
        private TraderRegistry _registry;
        private TradingManager _manager;
        private Trader _merchant;
        private ITradeOffer _basicOffer;

        [SetUp]
        public void Setup()
        {
            _registry = new TraderRegistry();
            _manager = new TradingManager(_registry);

            // Create a basic merchant
            _merchant = Trader.Create("merchant_001", "General Merchant", TraderType.Merchant);
            _registry.RegisterTrader(_merchant);

            // Create a basic trade offer
            _basicOffer = TradeOffer.Create("offer_001", "merchant_001");
            (_basicOffer as TradeOffer)?.AddOfferedItem(new TradeItem("item_iron_sword", 1, 0));
            (_basicOffer as TradeOffer)?.AddRequestedItem(new TradeItem("item_gold_coin", 100, 0));
            (_basicOffer as TradeOffer)?.SetGoldAmount(0);
            _merchant.AddOffer(_basicOffer);

            // Initialize player with gold
            _manager.InitializePlayerGold(playerEntityId: 1, goldAmount: 500);
        }

        // ==================== Trader Registry Tests ====================

        [Test]
        public void RegisterTrader_NewTrader_SuccessfullyRegisters()
        {
            var trader = Trader.Create("blacksmith_001", "Master Blacksmith", TraderType.Blacksmith);
            _registry.RegisterTrader(trader);

            Assert.That(_registry.HasTrader("blacksmith_001"), Is.True);
            Assert.That(_registry.GetTrader("blacksmith_001").Name, Is.EqualTo("Master Blacksmith"));
        }

        [Test]
        public void GetTrader_ValidId_ReturnsTrader()
        {
            var trader = _registry.GetTrader("merchant_001");
            Assert.That(trader, Is.Not.Null);
            Assert.That(trader!.Name, Is.EqualTo("General Merchant"));
        }

        [Test]
        public void GetTrader_InvalidId_ReturnsNull()
        {
            var trader = _registry.GetTrader("nonexistent_trader");
            Assert.That(trader, Is.Null);
        }

        [Test]
        public void GetTradersByType_ValidType_ReturnsMatchingTraders()
        {
            var blacksmith = Trader.Create("blacksmith_001", "Blacksmith", TraderType.Blacksmith);
            _registry.RegisterTrader(blacksmith);

            var merchants = _registry.GetTradersByType(TraderType.Merchant);
            var blacksmiths = _registry.GetTradersByType(TraderType.Blacksmith);

            Assert.That(merchants.Count, Is.EqualTo(1));
            Assert.That(blacksmiths.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetAllTraders_ReturnsAllRegistered()
        {
            var blacksmith = Trader.Create("blacksmith_001", "Blacksmith", TraderType.Blacksmith);
            var alchemist = Trader.Create("alchemist_001", "Alchemist", TraderType.Alchemist);
            _registry.RegisterTrader(blacksmith);
            _registry.RegisterTrader(alchemist);

            var all = _registry.GetAllTraders();
            Assert.That(all.Count, Is.EqualTo(3));
        }

        // ==================== Trade Offer Tests ====================

        [Test]
        public void TradeOffer_Create_InitializesCorrectly()
        {
            var offer = TradeOffer.Create("test_offer", "merchant_001");
            Assert.That(offer.OfferId, Is.EqualTo("test_offer"));
            Assert.That(offer.TraderId, Is.EqualTo("merchant_001"));
            Assert.That(offer.IsAvailable, Is.True);
        }

        [Test]
        public void TradeOffer_AddItems_StoresCorrectly()
        {
            var offer = TradeOffer.Create("offer_test", "merchant_001");
            var item1 = new TradeItem("item_sword", 1, 0);
            var item2 = new TradeItem("item_shield", 1, 0);

            (offer as TradeOffer)?.AddOfferedItem(item1);
            (offer as TradeOffer)?.AddOfferedItem(item2);

            Assert.That(offer.OfferedItems.Count, Is.EqualTo(2));
        }

        [Test]
        public void TradeOffer_CanBeUsedAgain_WithoutMaxUses()
        {
            Assert.That(_basicOffer.CanBeUsedAgain(), Is.True);
            _basicOffer.IncrementUse();
            Assert.That(_basicOffer.CanBeUsedAgain(), Is.True);
        }

        [Test]
        public void TradeOffer_CanBeUsedAgain_WithMaxUses()
        {
            var offer = TradeOffer.Create("limited_offer", "merchant_001");
            (offer as TradeOffer)?.SetMaxUses(2);

            Assert.That(offer.CanBeUsedAgain(), Is.True);
            offer.IncrementUse();
            Assert.That(offer.CanBeUsedAgain(), Is.True);
            offer.IncrementUse();
            Assert.That(offer.CanBeUsedAgain(), Is.False);
        }

        // ==================== Trader Tests ====================

        [Test]
        public void Trader_GetAvailableOffers_ReturnsOnlyAvailable()
        {
            var offer2 = TradeOffer.Create("offer_002", "merchant_001");
            var trader = _registry.GetTrader("merchant_001") as Trader;

            Assert.That(_merchant.GetAvailableOffers(playerEntityId: 1).Count, Is.GreaterThan(0));
        }

        [Test]
        public void Trader_MeetsReputationRequirement_PassesWhenEnough()
        {
            var trader = new Trader("exclusive_merchant", "Exclusive", TraderType.Merchant, priceModifier: 1.0f, minimumReputation: 50);
            Assert.That(trader.MeetsReputationRequirement(60), Is.True);
            Assert.That(trader.MeetsReputationRequirement(50), Is.True);
            Assert.That(trader.MeetsReputationRequirement(49), Is.False);
        }

        [Test]
        public void Trader_GetReputationGain_ReturnsConfigured()
        {
            var trader = Trader.Create("trader_001", "Trader", TraderType.Merchant);
            trader.SetReputationGain(25);
            Assert.That(trader.GetReputationGain(), Is.EqualTo(25));
        }

        // ==================== Trading Manager Tests ====================

        [Test]
        public void ProposeTradeOffer_ValidOffer_ReturnsTrue()
        {
            var result = _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "offer_001");
            Assert.That(result, Is.True);
        }

        [Test]
        public void ProposeTradeOffer_InvalidOffer_ReturnsFalse()
        {
            var result = _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "nonexistent_offer");
            Assert.That(result, Is.False);
        }

        [Test]
        public void AcceptTrade_ValidTrade_TransfersGold()
        {
            _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "offer_001");
            var initialGold = _manager.GetPlayerGold(playerEntityId: 1);

            var offer = TradeOffer.Create("test_gold_offer", "merchant_001");
            (offer as TradeOffer)?.SetGoldAmount(100);
            _merchant.AddOffer(offer);

            _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "test_gold_offer");
            var result = _manager.AcceptTrade(playerEntityId: 1, offerId: "test_gold_offer");

            Assert.That(result.Success, Is.True);
            Assert.That(result.GoldChange, Is.EqualTo(100));
        }

        [Test]
        public void AcceptTrade_InsufficientGold_Fails()
        {
            var offer = TradeOffer.Create("expensive_offer", "merchant_001");
            (offer as TradeOffer)?.SetGoldAmount(-1000); // Costs 1000 gold
            _merchant.AddOffer(offer);

            _manager.InitializePlayerGold(playerEntityId: 2, goldAmount: 500);
            _manager.ProposeTradeOffer(playerEntityId: 2, offerId: "expensive_offer");
            var result = _manager.AcceptTrade(playerEntityId: 2, offerId: "expensive_offer");

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void DeclineTrade_ValidTrade_RemovesProposal()
        {
            _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "offer_001");
            _manager.DeclineTrade(playerEntityId: 1, offerId: "offer_001");
            // Should not crash on next operations
            var result = _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "offer_001");
            Assert.That(result, Is.True);
        }

        [Test]
        public void GetPlayerGold_ReturnsCorrectAmount()
        {
            var gold = _manager.GetPlayerGold(playerEntityId: 1);
            Assert.That(gold, Is.EqualTo(500));
        }

        [Test]
        public void GetPlayerReputation_ReturnsCorrectValue()
        {
            _manager.SetPlayerReputation(playerEntityId: 1, traderId: "merchant_001", reputation: 50);
            var rep = _manager.GetPlayerReputation(playerEntityId: 1, traderId: "merchant_001");
            Assert.That(rep, Is.EqualTo(50));
        }

        [Test]
        public void SetPlayerReputation_UpdatesCorrectly()
        {
            _manager.SetPlayerReputation(playerEntityId: 1, traderId: "merchant_001", reputation: 75);
            var rep = _manager.GetPlayerReputation(playerEntityId: 1, traderId: "merchant_001");
            Assert.That(rep, Is.EqualTo(75));
        }

        // ==================== Trade Condition Tests ====================

        [Test]
        public void TradeCondition_CreateReputation_Correctly()
        {
            var condition = TradeConditionData.RequireReputation("merchant_001", 50);
            Assert.That(condition.Type, Is.EqualTo(ConditionType.ReputationLevel));
            Assert.That(condition.Value, Is.EqualTo(50));
        }

        [Test]
        public void TradeCondition_CreateQuestComplete_Correctly()
        {
            var condition = TradeConditionData.RequireQuestComplete("quest_001");
            Assert.That(condition.Type, Is.EqualTo(ConditionType.QuestComplete));
            Assert.That(condition.ParameterId, Is.EqualTo("quest_001"));
        }

        [Test]
        public void TradeCondition_CreatePlayerLevel_Correctly()
        {
            var condition = TradeConditionData.RequirePlayerLevel(10);
            Assert.That(condition.Type, Is.EqualTo(ConditionType.PlayerLevel));
            Assert.That(condition.Value, Is.EqualTo(10));
        }

        // ==================== Active Trades Data Tests ====================

        [Test]
        public void ActiveTradesData_GetReputation_ReturnsZeroIfNotSet()
        {
            var data = new ActiveTradesData();
            Assert.That(data.GetReputation("trader_001"), Is.EqualTo(0));
        }

        [Test]
        public void ActiveTradesData_AddReputation_IncrementsCorrectly()
        {
            var data = new ActiveTradesData();
            data.AddReputation("trader_001", 25);
            data.AddReputation("trader_001", 15);
            Assert.That(data.GetReputation("trader_001"), Is.EqualTo(40));
        }

        [Test]
        public void ActiveTradesData_GoldOperations_WorkCorrectly()
        {
            var data = new ActiveTradesData(initialGold: 100);
            Assert.That(data.AvailableGold, Is.EqualTo(100));

            data.AddGold(50);
            Assert.That(data.AvailableGold, Is.EqualTo(150));
            Assert.That(data.TotalGoldGained, Is.EqualTo(50));

            bool spent = data.SpendGold(30);
            Assert.That(spent, Is.True);
            Assert.That(data.AvailableGold, Is.EqualTo(120));
            Assert.That(data.TotalGoldSpent, Is.EqualTo(30));
        }

        [Test]
        public void ActiveTradesData_SpendGoldInsufficient_ReturnsFalse()
        {
            var data = new ActiveTradesData(initialGold: 50);
            bool spent = data.SpendGold(100);
            Assert.That(spent, Is.False);
            Assert.That(data.AvailableGold, Is.EqualTo(50));
        }

        [Test]
        public void ActiveTradesData_RecordCompletedTrade_IncrementsCounter()
        {
            var data = new ActiveTradesData(0); // Use constructor to initialize collections
            data.ActiveTradeIds.Add("trade_001");
            data.ActiveTradeIds.Add("trade_002");

            data.RecordCompletedTrade("trade_001");

            Assert.That(data.CompletedTradeCount, Is.EqualTo(1));
            Assert.That(data.ActiveTradeIds.Contains("trade_001"), Is.False);
        }

        // ==================== Integration Tests ====================

        [Test]
        public void FullTradeFlow_ProposeAcceptComplete_Success()
        {
            // Arrange
            var offer = TradeOffer.Create("full_trade_offer", "merchant_001");
            (offer as TradeOffer)?.AddOfferedItem(new TradeItem("item_potion", 3, 0));
            (offer as TradeOffer)?.AddRequestedItem(new TradeItem("item_gold_coin", 50, 0));
            (offer as TradeOffer)?.SetGoldAmount(100);
            _merchant.AddOffer(offer);
            _manager.InitializePlayerGold(playerEntityId: 3, goldAmount: 200);

            // Act
            bool proposed = _manager.ProposeTradeOffer(playerEntityId: 3, offerId: "full_trade_offer");
            var result = _manager.AcceptTrade(playerEntityId: 3, offerId: "full_trade_offer");
            int finalGold = (int)_manager.GetPlayerGold(playerEntityId: 3);

            // Assert
            Assert.That(proposed, Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(finalGold, Is.EqualTo(300)); // 200 + 100 gold from trade
        }

        [Test]
        public void MultipleTradesWithSamePlayer_WorkIndependently()
        {
            // Setup multiple offers
            var offer1 = TradeOffer.Create("offer_multi_1", "merchant_001");
            (offer1 as TradeOffer)?.SetGoldAmount(50);
            _merchant.AddOffer(offer1);

            var offer2 = TradeOffer.Create("offer_multi_2", "merchant_001");
            (offer2 as TradeOffer)?.SetGoldAmount(75);
            _merchant.AddOffer(offer2);

            // Execute trades
            _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "offer_multi_1");
            _manager.AcceptTrade(playerEntityId: 1, offerId: "offer_multi_1");

            _manager.ProposeTradeOffer(playerEntityId: 1, offerId: "offer_multi_2");
            _manager.AcceptTrade(playerEntityId: 1, offerId: "offer_multi_2");

            var finalGold = _manager.GetPlayerGold(playerEntityId: 1);
            Assert.That(finalGold, Is.EqualTo(625)); // 500 + 50 + 75
        }

        [Test]
        public void TradeWithReputation_ProvidesReputationGain()
        {
            var trader = Trader.Create("elite_trader", "Elite Merchant", TraderType.Merchant);
            trader.SetReputationGain(20);
            _registry.RegisterTrader(trader);

            var offer = TradeOffer.Create("rep_offer", "elite_trader");
            (offer as TradeOffer)?.SetGoldAmount(100);
            trader.AddOffer(offer);

            _manager.InitializePlayerGold(playerEntityId: 4, goldAmount: 500);
            _manager.ProposeTradeOffer(playerEntityId: 4, offerId: "rep_offer");
            _manager.AcceptTrade(playerEntityId: 4, offerId: "rep_offer");

            var rep = _manager.GetPlayerReputation(playerEntityId: 4, traderId: "elite_trader");
            Assert.That(rep, Is.GreaterThanOrEqualTo(20));
        }
    }
}
