using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion dies/is defeated.
    /// Published by <c>Companion.Kill</c> when a living companion dies (needs the publisher from CompanionManager).
    /// KillerEntityId is -1: Kill() is not told who dealt the blow.
    /// </summary>
    public struct CompanionDeathEvent : IEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OwnerId;
        public int KillerEntityId;
        public DateTime DeathTime;
    }
}
