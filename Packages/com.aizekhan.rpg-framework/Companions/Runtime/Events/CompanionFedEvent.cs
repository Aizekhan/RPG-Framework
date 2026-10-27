using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion is fed food.
    /// Published by <c>CompanionManager.FeedCompanion</c>; the gains are what actually changed after limits were applied.
    /// </summary>
    public struct CompanionFedEvent : IEvent
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
