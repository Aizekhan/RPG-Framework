using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion's loyalty level changes.
    /// Published by <c>Companion.ModifyLoyalty</c> (also reached through feeding and ownership transfer) only when the
    /// level crosses a boundary, not on every value change.
    /// </summary>
    public struct LoyaltyChangedEvent : IEvent
    {
        public string CompanionId;
        public int EntityId;
        public LoyaltyLevel OldLoyalty;
        public LoyaltyLevel NewLoyalty;
        public int LoyaltyValue;
        public DateTime ChangedAt;
    }
}
