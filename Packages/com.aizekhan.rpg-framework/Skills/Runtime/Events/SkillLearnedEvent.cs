// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Events/SkillLearnedEvent.cs

using RPGFramework.Events;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Event raised when an entity learns a new skill.
    /// Published by <c>SkillManager.LearnSkill</c> (when given an event publisher) after the skill is stored at level 1.
    /// </summary>
    public struct SkillLearnedEvent : IEvent
    {
        /// <summary>Entity that learned the skill</summary>
        public int EntityId;

        /// <summary>ID of the skill learned</summary>
        public string SkillId;

        /// <summary>Initial level of the skill (usually 1)</summary>
        public int Level;

        /// <summary>Timestamp when the skill was learned (0 when published by SkillManager, which keeps no clock)</summary>
        public float TimeStamp;
    }
}
