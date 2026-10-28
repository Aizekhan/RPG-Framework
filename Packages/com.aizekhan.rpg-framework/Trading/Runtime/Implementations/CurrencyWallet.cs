using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Default implementation of ICurrencyWallet, kept in memory (the game saves and restores it through
    /// <see cref="BalancesOf"/> and <see cref="SetBalance"/>).
    /// With an event publisher, raises CurrencyChangedEvent on every change of a balance.
    /// A null or empty currency id throws ArgumentException; a sum past long.MaxValue throws OverflowException.
    /// </summary>
    public class CurrencyWallet : ICurrencyWallet
    {
        private readonly IEventPublisher _events;
        private readonly Dictionary<int, Dictionary<string, long>> _balances = new Dictionary<int, Dictionary<string, long>>();

        public CurrencyWallet(IEventPublisher events = null)
        {
            _events = events;
        }

        public long Balance(int ownerId, string currency = Currencies.Gold)
        {
            CheckCurrency(currency);
            return _balances.TryGetValue(ownerId, out var owned) && owned.TryGetValue(currency, out var balance) ? balance : 0;
        }

        public bool CanAfford(int ownerId, long amount, string currency = Currencies.Gold)
        {
            CheckAmount(amount);
            return Balance(ownerId, currency) >= amount;
        }

        public void Add(int ownerId, long amount, string currency = Currencies.Gold, string reason = null)
        {
            CheckAmount(amount);
            CheckCurrency(currency);
            if (amount == 0)
                return;

            long balance = checked(Balance(ownerId, currency) + amount);
            Store(ownerId, currency, balance, amount, reason);
        }

        public bool TrySpend(int ownerId, long amount, string currency = Currencies.Gold, string reason = null)
        {
            CheckAmount(amount);
            long balance = Balance(ownerId, currency);
            if (balance < amount)
                return false;
            if (amount == 0)
                return true;

            Store(ownerId, currency, balance - amount, -amount, reason);
            return true;
        }

        public void SetBalance(int ownerId, string currency, long amount)
        {
            CheckAmount(amount);
            long balance = Balance(ownerId, currency);
            if (balance == amount)
                return;

            Store(ownerId, currency, amount, amount - balance, null);
        }

        public IReadOnlyDictionary<string, long> BalancesOf(int ownerId)
        {
            return _balances.TryGetValue(ownerId, out var owned)
                ? new Dictionary<string, long>(owned)
                : new Dictionary<string, long>();
        }

        public void Forget(int ownerId)
        {
            _balances.Remove(ownerId);
        }

        private void Store(int ownerId, string currency, long balance, long change, string reason)
        {
            if (!_balances.TryGetValue(ownerId, out var owned))
            {
                owned = new Dictionary<string, long>();
                _balances[ownerId] = owned;
            }
            owned[currency] = balance;

            _events?.Publish(new CurrencyChangedEvent
            {
                OwnerId = ownerId,
                Currency = currency,
                Change = change,
                Balance = balance,
                Reason = reason
            });
        }

        private static void CheckAmount(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must not be negative");
        }

        private static void CheckCurrency(string currency)
        {
            if (string.IsNullOrEmpty(currency))
                throw new ArgumentException("Currency id must not be null or empty", nameof(currency));
        }
    }
}
