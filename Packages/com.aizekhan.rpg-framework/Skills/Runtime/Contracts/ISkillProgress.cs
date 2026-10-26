// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Contracts/ISkillProgress.cs

using System.Collections.Generic;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Represents an entity's progress in learning skills.
    /// </summary>
    public interface ISkillProgress
    {
        /// <summary>
        /// Gets the level of a specific skill.
        /// Returns 0 if the skill has not been learned.
        /// </summary>
        int GetSkillLevel(string skillId);

        /// <summary>
        /// Checks if the entity has learned a specific skill.
        /// </summary>
        bool HasSkill(string skillId);

        /// <summary>
        /// Gets all skills that have been learned by the entity.
        /// </summary>
        IReadOnlyList<string> LearnedSkills { get; }

        /// <summary>
        /// Gets the total number of skills learned.
        /// </summary>
        int TotalSkillsLearned { get; }
    }
}
