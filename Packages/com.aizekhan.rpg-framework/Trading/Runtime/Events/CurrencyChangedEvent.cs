using RPGFramework.Events;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Event raised when a balance in a <c>CurrencyWallet</c> changes.
    /// Published by <c>Add</c>, <c>TrySpend</c> and <c>SetBalance</c>; not when nothing changed (adding or spending 0,
    /// a spend that was short, setting the same balance) and not by <c>Forget</c>.
    /// </summary>
    public struct CurrencyChangedEvent : IEvent
    {
        /// <summary>Owner (entity or player id) whose balance changed</summary>
        public int OwnerId;

        /// <summary>ID of the currency</summary>
        public string Currency;

        /// <summary>Change of the balance (positive = gain, negative = spent)</summary>
        public long Change;

        /// <summary>Balance after the change</summary>
        public long Balance;

        /// <summary>Why it changed, as given by the caller (null when none was given)</summary>
        public string Reason;
    }
}
