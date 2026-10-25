using System;

namespace RPGFramework.Companions.Runtime
{
    /// <summary>
    /// Event fired when a companion levels up.
    /// </summary>
    public struct CompanionLevelUpEvent
    {
        public string CompanionId;
        public int EntityId;
        public int OldLevel;
        public int NewLevel;
        public DateTime LeveledUpAt;
    }
}
