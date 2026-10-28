using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Balances of currencies per owner (an entity or player id). The currency is a string id, gold when not given.
    /// Balances never go below zero, and an owner without a balance has 0.
    /// </summary>
    public interface ICurrencyWallet
    {
        /// <summary>
        /// Current balance of a currency; 0 when the owner has none.
        /// </summary>
        long Balance(int ownerId, string currency = Currencies.Gold);

        /// <summary>
        /// True when the owner has at least <paramref name="amount"/> of the currency.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">When the amount is negative.</exception>
        bool CanAfford(int ownerId, long amount, string currency = Currencies.Gold);

        /// <summary>
        /// Adds to the owner's balance. Adding 0 changes nothing.
        /// </summary>
        /// <param name="reason">Optional note carried by the change event (for example "trade:offer_001").</param>
        /// <exception cref="System.ArgumentOutOfRangeException">When the amount is negative.</exception>
        void Add(int ownerId, long amount, string currency = Currencies.Gold, string reason = null);

        /// <summary>
        /// Takes from the owner's balance when it is enough. Spending 0 succeeds and changes nothing.
        /// </summary>
        /// <param name="reason">Optional note carried by the change event.</param>
        /// <returns>False, with the balance unchanged, when the owner has less than the amount.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">When the amount is negative.</exception>
        bool TrySpend(int ownerId, long amount, string currency = Currencies.Gold, string reason = null);

        /// <summary>
        /// Sets a balance outright, for restoring a save.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">When the amount is negative.</exception>
        void SetBalance(int ownerId, string currency, long amount);

        /// <summary>
        /// A copy of every balance the owner holds, by currency id; empty when there are none.
        /// </summary>
        IReadOnlyDictionary<string, long> BalancesOf(int ownerId);

        /// <summary>
        /// Drops every balance of the owner (when the entity is gone). Publishes no event.
        /// </summary>
        void Forget(int ownerId);
    }
}
