using RPGFramework.Events;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Event raised when an entity levels up.
    /// Can represent multiple levels gained at once.
    /// </summary>
    public struct LevelUpEvent : IEvent
    {
        /// <summary>Entity that leveled up</summary>
        public int EntityId;

        /// <summary>Previous level</summary>
        public int OldLevel;

        /// <summary>New level after level up</summary>
        public int NewLevel;

        /// <summary>Number of levels gained</summary>
        public int LevelsGained;

        /// <summary>Timestamp of level up</summary>
        public float Timestamp;
    }
}
