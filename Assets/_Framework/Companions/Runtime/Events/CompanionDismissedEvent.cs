using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion is dismissed/deactivated.
    /// </summary>
    public struct CompanionDismissedEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OwnerId;
        public DateTime DismissedAt;
    }
}
