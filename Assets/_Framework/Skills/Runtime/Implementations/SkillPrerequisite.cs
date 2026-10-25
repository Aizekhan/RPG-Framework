// Assets/_Framework/Skills/Runtime/Implementations/SkillPrerequisite.cs

namespace RPGFramework.Skills
{
    /// <summary>
    /// Implementation of a skill prerequisite.
    /// </summary>
    public class SkillPrerequisite : ISkillPrerequisite
    {
        public string RequiredSkillId { get; private set; }
        public int RequiredSkillLevel { get; private set; }
        public int RequiredCharacterLevel { get; private set; }

        public SkillPrerequisite(SkillPrerequisiteData data)
        {
            RequiredSkillId = data.RequiredSkillId;
            RequiredSkillLevel = data.RequiredSkillLevel;
            RequiredCharacterLevel = data.RequiredCharacterLevel;
        }

        public SkillPrerequisite(string requiredSkillId, int requiredSkillLevel, int requiredCharacterLevel)
        {
            RequiredSkillId = requiredSkillId;
            RequiredSkillLevel = requiredSkillLevel;
            RequiredCharacterLevel = requiredCharacterLevel;
        }

        public bool IsSatisfied(ISkillProgress progress, int characterLevel)
        {
            // Check character level requirement
            if (RequiredCharacterLevel > 0 && characterLevel < RequiredCharacterLevel)
                return false;

            // Check skill requirement
            if (!string.IsNullOrEmpty(RequiredSkillId))
            {
                int skillLevel = progress.GetSkillLevel(RequiredSkillId);
                if (skillLevel < RequiredSkillLevel)
                    return false;
            }

            return true;
        }
    }
}
