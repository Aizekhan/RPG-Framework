using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion is fed food.
    /// </summary>
    public struct CompanionFedEvent
    {
        public string CompanionId;
        public int EntityId;
        public string FoodItemId;
        public int HungerRestored;
        public int HappinessGained;
        public int LoyaltyGained;
        public DateTime FedAt;
    }
}
