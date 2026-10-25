using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion dies/is defeated.
    /// </summary>
    public struct CompanionDeathEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OwnerId;
        public int KillerEntityId;
        public DateTime DeathTime;
    }
}
