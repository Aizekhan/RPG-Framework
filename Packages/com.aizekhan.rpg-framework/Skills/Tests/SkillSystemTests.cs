// Packages/com.aizekhan.rpg-framework/Skills/Tests/SkillSystemTests.cs

using NUnit.Framework;
using RPGFramework.Skills;
using System.Collections.Generic;

namespace RPGFramework.Skills.Tests
{
    [TestFixture]
    public class SkillSystemTests
    {
        private ISkill _testSkill;
        private ISkillTree _skillTree;

        [SetUp]
        public void Setup()
        {
            // Create a test skill with prerequisites and bonuses
            var skillData = SkillData.Create(
                "skill_warrior_strength",
                "Warrior Strength",
                "Increases physical damage",
                10, // Max level
                SkillType.Passive
            );

            skillData.Prerequisites = new List<SkillPrerequisiteData>
            {
                SkillPrerequisiteData.CreateCharacterLevelRequirement(5)
            };

            skillData.Bonuses = new List<SkillBonusData>
            {
                SkillBonusData.Create("AttackPower", 5f, BonusType.Additive)
            };

            _testSkill = new Skill(skillData);

            // Create skill tree
            _skillTree = new SkillTree("tree_warrior", "Warrior Skills", "Combat skills for warriors");
            ((SkillTree)_skillTree).AddSkill(_testSkill);
        }

        #region Skill Tests

        [Test]
        public void Skill_HasCorrectProperties()
        {
            Assert.That(_testSkill.Id, Is.EqualTo("skill_warrior_strength"));
            Assert.That(_testSkill.Name, Is.EqualTo("Warrior Strength"));
            Assert.That(_testSkill.MaxLevel, Is.EqualTo(10));
            Assert.That(_testSkill.Type, Is.EqualTo(SkillType.Passive));
        }

        [Test]
        public void Skill_HasPrerequisites()
        {
            Assert.That(_testSkill.Prerequisites, Is.Not.Null);
            Assert.That(_testSkill.Prerequisites.Count, Is.EqualTo(1));
            Assert.That(_testSkill.Prerequisites[0].RequiredCharacterLevel, Is.EqualTo(5));
        }

        [Test]
        public void Skill_HasBonuses()
        {
            Assert.That(_testSkill.Bonuses, Is.Not.Null);
            Assert.That(_testSkill.Bonuses.Count, Is.EqualTo(1));
            Assert.That(_testSkill.Bonuses[0].StatId, Is.EqualTo("AttackPower"));
            Assert.That(_testSkill.Bonuses[0].BonusPerLevel, Is.EqualTo(5f));
        }

        #endregion

        #region SkillProgress Tests

        [Test]
        public void SkillProgress_EmptyByDefault()
        {
            var data = SkillProgressData.CreateEmpty();
            var progress = new SkillProgress(data);

            Assert.That(progress.TotalSkillsLearned, Is.EqualTo(0));
            Assert.That(progress.LearnedSkills.Count, Is.EqualTo(0));
        }

        [Test]
        public void SkillProgress_GetSkillLevel_ReturnsZeroForUnlearnedSkill()
        {
            var data = SkillProgressData.CreateEmpty();
            var progress = new SkillProgress(data);

            Assert.That(progress.GetSkillLevel("unknown_skill"), Is.EqualTo(0));
        }

        [Test]
        public void SkillProgress_HasSkill_ReturnsFalseForUnlearnedSkill()
        {
            var data = SkillProgressData.CreateEmpty();
            var progress = new SkillProgress(data);

            Assert.That(progress.HasSkill("unknown_skill"), Is.False);
        }

        [Test]
        public void SkillProgressData_SetSkillLevel_SetsLevel()
        {
            var data = SkillProgressData.CreateEmpty();
            data.SetSkillLevel("skill_test", 3);

            Assert.That(data.GetSkillLevel("skill_test"), Is.EqualTo(3));
            Assert.That(data.HasSkill("skill_test"), Is.True);
        }

        [Test]
        public void SkillProgressData_RemoveSkill_RemovesSkill()
        {
            var data = SkillProgressData.CreateEmpty();
            data.SetSkillLevel("skill_test", 3);
            data.RemoveSkill("skill_test");

            Assert.That(data.HasSkill("skill_test"), Is.False);
        }

        [Test]
        public void SkillProgressData_CreateWithSkills_InitializesSkillsAtLevel1()
        {
            var skills = new List<string> { "skill_1", "skill_2", "skill_3" };
            var data = SkillProgressData.CreateWithSkills(skills);

            Assert.That(data.GetSkillLevel("skill_1"), Is.EqualTo(1));
            Assert.That(data.GetSkillLevel("skill_2"), Is.EqualTo(1));
            Assert.That(data.GetSkillLevel("skill_3"), Is.EqualTo(1));
        }

        #endregion

        #region SkillPrerequisite Tests

        [Test]
        public void SkillPrerequisite_CharacterLevel_IsSatisfied()
        {
            var prereq = new SkillPrerequisite(null, 0, 10);
            var progress = new SkillProgress(SkillProgressData.CreateEmpty());

            Assert.That(prereq.IsSatisfied(progress, 10), Is.True);
            Assert.That(prereq.IsSatisfied(progress, 15), Is.True);
            Assert.That(prereq.IsSatisfied(progress, 5), Is.False);
        }

        [Test]
        public void SkillPrerequisite_SkillLevel_IsSatisfied()
        {
            var data = SkillProgressData.CreateEmpty();
            data.SetSkillLevel("skill_basic", 3);
            var progress = new SkillProgress(data);

            var prereq = new SkillPrerequisite("skill_basic", 2, 0);

            Assert.That(prereq.IsSatisfied(progress, 1), Is.True); // Has skill at level 3
            Assert.That(prereq.IsSatisfied(progress, 1), Is.True); // Character level doesn't matter
        }

        [Test]
        public void SkillPrerequisite_SkillLevel_NotSatisfied()
        {
            var data = SkillProgressData.CreateEmpty();
            data.SetSkillLevel("skill_basic", 1);
            var progress = new SkillProgress(data);

            var prereq = new SkillPrerequisite("skill_basic", 3, 0);

            Assert.That(prereq.IsSatisfied(progress, 1), Is.False);
        }

        [Test]
        public void SkillPrerequisite_Combined_BothMustBeSatisfied()
        {
            var data = SkillProgressData.CreateEmpty();
            data.SetSkillLevel("skill_basic", 3);
            var progress = new SkillProgress(data);

            var prereq = new SkillPrerequisite("skill_basic", 2, 10);

            Assert.That(prereq.IsSatisfied(progress, 15), Is.True); // Both satisfied
            Assert.That(prereq.IsSatisfied(progress, 5), Is.False); // Character level too low
        }

        #endregion

        #region SkillBonus Tests

        [Test]
        public void SkillBonus_CalculateBonus_Additive()
        {
            var bonus = new SkillBonus("HP", 10f, BonusType.Additive);

            Assert.That(bonus.CalculateBonus(1), Is.EqualTo(10f));
            Assert.That(bonus.CalculateBonus(5), Is.EqualTo(50f));
            Assert.That(bonus.CalculateBonus(10), Is.EqualTo(100f));
        }

        [Test]
        public void SkillBonus_CalculateBonus_Multiplicative()
        {
            var bonus = new SkillBonus("Damage", 0.1f, BonusType.Multiplicative);

            Assert.That(bonus.CalculateBonus(1), Is.EqualTo(0.1f));
            Assert.That(bonus.CalculateBonus(5), Is.EqualTo(0.5f));
        }

        #endregion

        #region SkillTree Tests

        [Test]
        public void SkillTree_HasCorrectProperties()
        {
            Assert.That(_skillTree.Id, Is.EqualTo("tree_warrior"));
            Assert.That(_skillTree.Name, Is.EqualTo("Warrior Skills"));
            Assert.That(_skillTree.Description, Is.EqualTo("Combat skills for warriors"));
        }

        [Test]
        public void SkillTree_ContainsSkill_ReturnsTrue()
        {
            Assert.That(_skillTree.ContainsSkill("skill_warrior_strength"), Is.True);
        }

        [Test]
        public void SkillTree_ContainsSkill_ReturnsFalse()
        {
            Assert.That(_skillTree.ContainsSkill("unknown_skill"), Is.False);
        }

        [Test]
        public void SkillTree_GetSkill_ReturnsSkill()
        {
            var skill = _skillTree.GetSkill("skill_warrior_strength");

            Assert.That(skill, Is.Not.Null);
            Assert.That(skill.Id, Is.EqualTo("skill_warrior_strength"));
        }

        [Test]
        public void SkillTree_GetSkill_ReturnsNullForUnknownSkill()
        {
            var skill = _skillTree.GetSkill("unknown_skill");

            Assert.That(skill, Is.Null);
        }

        [Test]
        public void SkillTree_CanLearnSkill_PrerequisitesNotMet()
        {
            var data = SkillProgressData.CreateEmpty();
            var progress = new SkillProgress(data);

            // Character level 3, but skill requires level 5
            Assert.That(_skillTree.CanLearnSkill(progress, 3, "skill_warrior_strength"), Is.False);
        }

        [Test]
        public void SkillTree_CanLearnSkill_PrerequisitesMet()
        {
            var data = SkillProgressData.CreateEmpty();
            var progress = new SkillProgress(data);

            // Character level 5 - meets requirement
            Assert.That(_skillTree.CanLearnSkill(progress, 5, "skill_warrior_strength"), Is.True);
        }

        [Test]
        public void SkillTree_CanLearnSkill_AlreadyAtMaxLevel()
        {
            var data = SkillProgressData.CreateEmpty();
            data.SetSkillLevel("skill_warrior_strength", 10); // Max level
            var progress = new SkillProgress(data);

            Assert.That(_skillTree.CanLearnSkill(progress, 10, "skill_warrior_strength"), Is.False);
        }

        [Test]
        public void SkillTree_GetAvailableSkills_ReturnsOnlyAvailableSkills()
        {
            // Add another skill with higher requirements
            var advancedSkillData = SkillData.Create(
                "skill_advanced",
                "Advanced Skill",
                "Requires high level",
                5,
                SkillType.Active
            );
            advancedSkillData.Prerequisites = new List<SkillPrerequisiteData>
            {
                SkillPrerequisiteData.CreateCharacterLevelRequirement(20)
            };
            ((SkillTree)_skillTree).AddSkill(new Skill(advancedSkillData));

            var data = SkillProgressData.CreateEmpty();
            var progress = new SkillProgress(data);

            // At character level 10, only the first skill should be available
            var available = _skillTree.GetAvailableSkills(progress, 10);

            Assert.That(available.Count, Is.EqualTo(1));
            Assert.That(available[0].Id, Is.EqualTo("skill_warrior_strength"));
        }

        #endregion
    }
}
