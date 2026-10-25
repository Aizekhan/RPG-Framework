using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion's loyalty level changes.
    /// </summary>
    public struct LoyaltyChangedEvent
    {
        public string CompanionId;
        public int EntityId;
        public LoyaltyLevel OldLoyalty;
        public LoyaltyLevel NewLoyalty;
        public int LoyaltyValue;
        public DateTime ChangedAt;
    }
}
