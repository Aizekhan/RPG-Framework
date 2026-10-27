using System;
using NUnit.Framework;

namespace RPGFramework.Skills.Tests
{
    /// <summary>
    /// A tree refuses a second skill with the same id instead of dropping it, and a prerequisite that names a skill
    /// without a level still needs that skill to be known.
    /// </summary>
    public sealed class SkillTreeRulesTests
    {
        [Test]
        public void ASecondSkillWithTheSameId_IsRefused()
        {
            var tree = new SkillTree("warrior", "Warrior", "");
            tree.AddSkill(new Skill("strike", "Strike", "", 3, SkillType.Active));

            Assert.Throws<ArgumentException>(() => tree.AddSkill(new Skill("strike", "Other Strike", "", 1, SkillType.Active)));
            Assert.That(tree.GetSkill("strike").Name, Is.EqualTo("Strike"));
            Assert.That(tree.Skills.Count, Is.EqualTo(1));
        }

        [Test]
        public void APrerequisiteWithoutALevel_StillNeedsTheSkillKnown()
        {
            var prerequisite = new SkillPrerequisite("strike", 0, 0);
            var progress = SkillProgressData.CreateEmpty();

            Assert.That(prerequisite.IsSatisfied(new SkillProgress(progress), 1), Is.False);

            progress.SetSkillLevel("strike", 1);
            Assert.That(prerequisite.IsSatisfied(new SkillProgress(progress), 1), Is.True);
        }
    }
}
