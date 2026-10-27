using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion levels up.
    /// Published by <c>Companion.LevelUp</c> (needs the publisher from CompanionManager).
    /// </summary>
    public struct CompanionLevelUpEvent : IEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OldLevel;
        public int NewLevel;
        public DateTime LeveledUpAt;
    }
}
