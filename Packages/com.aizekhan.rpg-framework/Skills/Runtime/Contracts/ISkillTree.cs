// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Contracts/ISkillTree.cs

using System.Collections.Generic;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Represents a collection of related skills (e.g., Warrior skill tree, Mage skill tree).
    /// </summary>
    public interface ISkillTree
    {
        /// <summary>
        /// Unique identifier for the skill tree.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the skill tree.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of the skill tree.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// All skills in this tree.
        /// </summary>
        IReadOnlyList<ISkill> Skills { get; }

        /// <summary>
        /// Checks if the skill tree contains a specific skill.
        /// </summary>
        bool ContainsSkill(string skillId);

        /// <summary>
        /// Gets a skill by its ID.
        /// Returns null if the skill is not in this tree.
        /// </summary>
        ISkill GetSkill(string skillId);

        /// <summary>
        /// Checks if an entity can learn a specific skill based on prerequisites.
        /// </summary>
        bool CanLearnSkill(ISkillProgress progress, int characterLevel, string skillId);

        /// <summary>
        /// Gets all skills that are available to learn (prerequisites satisfied).
        /// </summary>
        IReadOnlyList<ISkill> GetAvailableSkills(ISkillProgress progress, int characterLevel);
    }
}
