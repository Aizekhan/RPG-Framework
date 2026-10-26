// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Events/SkillLevelUpEvent.cs

namespace RPGFramework.Skills
{
    /// <summary>
    /// Event raised when a skill's level increases.
    /// </summary>
    public struct SkillLevelUpEvent
    {
        /// <summary>Entity whose skill leveled up</summary>
        public int EntityId;

        /// <summary>ID of the skill that leveled up</summary>
        public string SkillId;

        /// <summary>Previous level</summary>
        public int OldLevel;

        /// <summary>New level after level up</summary>
        public int NewLevel;

        /// <summary>Timestamp of level up</summary>
        public float TimeStamp;
    }
}
