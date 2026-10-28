using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Trading.Tests
{
    /// <summary>
    /// Proves that a TradingManager with a wallet and a trade inventory works as a shop: a trade pays from the wallet
    /// and moves the items, all or nothing, and each player has their own proposals.
    /// </summary>
    [TestFixture]
    public class TradingShopTests
    {
        private const int Hero = 1, Friend = 2;

        private EventBus _bus;
        private CurrencyWallet _wallet;
        private FakeInventory _inventory;
        private TradingManager _manager;
        private Trader _smith;

        [SetUp]
        public void Setup()
        {
            _bus = new EventBus();
            var registry = new TraderRegistry();
            _wallet = new CurrencyWallet(_bus);
            _inventory = new FakeInventory();
            _manager = new TradingManager(registry, _bus, _wallet, _inventory);

            _smith = Trader.Create("smith", "Smith", TraderType.Blacksmith);
            registry.RegisterTrader(_smith);

            // Buy a sword for 150 gold and 2 iron
            var sword = TradeOffer.Create("buy_sword", "smith");
            sword.AddOfferedItem(TradeItem.Create("sword", 1));
            sword.AddRequestedItem(TradeItem.Create("iron", 2));
            sword.SetGoldAmount(-150);
            _smith.AddOffer(sword);

            // Sell a pelt for 20 gold
            var pelt = TradeOffer.Create("sell_pelt", "smith");
            pelt.AddRequestedItem(TradeItem.Create("pelt", 1));
            pelt.SetGoldAmount(20);
            _smith.AddOffer(pelt);

            _wallet.Add(Hero, 200);
            _inventory.Give(Hero, "iron", 2);
        }

        [TearDown]
        public void Teardown()
        {
            _bus.Dispose();
        }

        [Test]
        public void Buying_PaysFromTheWalletAndMovesTheItems()
        {
            var changes = new List<CurrencyChangedEvent>();
            _bus.Subscribe<CurrencyChangedEvent>(changes.Add);

            Assert.That(_manager.ProposeTradeOffer(Hero, "buy_sword"), Is.True);
            var result = _manager.AcceptTrade(Hero, "buy_sword");

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.GoldChange, Is.EqualTo(-150));
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(50));
            Assert.That(_manager.GetPlayerGold(Hero), Is.EqualTo(50));
            Assert.That(_inventory.Count(Hero, "iron"), Is.EqualTo(0));
            Assert.That(_inventory.Count(Hero, "sword"), Is.EqualTo(1));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Change, Is.EqualTo(-150));
            Assert.That(changes[0].Reason, Is.EqualTo("trade:buy_sword"));
        }

        [Test]
        public void Selling_PaysIntoTheWallet()
        {
            _inventory.Give(Hero, "pelt", 1);

            var result = _manager.Trade(Hero, "sell_pelt");

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(220));
            Assert.That(_inventory.Count(Hero, "pelt"), Is.EqualTo(0));
        }

        [Test]
        public void MissingItems_FailsAndChangesNothing()
        {
            _inventory.Take(Hero, "iron", 1);
            _manager.ProposeTradeOffer(Hero, "buy_sword");

            var result = _manager.AcceptTrade(Hero, "buy_sword");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Missing items"));
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(200));
            Assert.That(_inventory.Count(Hero, "iron"), Is.EqualTo(1));
            Assert.That(_inventory.Count(Hero, "sword"), Is.EqualTo(0));
            Assert.That(_inventory.Calls, Is.Zero, "nothing taken or given");
        }

        [Test]
        public void InsufficientGold_FailsAndChangesNothing()
        {
            _wallet.TrySpend(Hero, 100);
            _manager.ProposeTradeOffer(Hero, "buy_sword");

            var result = _manager.AcceptTrade(Hero, "buy_sword");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Insufficient gold"));
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(100));
            Assert.That(_inventory.Count(Hero, "iron"), Is.EqualTo(2));
            Assert.That(_inventory.Count(Hero, "sword"), Is.EqualTo(0));
            Assert.That(_inventory.Calls, Is.Zero, "nothing taken or given");
        }

        [Test]
        public void WhenGivingItemsFails_EverythingIsRolledBack()
        {
            _inventory.RefuseToAdd = "sword"; // full inventory
            var accepted = new List<TradeAcceptedEvent>();
            _bus.Subscribe<TradeAcceptedEvent>(accepted.Add);
            _manager.ProposeTradeOffer(Hero, "buy_sword");

            var result = _manager.AcceptTrade(Hero, "buy_sword");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Could not give items"));
            Assert.That(_inventory.Count(Hero, "iron"), Is.EqualTo(2), "the iron taken is given back");
            Assert.That(_inventory.Count(Hero, "sword"), Is.EqualTo(0));
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(200));
            Assert.That(_manager.GetPlayerReputation(Hero, "smith"), Is.EqualTo(0));
            Assert.That(accepted, Is.Empty);
        }

        [Test]
        public void TwoPlayersProposingTheSameOffer_DoNotCollide()
        {
            _wallet.Add(Friend, 500);
            _inventory.Give(Friend, "iron", 2);

            Assert.That(_manager.ProposeTradeOffer(Hero, "buy_sword"), Is.True);
            Assert.That(_manager.ProposeTradeOffer(Friend, "buy_sword"), Is.True);
            _manager.DeclineTrade(Friend, "buy_sword");
            Assert.That(_manager.AcceptTrade(Friend, "buy_sword").Success, Is.False, "the friend declined");

            var result = _manager.AcceptTrade(Hero, "buy_sword");

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(50));
            Assert.That(_wallet.Balance(Friend), Is.EqualTo(500));
            Assert.That(_inventory.Count(Friend, "sword"), Is.EqualTo(0));
        }

        [Test]
        public void Trade_ProposesAndAcceptsInOneCall()
        {
            var proposed = new List<TradeProposedEvent>();
            var completed = new List<TradeCompletedEvent>();
            _bus.Subscribe<TradeProposedEvent>(proposed.Add);
            _bus.Subscribe<TradeCompletedEvent>(completed.Add);

            var result = _manager.Trade(Hero, "buy_sword");

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.TradeId, Is.EqualTo("buy_sword"));
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(50));
            Assert.That(_inventory.Count(Hero, "sword"), Is.EqualTo(1));
            Assert.That(proposed.Count, Is.EqualTo(1));
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(_manager.AcceptTrade(Hero, "buy_sword").Success, Is.False, "no proposal is left");
        }

        [Test]
        public void Trade_WhenTheOfferCannotBeProposed_FailsWithTheReason()
        {
            var result = _manager.Trade(Hero, "no_such_offer");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Offer not found"));
        }

        [Test]
        public void Trade_WhenItFails_LeavesNoProposalAndPublishesTheDecline()
        {
            var declined = new List<TradeDeclinedEvent>();
            _bus.Subscribe<TradeDeclinedEvent>(declined.Add);
            _wallet.TrySpend(Hero, 200);

            var result = _manager.Trade(Hero, "buy_sword");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Insufficient gold"));
            Assert.That(declined.Count, Is.EqualTo(1));
            Assert.That(declined[0].Reason, Is.EqualTo("Insufficient gold"));
            _wallet.Add(Hero, 1000);
            Assert.That(_manager.AcceptTrade(Hero, "buy_sword").ErrorMessage, Is.EqualTo("Trade not proposed"));
        }

        [Test]
        public void AnOfferUsedUpByAnotherPlayer_CannotBeAccepted()
        {
            var limited = TradeOffer.Create("last_sword", "smith");
            limited.AddOfferedItem(TradeItem.Create("sword", 1));
            limited.SetGoldAmount(-10);
            limited.SetMaxUses(1);
            _smith.AddOffer(limited);
            _wallet.Add(Friend, 100);
            _manager.ProposeTradeOffer(Hero, "last_sword");
            _manager.ProposeTradeOffer(Friend, "last_sword");

            Assert.That(_manager.AcceptTrade(Friend, "last_sword").Success, Is.True);
            var result = _manager.AcceptTrade(Hero, "last_sword");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("Offer not available"));
            Assert.That(_wallet.Balance(Hero), Is.EqualTo(200));
        }

        /// <summary>
        /// Item counts per owner; can refuse to add one item id, and counts the RemoveItem/AddItem calls.
        /// </summary>
        private sealed class FakeInventory : ITradeInventory
        {
            private readonly Dictionary<(int, string), int> _counts = new Dictionary<(int, string), int>();

            public string RefuseToAdd;
            public int Calls;

            public int Count(int ownerId, string itemId) =>
                _counts.TryGetValue((ownerId, itemId), out var count) ? count : 0;

            public void Give(int ownerId, string itemId, int quantity) =>
                _counts[(ownerId, itemId)] = Count(ownerId, itemId) + quantity;

            public void Take(int ownerId, string itemId, int quantity) =>
                _counts[(ownerId, itemId)] = Count(ownerId, itemId) - quantity;

            public bool HasItem(int ownerId, string itemId, int quantity) => Count(ownerId, itemId) >= quantity;

            public bool RemoveItem(int ownerId, string itemId, int quantity)
            {
                Calls++;
                if (!HasItem(ownerId, itemId, quantity))
                    return false;
                Take(ownerId, itemId, quantity);
                return true;
            }

            public bool AddItem(int ownerId, string itemId, int quantity)
            {
                Calls++;
                if (itemId == RefuseToAdd)
                    return false;
                Give(ownerId, itemId, quantity);
                return true;
            }
        }
    }
}
