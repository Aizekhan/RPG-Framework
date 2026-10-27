// Packages/com.aizekhan.rpg-framework/Skills/Runtime/Implementations/SkillManager.cs

using System.Collections.Generic;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.Skills
{
    /// <summary>
    /// Manages skill learning, leveling, and querying for entities.
    /// Works with any component type that contains SkillProgressData.
    /// With an event publisher, raises SkillLearned and SkillLevelUp (TimeStamp 0: the manager keeps no clock).
    /// </summary>
    public class SkillManager : ISkillManager
    {
        private readonly IEntityManager _entityManager;
        private readonly ISkillTree _skillTree;
        private readonly IEventPublisher _events;

        public SkillManager(IEntityManager entityManager, ISkillTree skillTree, IEventPublisher events = null)
        {
            _events = events;
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

            _events?.Publish(new SkillLearnedEvent { EntityId = entityId, SkillId = skillId, Level = 1 });
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

            _events?.Publish(new SkillLevelUpEvent
            {
                EntityId = entityId, SkillId = skillId, OldLevel = currentLevel, NewLevel = currentLevel + 1
            });
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

        /// <summary>
        /// Reads an entity's skill data. The base version stores nothing and returns false;
        /// a game-layer subclass overrides this and <see cref="UpdateSkillProgressData"/> to use its own component.
        /// </summary>
        protected virtual bool TryGetSkillProgressData(int entityId, out SkillProgressData data)
        {
            // This is a simplified version - actual implementation would depend on component structure
            // Game-layer adapter would override this to work with specific component types
            data = SkillProgressData.CreateEmpty();
            return false;
        }

        /// <summary>
        /// Writes an entity's skill data back. The base version does nothing.
        /// </summary>
        protected virtual void UpdateSkillProgressData(int entityId, SkillProgressData data)
        {
            // This is a simplified version - actual implementation would depend on component structure
            // Game-layer adapter would override this to work with specific component types
        }
    }
}
