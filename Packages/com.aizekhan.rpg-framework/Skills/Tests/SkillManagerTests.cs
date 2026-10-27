using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;

namespace RPGFramework.Skills.Tests
{
    /// <summary>
    /// <see cref="SkillManager"/> works on its own: skills are stored on the entity as a <see cref="SkillProgressData"/>
    /// component, so a character learns a skill, raises it up to its maximum, needs its prerequisites, and keeps each
    /// character's skills apart. Entities that do not exist learn nothing.
    /// </summary>
    public sealed class SkillManagerTests
    {
        private const string Strike = "strike";
        private const string Whirlwind = "whirlwind"; // needs strike 2 and character level 5

        private EntityManager _entities;
        private SkillManager _skills;
        private int _hero;

        [SetUp]
        public void SetUp()
        {
            var tree = new SkillTree("warrior", "Warrior", "Blades");
            tree.AddSkill(new Skill(Strike, "Strike", "A hard hit", 3, SkillType.Active));
            var whirlwind = SkillData.Create(Whirlwind, "Whirlwind", "Hits all around", 1, SkillType.Active);
            whirlwind.Prerequisites = new List<SkillPrerequisiteData>
            {
                SkillPrerequisiteData.CreateSkillRequirement(Strike, 2),
                SkillPrerequisiteData.CreateCharacterLevelRequirement(5)
            };
            tree.AddSkill(new Skill(whirlwind));

            _entities = new EntityManager();
            _skills = new SkillManager(_entities, tree);
            _hero = _entities.CreateEntity();
        }

        [Test]
        public void Learning_StoresTheSkillOnTheEntity()
        {
            Assert.That(_skills.LearnSkill(_hero, Strike, characterLevel: 1), Is.True);

            Assert.That(_skills.GetSkillLevel(_hero, Strike), Is.EqualTo(1));
            Assert.That(_skills.GetLearnedSkills(_hero), Is.EqualTo(new[] { Strike }));
            Assert.That(_entities.GetComponent<SkillProgressData>(_hero).GetSkillLevel(Strike), Is.EqualTo(1));
            Assert.That(_skills.LearnSkill(_hero, Strike, 1), Is.False, "already learned");
        }

        [Test]
        public void Raising_StopsAtTheSkillsMaximum()
        {
            _skills.LearnSkill(_hero, Strike, 1);

            Assert.That(_skills.IncreaseSkillLevel(_hero, Strike), Is.True);
            Assert.That(_skills.IncreaseSkillLevel(_hero, Strike), Is.True);
            Assert.That(_skills.IncreaseSkillLevel(_hero, Strike), Is.False);
            Assert.That(_skills.GetSkillLevel(_hero, Strike), Is.EqualTo(3));
        }

        [Test]
        public void ASkillNeedsItsPrerequisites()
        {
            Assert.That(_skills.CanLearnSkill(_hero, Whirlwind, 5), Is.False, "strike not learned");
            _skills.LearnSkill(_hero, Strike, 1);
            Assert.That(_skills.LearnSkill(_hero, Whirlwind, 5), Is.False, "strike only at 1");
            _skills.IncreaseSkillLevel(_hero, Strike);
            Assert.That(_skills.LearnSkill(_hero, Whirlwind, 4), Is.False, "character level too low");

            Assert.That(_skills.LearnSkill(_hero, Whirlwind, 5), Is.True);
            Assert.That(_skills.GetSkillProgress(_hero).GetSkillLevel(Whirlwind), Is.EqualTo(1));
        }

        [Test]
        public void EachCharacter_KeepsItsOwnSkills()
        {
            int friend = _entities.CreateEntity();
            _skills.LearnSkill(_hero, Strike, 1);

            Assert.That(_skills.GetSkillLevel(friend, Strike), Is.EqualTo(0));
            Assert.That(_skills.LearnSkill(friend, Strike, 1), Is.True);
            _skills.ResetSkills(_hero);

            Assert.That(_skills.GetLearnedSkills(_hero), Is.Empty);
            Assert.That(_skills.GetSkillLevel(friend, Strike), Is.EqualTo(1));
        }

        [Test]
        public void SettingALevel_RestoresSavedSkills()
        {
            _skills.SetSkillLevel(_hero, Strike, 2);

            Assert.That(_skills.GetSkillLevel(_hero, Strike), Is.EqualTo(2));
        }

        [Test]
        public void AnEntityThatDoesNotExist_LearnsNothing()
        {
            Assert.That(_skills.LearnSkill(999, Strike, 1), Is.False);
            Assert.That(_skills.GetSkillLevel(999, Strike), Is.EqualTo(0));
            _skills.SetSkillLevel(999, Strike, 2); // ignored, does not throw
            Assert.That(_entities.Exists(999), Is.False);
        }
    }
}
