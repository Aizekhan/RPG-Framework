// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Implementations/SkillManager.cs

using System.Collections.Generic;
using RPGFramework.ECS;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Manages skill learning, leveling, and querying for entities.
    /// Works with any component type that contains SkillProgressData.
    /// </summary>
    public class SkillManager : ISkillManager
    {
        private readonly IEntityManager _entityManager;
        private readonly ISkillTree _skillTree;

        public SkillManager(IEntityManager entityManager, ISkillTree skillTree)
        {
            _entityManager = entityManager;
            _skillTree = skillTree;
        }

        public bool LearnSkill(int entityId, string skillId, int characterLevel)
        {
            ISkill skill = _skillTree.GetSkill(skillId);
            if (skill == null)
                return false;

            // Get component with SkillProgressData
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return false;

            // Check if already learned
            if (progressData.HasSkill(skillId))
                return false;

            // Check prerequisites
            var progress = new SkillProgress(progressData);
            if (!_skillTree.CanLearnSkill(progress, characterLevel, skillId))
                return false;

            // Learn skill at level 1
            progressData.SetSkillLevel(skillId, 1);
            UpdateSkillProgressData(entityId, progressData);

            return true;
        }

        public bool IncreaseSkillLevel(int entityId, string skillId)
        {
            ISkill skill = _skillTree.GetSkill(skillId);
            if (skill == null)
                return false;

            if (!TryGetSkillProgressData(entityId, out var progressData))
                return false;

            int currentLevel = progressData.GetSkillLevel(skillId);
            if (currentLevel == 0)
                return false; // Not learned yet

            if (currentLevel >= skill.MaxLevel)
                return false; // Already at max level

            progressData.SetSkillLevel(skillId, currentLevel + 1);
            UpdateSkillProgressData(entityId, progressData);

            return true;
        }

        public void SetSkillLevel(int entityId, string skillId, int level)
        {
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return;

            progressData.SetSkillLevel(skillId, level);
            UpdateSkillProgressData(entityId, progressData);
        }

        public int GetSkillLevel(int entityId, string skillId)
        {
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return 0;

            return progressData.GetSkillLevel(skillId);
        }

        public bool CanLearnSkill(int entityId, string skillId, int characterLevel)
        {
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return false;

            var progress = new SkillProgress(progressData);
            return _skillTree.CanLearnSkill(progress, characterLevel, skillId);
        }

        public IReadOnlyList<string> GetLearnedSkills(int entityId)
        {
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return new List<string>();

            return progressData.SkillLevels != null
                ? new List<string>(progressData.SkillLevels.Keys)
                : new List<string>();
        }

        public ISkillProgress GetSkillProgress(int entityId)
        {
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return new SkillProgress(SkillProgressData.CreateEmpty());

            return new SkillProgress(progressData);
        }

        public void ResetSkills(int entityId)
        {
            if (!TryGetSkillProgressData(entityId, out var progressData))
                return;

            progressData.SkillLevels?.Clear();
            UpdateSkillProgressData(entityId, progressData);
        }

        // Helper methods - these would be implemented by game-specific adapters
        // For now, we'll provide a basic implementation that works with a generic component

        private bool TryGetSkillProgressData(int entityId, out SkillProgressData data)
        {
            // This is a simplified version - actual implementation would depend on component structure
            // Game-layer adapter would override this to work with specific component types
            data = SkillProgressData.CreateEmpty();
            return false;
        }

        private void UpdateSkillProgressData(int entityId, SkillProgressData data)
        {
            // This is a simplified version - actual implementation would depend on component structure
            // Game-layer adapter would override this to work with specific component types
        }
    }
}
