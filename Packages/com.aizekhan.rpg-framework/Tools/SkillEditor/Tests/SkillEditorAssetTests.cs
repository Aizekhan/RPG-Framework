using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.ECS;
using RPGFramework.Skills;
using UnityEngine;

namespace RPGFramework.Tools.SkillEditor.Tests
{
    /// <summary>
    /// Skills and skill trees made as assets become exactly the skills the game runs on: the real
    /// <see cref="SkillManager"/> learns them and holds back a skill until its prerequisites are learned. The checks
    /// catch every way a designer can make a skill impossible to learn: missing or duplicate ids, empty slots,
    /// prerequisites outside the tree, loops, a skill needing itself, and levels a skill can never reach.
    /// </summary>
    public sealed class SkillEditorAssetTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _made)
                Object.DestroyImmediate(asset);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _made.Add(asset);
            return asset;
        }

        private SkillAsset Skill(string id, int maxLevel = 1, params SkillAsset[] prerequisites)
        {
            var skill = Make<SkillAsset>();
            skill.skillId = id;
            skill.skillName = id;
            skill.maxLevel = maxLevel;
            skill.prerequisites = new SkillPrerequisiteEntry[prerequisites.Length];
            for (int i = 0; i < prerequisites.Length; i++)
                skill.prerequisites[i] = new SkillPrerequisiteEntry { skill = prerequisites[i], requiredLevel = 1 };
            return skill;
        }

        private SkillTreeAsset Tree(params SkillAsset[] skills)
        {
            var tree = Make<SkillTreeAsset>();
            tree.treeId = "warrior";
            tree.treeName = "Warrior";
            tree.skills = new List<SkillAsset>(skills);
            return tree;
        }

        // ---- A good tree ----

        [Test]
        public void AGoodTree_HasNoProblems()
        {
            var strike = Skill("strike", 3);
            var whirlwind = Skill("whirlwind", 1, strike);
            whirlwind.prerequisites[0].requiredLevel = 3;
            whirlwind.requiredCharacterLevel = 5;
            whirlwind.bonuses = new[] { new SkillBonusEntry { statId = "AttackPower", bonusPerLevel = 2f } };

            Assert.That(strike.Validate(), Is.Empty);
            Assert.That(whirlwind.Validate(), Is.Empty);
            Assert.That(Tree(strike, whirlwind).Validate(), Is.Empty);
        }

        [Test]
        public void ASkillAsset_BecomesTheSkillTheGameRuns()
        {
            var strike = Skill("strike", 3);
            var whirlwind = Skill("whirlwind", 2, strike);
            whirlwind.skillName = "Whirlwind";
            whirlwind.description = "Hits all around";
            whirlwind.skillType = SkillType.Combat;
            whirlwind.prerequisites[0].requiredLevel = 2;
            whirlwind.requiredCharacterLevel = 5;
            whirlwind.bonuses = new[]
            {
                new SkillBonusEntry { statId = "AttackPower", bonusPerLevel = 2f, bonusType = BonusType.Multiplicative }
            };

            ISkill skill = whirlwind.ToSkill();

            Assert.That(skill.Id, Is.EqualTo("whirlwind"));
            Assert.That(skill.Name, Is.EqualTo("Whirlwind"));
            Assert.That(skill.Description, Is.EqualTo("Hits all around"));
            Assert.That(skill.MaxLevel, Is.EqualTo(2));
            Assert.That(skill.Type, Is.EqualTo(SkillType.Combat));
            Assert.That(skill.Prerequisites.Count, Is.EqualTo(2));
            Assert.That(skill.Prerequisites[0].RequiredCharacterLevel, Is.EqualTo(5));
            Assert.That(skill.Prerequisites[1].RequiredSkillId, Is.EqualTo("strike"));
            Assert.That(skill.Prerequisites[1].RequiredSkillLevel, Is.EqualTo(2));
            Assert.That(skill.Bonuses.Count, Is.EqualTo(1));
            Assert.That(skill.Bonuses[0].StatId, Is.EqualTo("AttackPower"));
            Assert.That(skill.Bonuses[0].Type, Is.EqualTo(BonusType.Multiplicative));
            Assert.That(skill.Bonuses[0].CalculateBonus(2), Is.EqualTo(4f));
        }

        [Test]
        public void EmptySlots_AreLeftOutOfTheGameSkill()
        {
            var strike = Skill("strike");
            strike.prerequisites = new[] { new SkillPrerequisiteEntry(), null };
            strike.bonuses = new SkillBonusEntry[] { null };

            ISkill skill = strike.ToSkill();

            Assert.That(skill.Prerequisites, Is.Empty, "an empty prerequisite slot must not lock the skill");
            Assert.That(skill.Bonuses, Is.Empty);
        }

        [Test]
        public void TheRealSkillManager_LearnsTheTreeMadeAsAssets()
        {
            var strike = Skill("strike", 3);
            var whirlwind = Skill("whirlwind", 1, strike);
            whirlwind.prerequisites[0].requiredLevel = 2;
            var tree = Tree(strike, whirlwind);

            SkillTree runtimeTree = tree.ToSkillTree();
            Assert.That(runtimeTree.Id, Is.EqualTo("warrior"));
            Assert.That(runtimeTree.Skills.Count, Is.EqualTo(2));

            var entities = new EntityManager();
            var manager = new SkillManager(entities, runtimeTree);
            int hero = entities.CreateEntity();

            Assert.That(manager.LearnSkill(hero, "whirlwind", 1), Is.False, "strike not learned yet");
            Assert.That(manager.LearnSkill(hero, "strike", 1), Is.True, "a root skill is learned at once");
            Assert.That(manager.LearnSkill(hero, "whirlwind", 1), Is.False, "strike only at level 1");

            Assert.That(manager.IncreaseSkillLevel(hero, "strike"), Is.True);
            Assert.That(manager.LearnSkill(hero, "whirlwind", 1), Is.True);
            Assert.That(manager.GetSkillLevel(hero, "whirlwind"), Is.EqualTo(1));
        }

        [Test]
        public void RegisterInto_AddsTheSkillsToAnExistingTree()
        {
            var runtimeTree = new SkillTree("shared", "Shared", "");
            Tree(Skill("strike"), null, Skill("block")).RegisterInto(runtimeTree);

            Assert.That(runtimeTree.ContainsSkill("strike"), Is.True);
            Assert.That(runtimeTree.ContainsSkill("block"), Is.True);
            Assert.That(runtimeTree.Skills.Count, Is.EqualTo(2));
        }

        [Test]
        public void Tiers_PutRootsFirst_ThenTheSkillsBuiltOnThem()
        {
            var strike = Skill("strike");
            var block = Skill("block");
            var cleave = Skill("cleave", 1, strike);
            var whirlwind = Skill("whirlwind", 1, cleave, block);

            var tiers = Tree(whirlwind, cleave, strike, block).GetTiers();

            Assert.That(tiers.Count, Is.EqualTo(3));
            Assert.That(tiers[0], Is.EquivalentTo(new[] { strike, block }));
            Assert.That(tiers[1], Is.EquivalentTo(new[] { cleave }));
            Assert.That(tiers[2], Is.EquivalentTo(new[] { whirlwind }));
        }

        // ---- Tree checks ----

        [Test]
        public void ATreeWithoutAnId_IsReported()
        {
            var tree = Tree(Skill("strike"));
            tree.treeId = " ";

            Assert.That(tree.Validate(), Has.Some.Contains("Tree ID cannot be empty"));
        }

        [Test]
        public void ATreeWithoutSkills_IsReported()
        {
            Assert.That(Tree().Validate(), Has.Some.Contains("Tree has no skills"));
        }

        [Test]
        public void EmptySkillSlots_AreReported()
        {
            Assert.That(Tree(Skill("strike"), null).Validate(), Has.Some.Contains("1 empty skill slot"));
        }

        [Test]
        public void DuplicateSkillIds_AreReported()
        {
            var errors = Tree(Skill("strike"), Skill("strike")).Validate();

            Assert.That(errors, Has.Some.Contains("Duplicate skill ID: strike"));
        }

        [Test]
        public void ASkillsOwnProblems_ShowInTheTree()
        {
            var nameless = Skill("strike");
            nameless.skillName = "";

            Assert.That(Tree(nameless).Validate(), Has.Some.Contains("Skill 'strike': Skill Name cannot be empty"));
        }

        [Test]
        public void APrerequisiteOutsideTheTree_IsReported()
        {
            var outsider = Skill("fireball");
            var errors = Tree(Skill("strike", 1, outsider)).Validate();

            Assert.That(errors, Has.Some.Contains("prerequisite 'fireball' is not in this tree"));
        }

        [Test]
        public void APrerequisiteLoop_IsReported_ForEverySkillInOrAfterIt()
        {
            var a = Skill("a");
            var b = Skill("b", 1, a);
            a.prerequisites = new[] { new SkillPrerequisiteEntry { skill = b, requiredLevel = 1 } };
            var c = Skill("c", 1, a);
            var root = Skill("root");
            var tree = Tree(a, b, c, root);

            var errors = tree.Validate();

            Assert.That(errors, Has.Some.Contains("Skill 'a': its prerequisites lead into a loop"));
            Assert.That(errors, Has.Some.Contains("Skill 'b': its prerequisites lead into a loop"));
            Assert.That(errors, Has.Some.Contains("Skill 'c': its prerequisites lead into a loop"));
            Assert.That(errors, Has.None.Contains("Skill 'root'"));
            Assert.That(tree.GetTiers().Count, Is.EqualTo(1));
            Assert.That(tree.GetTiers()[0], Is.EquivalentTo(new[] { root }), "skills in a loop have no tier");
        }

        // ---- Skill checks ----

        [Test]
        public void ASkillRequiringItself_IsReported()
        {
            var strike = Skill("strike");
            strike.prerequisites = new[] { new SkillPrerequisiteEntry { skill = strike, requiredLevel = 1 } };

            Assert.That(strike.Validate(), Has.Some.Contains("requires itself"));
            Assert.That(Tree(strike).Validate(), Has.Some.Contains("Skill 'strike': its prerequisites lead into a loop"));
        }

        [Test]
        public void APrerequisiteLevelAboveThatSkillsMaximum_IsReported()
        {
            var strike = Skill("strike", 3);
            var whirlwind = Skill("whirlwind", 1, strike);
            whirlwind.prerequisites[0].requiredLevel = 4;

            Assert.That(whirlwind.Validate(), Has.Some.Contains("only goes up to 3"));
        }

        [Test]
        public void APrerequisiteLevelBelowOne_IsReported()
        {
            var whirlwind = Skill("whirlwind", 1, Skill("strike"));
            whirlwind.prerequisites[0].requiredLevel = 0;

            Assert.That(whirlwind.Validate(), Has.Some.Contains("required level of at least 1"));
        }

        [Test]
        public void AnEmptyPrerequisiteSlot_IsReported()
        {
            var strike = Skill("strike");
            strike.prerequisites = new[] { new SkillPrerequisiteEntry() };

            Assert.That(strike.Validate(), Has.Some.Contains("Prerequisite 1 is empty"));
        }

        [Test]
        public void ABonusWithoutAStat_IsReported()
        {
            var strike = Skill("strike");
            strike.bonuses = new[] { new SkillBonusEntry { statId = "" } };

            Assert.That(strike.Validate(), Has.Some.Contains("Bonus 1 has no stat"));
        }

        [Test]
        public void MissingIdNameOrMaxLevel_AreReported()
        {
            var broken = Skill(" ", 0);
            broken.skillName = "";
            broken.requiredCharacterLevel = -1;

            var errors = broken.Validate();

            Assert.That(errors, Has.Some.Contains("Skill ID cannot be empty"));
            Assert.That(errors, Has.Some.Contains("Skill Name cannot be empty"));
            Assert.That(errors, Has.Some.Contains("Max Level must be at least 1"));
            Assert.That(errors, Has.Some.Contains("Required Character Level cannot be negative"));
        }
    }
}
