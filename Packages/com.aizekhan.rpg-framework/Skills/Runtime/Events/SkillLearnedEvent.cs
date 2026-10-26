// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Events/SkillLearnedEvent.cs

namespace RPGFramework.Skills
{
    /// <summary>
    /// Event raised when an entity learns a new skill.
    /// </summary>
    public struct SkillLearnedEvent
    {
        /// <summary>Entity that learned the skill</summary>
        public int EntityId;

        /// <summary>ID of the skill learned</summary>
        public string SkillId;

        /// <summary>Initial level of the skill (usually 1)</summary>
        public int Level;

        /// <summary>Timestamp when the skill was learned</summary>
        public float TimeStamp;
    }
}
