// Assets/_Framework/Skills/Runtime/Contracts/ISkillManager.cs

using System.Collections.Generic;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Manages skill learning, leveling, and querying for entities.
    /// </summary>
    public interface ISkillManager
    {
        /// <summary>
        /// Attempts to learn a skill for an entity.
        /// Returns true if successful, false if prerequisites are not met or skill is already at max level.
        /// </summary>
        bool LearnSkill(int entityId, string skillId, int characterLevel);

        /// <summary>
        /// Increases the level of a skill for an entity.
        /// Returns true if successful, false if skill is not learned or already at max level.
        /// </summary>
        bool IncreaseSkillLevel(int entityId, string skillId);

        /// <summary>
        /// Sets the level of a skill directly (for loading saved data).
        /// </summary>
        void SetSkillLevel(int entityId, string skillId, int level);

        /// <summary>
        /// Gets the current level of a skill for an entity.
        /// Returns 0 if the skill has not been learned.
        /// </summary>
        int GetSkillLevel(int entityId, string skillId);

        /// <summary>
        /// Checks if an entity can learn a specific skill.
        /// </summary>
        bool CanLearnSkill(int entityId, string skillId, int characterLevel);

        /// <summary>
        /// Gets all skills learned by an entity.
        /// </summary>
        IReadOnlyList<string> GetLearnedSkills(int entityId);

        /// <summary>
        /// Gets the skill progress for an entity.
        /// </summary>
        ISkillProgress GetSkillProgress(int entityId);

        /// <summary>
        /// Removes all skills from an entity (for testing or reset).
        /// </summary>
        void ResetSkills(int entityId);
    }
}
