using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion is summoned/activated.
    /// </summary>
    public struct CompanionSummonedEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OwnerId;
        public CompanionType CompanionType;
        public DateTime SummonedAt;
    }
}
