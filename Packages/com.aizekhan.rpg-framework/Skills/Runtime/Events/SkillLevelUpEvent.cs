// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Events/SkillLevelUpEvent.cs

using RPGFramework.Events;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Event raised when a skill's level increases.
    /// Published by <c>SkillManager.IncreaseSkillLevel</c> (when given an event publisher) after the new level is stored.
    /// </summary>
    public struct SkillLevelUpEvent : IEvent
    {
        /// <summary>Entity whose skill leveled up</summary>
        public int EntityId;

        /// <summary>ID of the skill that leveled up</summary>
        public string SkillId;

        /// <summary>Previous level</summary>
        public int OldLevel;

        /// <summary>New level after level up</summary>
        public int NewLevel;

        /// <summary>Timestamp of level up (0 when published by SkillManager, which keeps no clock)</summary>
        public float TimeStamp;
    }
}
