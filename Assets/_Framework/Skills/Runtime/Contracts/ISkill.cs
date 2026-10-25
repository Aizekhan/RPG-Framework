// Assets/_Framework/Skills/Runtime/Contracts/ISkill.cs

using System.Collections.Generic;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Represents a skill definition in the RPG system.
    /// Skills can be passive (stat bonuses) or active (unlock abilities).
    /// </summary>
    public interface ISkill
    {
        /// <summary>
        /// Unique identifier for the skill.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the skill.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what the skill does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Maximum level this skill can reach.
        /// </summary>
        int MaxLevel { get; }

        /// <summary>
        /// Type of skill (Passive, Active, Combat, etc.).
        /// </summary>
        SkillType Type { get; }

        /// <summary>
        /// Prerequisites required to learn this skill.
        /// </summary>
        IReadOnlyList<ISkillPrerequisite> Prerequisites { get; }

        /// <summary>
        /// Stat bonuses granted by this skill per level.
        /// </summary>
        IReadOnlyList<ISkillBonus> Bonuses { get; }
    }

    /// <summary>
    /// Represents a prerequisite for learning a skill.
    /// </summary>
    public interface ISkillPrerequisite
    {
        /// <summary>
        /// Required skill ID (if prerequisite is another skill).
        /// Null if only character level is required.
        /// </summary>
        string RequiredSkillId { get; }

        /// <summary>
        /// Required level of the prerequisite skill.
        /// 0 if not applicable.
        /// </summary>
        int RequiredSkillLevel { get; }

        /// <summary>
        /// Required character level to learn this skill.
        /// 0 if not applicable.
        /// </summary>
        int RequiredCharacterLevel { get; }

        /// <summary>
        /// Checks if the prerequisite is satisfied for the given skill progress and character level.
        /// </summary>
        bool IsSatisfied(ISkillProgress progress, int characterLevel);
    }

    /// <summary>
    /// Represents a stat bonus granted by a skill.
    /// </summary>
    public interface ISkillBonus
    {
        /// <summary>
        /// ID of the stat to modify (e.g., "HP", "AttackPower", "Defense").
        /// </summary>
        string StatId { get; }

        /// <summary>
        /// Bonus amount per skill level.
        /// </summary>
        float BonusPerLevel { get; }

        /// <summary>
        /// Type of bonus (Additive, Multiplicative, Override).
        /// </summary>
        BonusType Type { get; }

        /// <summary>
        /// Calculates the total bonus for the given skill level.
        /// </summary>
        float CalculateBonus(int skillLevel);
    }
}
