using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Trading.Tests
{
    /// <summary>
    /// Proves that a TradingManager built with an event bus publishes each trade event exactly once, with the right
    /// values, over the trade lifecycle: proposed, then either declined or accepted and completed.
    /// </summary>
    [TestFixture]
    public class TradingEventTests
    {
        private EventBus _bus;
        private TradingManager _manager;

        [SetUp]
        public void Setup()
        {
            _bus = new EventBus();
            var registry = new TraderRegistry();
            _manager = new TradingManager(registry, _bus);

            var merchant = Trader.Create("merchant_001", "General Merchant", TraderType.Merchant);
            registry.RegisterTrader(merchant);

            var offer = TradeOffer.Create("offer_001", "merchant_001");
            offer.AddOfferedItem(new TradeItem("item_iron_sword", 1, 0));
            offer.AddRequestedItem(new TradeItem("item_wolf_pelt", 3, 0));
            offer.AddRequestedItem(new TradeItem("item_bone", 2, 0));
            offer.SetGoldAmount(-150);
            merchant.AddOffer(offer);

            _manager.InitializePlayerGold(playerEntityId: 1, goldAmount: 500);
        }

        [TearDown]
        public void Teardown()
        {
            _bus.Dispose();
        }

        [Test]
        public void Proposing_PublishesTradeProposed()
        {
            var received = new List<TradeProposedEvent>();
            _bus.Subscribe<TradeProposedEvent>(received.Add);

            Assert.That(_manager.ProposeTradeOffer(1, "offer_001"), Is.True);
            Assert.That(_manager.ProposeTradeOffer(1, "missing_offer"), Is.False); // not found: no event

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].PlayerEntityId, Is.EqualTo(1));
            Assert.That(received[0].TraderId, Is.EqualTo("merchant_001"));
            Assert.That(received[0].TradeOfferId, Is.EqualTo("offer_001"));
            Assert.That(received[0].OfferedItemCount, Is.EqualTo(1));
            Assert.That(received[0].RequestedItemCount, Is.EqualTo(2));
            Assert.That(received[0].GoldAmount, Is.EqualTo(-150));
        }

        [Test]
        public void Declining_PublishesTradeDeclined()
        {
            var received = new List<TradeDeclinedEvent>();
            _bus.Subscribe<TradeDeclinedEvent>(received.Add);
            _manager.ProposeTradeOffer(1, "offer_001");

            _manager.DeclineTrade(1, "offer_001");
            _manager.DeclineTrade(1, "offer_001"); // no longer proposed: no event

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].PlayerEntityId, Is.EqualTo(1));
            Assert.That(received[0].TraderId, Is.EqualTo("merchant_001"));
            Assert.That(received[0].TradeOfferId, Is.EqualTo("offer_001"));
            Assert.That(received[0].Reason, Is.Null);
        }

        [Test]
        public void Accepting_PublishesTradeAccepted()
        {
            var received = new List<TradeAcceptedEvent>();
            _bus.Subscribe<TradeAcceptedEvent>(received.Add);
            _manager.ProposeTradeOffer(1, "offer_001");
            int reputationBefore = _manager.GetPlayerReputation(1, "merchant_001");

            Assert.That(_manager.AcceptTrade(1, "offer_001").Success, Is.True);
            Assert.That(_manager.AcceptTrade(1, "offer_001").Success, Is.False); // no longer proposed: no event

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].PlayerEntityId, Is.EqualTo(1));
            Assert.That(received[0].TraderId, Is.EqualTo("merchant_001"));
            Assert.That(received[0].TradeOfferId, Is.EqualTo("offer_001"));
            Assert.That(received[0].ItemsReceivedCount, Is.EqualTo(1));
            Assert.That(received[0].ItemsGivenCount, Is.EqualTo(2));
            Assert.That(received[0].GoldChange, Is.EqualTo(-150));
            Assert.That(received[0].ReputationChange,
                Is.EqualTo(_manager.GetPlayerReputation(1, "merchant_001") - reputationBefore));
        }

        [Test]
        public void Accepting_PublishesTradeCompletedAfterAccepted()
        {
            var order = new List<string>();
            var completed = new List<TradeCompletedEvent>();
            _bus.Subscribe<TradeAcceptedEvent>(e => order.Add("accepted"));
            _bus.Subscribe<TradeCompletedEvent>(e =>
            {
                order.Add("completed");
                completed.Add(e);
            });
            _manager.ProposeTradeOffer(1, "offer_001");

            _manager.AcceptTrade(1, "offer_001");

            CollectionAssert.AreEqual(new[] { "accepted", "completed" }, order);
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(completed[0].PlayerEntityId, Is.EqualTo(1));
            Assert.That(completed[0].TraderId, Is.EqualTo("merchant_001"));
            Assert.That(completed[0].TradeOfferId, Is.EqualTo("offer_001"));
            Assert.That(completed[0].TotalValue, Is.EqualTo(150));
            Assert.That(completed[0].NewReputation, Is.EqualTo(_manager.GetPlayerReputation(1, "merchant_001")));
        }
    }
}
