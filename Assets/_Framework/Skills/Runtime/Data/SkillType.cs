// Assets/_Framework/Skills/Runtime/Data/SkillType.cs

namespace RPGFramework.Skills
{
    /// <summary>
    /// Defines the type/category of a skill.
    /// </summary>
    public enum SkillType
    {
        /// <summary>
        /// Passive skill that provides stat bonuses (e.g., +10% HP, +5 Strength).
        /// </summary>
        Passive = 0,

        /// <summary>
        /// Active skill that unlocks or modifies abilities.
        /// </summary>
        Active = 1,

        /// <summary>
        /// Combat-specific skill (weapon proficiency, combat techniques).
        /// </summary>
        Combat = 2,

        /// <summary>
        /// Crafting-related skill (blacksmithing, alchemy, etc.).
        /// </summary>
        Crafting = 3,

        /// <summary>
        /// Social skill (persuasion, intimidation, charisma).
        /// </summary>
        Social = 4,

        /// <summary>
        /// Movement skill (speed, jump height, climbing).
        /// </summary>
        Movement = 5,

        /// <summary>
        /// Magic-related skill (spell power, mana efficiency).
        /// </summary>
        Magic = 6
    }
}
