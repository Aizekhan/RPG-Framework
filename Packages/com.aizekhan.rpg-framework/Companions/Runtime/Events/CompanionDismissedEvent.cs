using System;
using RPGFramework.Events;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion is dismissed/deactivated.
    /// Published by <c>Companion.Dismiss</c> when an active companion is sent away (needs the publisher from CompanionManager).
    /// </summary>
    public struct CompanionDismissedEvent : IEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OwnerId;
        public DateTime DismissedAt;
    }
}
