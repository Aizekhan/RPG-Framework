using System;
using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Trading.Tests
{
    /// <summary>
    /// Proves that CurrencyWallet keeps balances per owner and currency, refuses negative amounts and short spends,
    /// and publishes one CurrencyChangedEvent per change (none when nothing changed).
    /// </summary>
    [TestFixture]
    public class CurrencyWalletTests
    {
        private EventBus _bus;
        private CurrencyWallet _wallet;
        private List<CurrencyChangedEvent> _changes;

        [SetUp]
        public void Setup()
        {
            _bus = new EventBus();
            _wallet = new CurrencyWallet(_bus);
            _changes = new List<CurrencyChangedEvent>();
            _bus.Subscribe<CurrencyChangedEvent>(_changes.Add);
        }

        [TearDown]
        public void Teardown()
        {
            _bus.Dispose();
        }

        [Test]
        public void Balance_OfAnUnknownOwner_IsZero()
        {
            Assert.That(_wallet.Balance(1), Is.EqualTo(0));
            Assert.That(_wallet.BalancesOf(1), Is.Empty);
        }

        [Test]
        public void Add_RaisesTheBalanceOfThatCurrencyOnly()
        {
            _wallet.Add(1, 100);
            _wallet.Add(1, 5, "gems");

            Assert.That(_wallet.Balance(1), Is.EqualTo(100));
            Assert.That(_wallet.Balance(1, Currencies.Gold), Is.EqualTo(100));
            Assert.That(_wallet.Balance(1, "gems"), Is.EqualTo(5));
            Assert.That(_wallet.Balance(2), Is.EqualTo(0));
            Assert.That(_wallet.CanAfford(1, 100), Is.True);
            Assert.That(_wallet.CanAfford(1, 101), Is.False);
        }

        [Test]
        public void TrySpend_TakesTheAmount()
        {
            _wallet.Add(1, 100);

            Assert.That(_wallet.TrySpend(1, 30), Is.True);

            Assert.That(_wallet.Balance(1), Is.EqualTo(70));
        }

        [Test]
        public void TrySpend_WhenShort_FailsAndChangesNothing()
        {
            _wallet.Add(1, 20);
            _changes.Clear();

            Assert.That(_wallet.TrySpend(1, 21), Is.False);

            Assert.That(_wallet.Balance(1), Is.EqualTo(20));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void Changes_PublishTheDeltaTheBalanceAndTheReason()
        {
            _wallet.Add(1, 100, Currencies.Gold, "quest:wolves");
            _wallet.TrySpend(1, 40, Currencies.Gold, "shop:sword");

            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[0].OwnerId, Is.EqualTo(1));
            Assert.That(_changes[0].Currency, Is.EqualTo(Currencies.Gold));
            Assert.That(_changes[0].Change, Is.EqualTo(100));
            Assert.That(_changes[0].Balance, Is.EqualTo(100));
            Assert.That(_changes[0].Reason, Is.EqualTo("quest:wolves"));
            Assert.That(_changes[1].Change, Is.EqualTo(-40));
            Assert.That(_changes[1].Balance, Is.EqualTo(60));
            Assert.That(_changes[1].Reason, Is.EqualTo("shop:sword"));
        }

        [Test]
        public void NoOps_PublishNothing()
        {
            _wallet.Add(1, 10);
            _changes.Clear();

            _wallet.Add(1, 0);
            Assert.That(_wallet.TrySpend(1, 0), Is.True);
            _wallet.SetBalance(1, Currencies.Gold, 10);

            Assert.That(_changes, Is.Empty);
            Assert.That(_wallet.Balance(1), Is.EqualTo(10));
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.Add(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.TrySpend(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.SetBalance(1, Currencies.Gold, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.CanAfford(1, -1));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void SetBalance_RestoresASaveAndPublishesTheDifference()
        {
            _wallet.Add(1, 30);
            _changes.Clear();

            _wallet.SetBalance(1, Currencies.Gold, 250);
            _wallet.SetBalance(1, "gems", 7);

            Assert.That(_wallet.Balance(1), Is.EqualTo(250));
            Assert.That(_wallet.BalancesOf(1), Is.EquivalentTo(new Dictionary<string, long> { { Currencies.Gold, 250 }, { "gems", 7 } }));
            Assert.That(_changes.Count, Is.EqualTo(2));
            Assert.That(_changes[0].Change, Is.EqualTo(220));
            Assert.That(_changes[0].Balance, Is.EqualTo(250));
        }

        [Test]
        public void BalancesOf_IsACopy()
        {
            _wallet.Add(1, 30);
            var balances = _wallet.BalancesOf(1);

            _wallet.Add(1, 10);

            Assert.That(balances[Currencies.Gold], Is.EqualTo(30));
        }

        [Test]
        public void Forget_DropsEveryBalanceOfTheOwnerOnly()
        {
            _wallet.Add(1, 30);
            _wallet.Add(1, 3, "gems");
            _wallet.Add(2, 50);
            _changes.Clear();

            _wallet.Forget(1);

            Assert.That(_wallet.Balance(1), Is.EqualTo(0));
            Assert.That(_wallet.Balance(1, "gems"), Is.EqualTo(0));
            Assert.That(_wallet.BalancesOf(1), Is.Empty);
            Assert.That(_wallet.Balance(2), Is.EqualTo(50));
            Assert.That(_changes, Is.Empty);
        }
    }
}
