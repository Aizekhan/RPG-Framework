using RPGFramework.Events;

namespace RPGFramework.Progression
{
    /// <summary>
    /// Event raised when an entity unlocks new content.
    /// </summary>
    public struct UnlockAchievedEvent : IEvent
    {
        /// <summary>Entity that achieved unlock</summary>
        public int EntityId;

        /// <summary>Unlock identifier</summary>
        public string UnlockId;

        /// <summary>Name of unlocked content</summary>
        public string UnlockName;

        /// <summary>Unlock type (Ability, Feature, Item, etc.)</summary>
        public string UnlockType;

        /// <summary>Level at which unlock was achieved</summary>
        public int Level;

        /// <summary>Timestamp of unlock</summary>
        public float Timestamp;
    }
}
