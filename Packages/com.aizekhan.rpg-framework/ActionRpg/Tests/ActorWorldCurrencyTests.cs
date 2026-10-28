using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Events;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Loot;
using RPGFramework.Quests;
using RPGFramework.Trading;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// Money in the action RPG, through the real modules: gold drops from a loot table with its quantity range, quest
    /// rewards pay it, a shop is a trader whose offers move money and items together, every player (or the party)
    /// has their own wallet, and it is saved with the progress.
    /// </summary>
    public sealed class ActorWorldCurrencyTests
    {
        private const string Goblin = "goblin";
        private const string Potion = "potion";
        private const string Pelt = "pelt";
        private const string HuntQuest = "hunt";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgModule _module;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData
            {
                Id = Goblin, DisplayName = "Goblin", MaxHealth = 30f, Attack = 6f, Defense = 2f, ExperienceReward = 10,
                LootTableId = Goblin
            });
            _settings.StartingQuests.Add(HuntQuest);
            _content = new ActionRpgContent();
            _content.Items.Add(new Item(Potion, "Potion", "Heals", ItemType.Consumable, maxStackSize: 10));
            _content.Items.Add(new Item(Pelt, "Pelt", "Soft", ItemType.Crafting, maxStackSize: 10));
            // A goblin always drops 5 to 20 gold and a pelt
            _content.LootTables.Add(LootTableBuilder.Create(Goblin, "Goblin").AddEntry(Currencies.Gold, minQuantity: 5, maxQuantity: 20)
                .AddGuaranteedDrop(Pelt).WithDrops(1, 1).Build());
            _content.Quests.Add(new Quest(HuntQuest, "The Hunt", "Slay a goblin",
                new List<IQuestObjective> { new QuestObjective("slay", ObjectiveType.Kill, Goblin, 1, "Slay a goblin") },
                new List<IQuestReward> { new QuestReward(RewardType.Currency, Currencies.Gold, 100) },
                new List<string>(), minimumLevel: 1, isRepeatable: false));
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame() => _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));

        private void Slay(int enemy, int attacker = -1)
        {
            for (int round = 0; round < 50 && World.Get(enemy) != null; round++)
                World.Attack(attacker < 0 ? World.PlayerId : attacker, enemy);
        }

        // A shop: a potion for 30 gold, and 15 gold for a pelt
        private void OpenShop()
        {
            var shop = Trader.Create("shop", "Shop", TraderType.Merchant);
            var potion = TradeOffer.Create("buy_potion", "shop");
            potion.AddOfferedItem(TradeItem.Create(Potion, 1));
            potion.SetGoldAmount(-30);
            shop.AddOffer(potion);
            var pelt = TradeOffer.Create("sell_pelt", "shop");
            pelt.AddRequestedItem(TradeItem.Create(Pelt, 1));
            pelt.SetGoldAmount(15);
            shop.AddOffer(pelt);
            _host.Container.Resolve<ITraderRegistry>().RegisterTrader(shop);
        }

        [Test]
        public void GoldFromALootTable_GoesToTheWallet_InItsRange_AndAQuestPaysToo()
        {
            Assert.That(_settings.Validate(_content), Is.Empty, "gold is a currency, not a missing item");
            StartGame();
            var changes = new List<CurrencyChangedEvent>();
            _host.Container.Resolve<IEventBus>().Subscribe<CurrencyChangedEvent>(changes.Add);

            Slay(World.SpawnEnemy(Goblin));

            long fromLoot = changes.Where(c => c.Reason == "loot:" + Goblin).Sum(c => c.Change);
            Assert.That(fromLoot, Is.InRange(5, 20));
            Assert.That(World.Inventory.GetAllItems().Select(i => i.ItemId), Is.EqualTo(new[] { Pelt }), "gold is not in the backpack");
            Assert.That(changes.Single(c => c.Reason == "quest:" + HuntQuest).Change, Is.EqualTo(100));
            Assert.That(World.CurrencyOf(), Is.EqualTo(fromLoot + 100));
            Assert.That(World.Wallet, Is.SameAs(_host.Container.Resolve<ICurrencyWallet>()));
        }

        [Test]
        public void AShop_SellsAndBuys_MovingMoneyAndItemsTogether()
        {
            StartGame();
            OpenShop();
            World.GiveCurrency(World.PlayerId, 40);
            World.GiveItem(Pelt, 1);

            Assert.That(World.TakeOffer("buy_potion"), Is.True);
            Assert.That(World.CurrencyOf(), Is.EqualTo(10));
            Assert.That(World.Inventory.GetAllItems().Count(i => i.ItemId == Potion), Is.EqualTo(1));

            var refused = World.Trade(World.PlayerId, "buy_potion");
            Assert.That(refused.Success, Is.False);
            Assert.That(World.CurrencyOf(), Is.EqualTo(10), "nothing moved");
            Assert.That(World.Inventory.GetAllItems().Count(i => i.ItemId == Potion), Is.EqualTo(1));

            Assert.That(World.TakeOffer("sell_pelt"), Is.True);
            Assert.That(World.CurrencyOf(), Is.EqualTo(25));
            Assert.That(World.Inventory.GetAllItems().Any(i => i.ItemId == Pelt), Is.False);
            Assert.That(World.TakeOffer("sell_pelt"), Is.False, "no pelt left to sell");
        }

        // A discount for one party member (their Trade stat, as MythRunner's heroes have)
        private sealed class Haggler : ITradePricing
        {
            public int Who = -1;
            public readonly List<int> Asked = new List<int>();

            public long GoldFor(int playerEntityId, ITrader trader, ITradeOffer offer)
            {
                Asked.Add(playerEntityId);
                return playerEntityId == Who && offer.GoldAmount < 0 ? offer.GoldAmount / 2 : offer.GoldAmount;
            }
        }

        [Test]
        public void Prices_AreTheBuyers_EvenWhenThePartyPays()
        {
            StartGame();
            OpenShop();
            int friend = World.AddPlayer();
            var haggler = new Haggler { Who = friend };
            World.TradePricing = haggler;
            World.GiveCurrency(World.PlayerId, 100);

            Assert.That(World.GoldFor(friend, "buy_potion"), Is.EqualTo(-15));
            Assert.That(World.GoldFor(World.PlayerId, "buy_potion"), Is.EqualTo(-30));
            Assert.That(World.Trade(friend, "buy_potion").Success, Is.True);
            Assert.That(World.CurrencyOf(), Is.EqualTo(85), "the party's wallet paid the friend's price");
            Assert.That(haggler.Asked, Has.No.Member(-1));
            Assert.That(World.TakeOffer("buy_potion"), Is.True);
            Assert.That(World.CurrencyOf(), Is.EqualTo(55));
        }

        [Test]
        public void AFullBackpack_GetsNothing_AndKeepsItsMoney()
        {
            _settings.InventoryCapacity = 1;
            StartGame();
            OpenShop();
            World.GiveCurrency(World.PlayerId, 100);
            World.GiveItem(Pelt, 1);

            Assert.That(World.TakeOffer("buy_potion"), Is.False);
            Assert.That(World.CurrencyOf(), Is.EqualTo(100));
            Assert.That(World.Inventory.GetAllItems().Select(i => i.ItemId), Is.EqualTo(new[] { Pelt }));
        }

        [Test]
        public void PerPlayer_EachHasTheirOwnWallet_Shared_ThePartyHasOne()
        {
            _settings.ProgressMode = ProgressMode.PerPlayer;
            StartGame();
            OpenShop();
            int friend = World.AddPlayer();
            World.GiveCurrency(friend, 50);

            Assert.That(World.CurrencyOf(friend), Is.EqualTo(50));
            Assert.That(World.CurrencyOf(), Is.Zero);
            Assert.That(World.TakeOffer("buy_potion"), Is.False, "the host has no money");
            Assert.That(World.Trade(friend, "buy_potion").Success, Is.True);
            Assert.That(World.InventoryOf(friend).Count, Is.EqualTo(1));
            Assert.That(World.SpendCurrency(friend, 100), Is.False);
            Assert.That(World.SpendCurrency(friend, 20), Is.True);
            Assert.That(World.CurrencyOf(friend), Is.Zero);
            Assert.That(World.GiveCurrency(999, 5), Is.False, "not in the party");
        }

        [Test]
        public void Shared_ThePartySharesTheWallet()
        {
            StartGame();
            int friend = World.AddPlayer();

            World.GiveCurrency(friend, 30);

            Assert.That(World.CurrencyOf(), Is.EqualTo(30));
            Assert.That(World.CurrencyOf(friend), Is.EqualTo(30));
        }

        [Test]
        public void Money_IsSavedWithTheProgress_AndRestored()
        {
            StartGame();
            World.GiveCurrency(World.PlayerId, 70);
            World.GiveCurrency(World.PlayerId, 3, "gems");

            var saved = World.CaptureProgress();
            Assert.That(saved.Currencies.Select(c => (c.Currency, c.Amount)), Is.EqualTo(new[] { ("gems", 3L), (Currencies.Gold, 70L) }));

            World.SpendCurrency(World.PlayerId, 70);
            World.GiveCurrency(World.PlayerId, 9, "shards");
            World.RestoreProgress(saved);

            Assert.That(World.CurrencyOf(), Is.EqualTo(70));
            Assert.That(World.CurrencyOf("gems"), Is.EqualTo(3));
            Assert.That(World.CurrencyOf("shards"), Is.Zero, "the save had none");
        }
    }
}
