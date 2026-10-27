using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion is summoned/activated.
    /// Published by <c>Companion.Summon</c> when an inactive companion is called out (needs the publisher from CompanionManager).
    /// </summary>
    public struct CompanionSummonedEvent : IEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OwnerId;
        public CompanionType CompanionType;
        public DateTime SummonedAt;
    }
}
