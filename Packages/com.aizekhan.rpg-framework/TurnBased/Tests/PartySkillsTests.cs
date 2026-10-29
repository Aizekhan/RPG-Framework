using System.Linq;
using NUnit.Framework;
using RPGFramework.Skills;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// The party's skill tree: points buy skill levels, the bonuses outlast battles, a save keeps them and a new run
    /// starts over.
    /// </summary>
    public sealed class PartySkillsTests
    {
        private const string Might = "might", Fireward = "fireward";
        private TurnBasedTestGame _game;

        [SetUp]
        public void SetUp()
        {
            _game = new TurnBasedTestGame();
            _game.EnemiesDefend();
            _game.Settings.StartingSkillPoints = 1;
            var tree = new SkillTree("party", "Party", "");
            var might = new Skill(Might, "Might", "", 2, SkillType.Combat);
            might.AddBonus(new SkillBonus("AttackPower", 4f, BonusType.Additive));
            tree.AddSkill(might);
            var fireward = new Skill(Fireward, "Fireward", "", 1, SkillType.Magic);
            fireward.AddBonus(new SkillBonus("resist:fire", 0.5f, BonusType.Additive));
            fireward.AddBonus(new SkillBonus("MaxHP", 0.1f, BonusType.Multiplicative));
            tree.AddSkill(fireward);
            _game.Content.SkillTree = tree;
        }

        [TearDown]
        public void TearDown() => _game.Dispose();

        [Test]
        public void Points_BuyBonuses_ThatOutlastBattles()
        {
            var world = _game.Start();
            var knight = _game.Member(Knight);
            var skills = world.Skills;
            Assert.That(skills.PointsOf(knight.Id), Is.EqualTo(1));

            Assert.That(skills.Learn(knight.Id, Might), Is.True);
            Assert.That(knight.Attack, Is.EqualTo(24f));
            Assert.That(skills.WhyNot(knight.Id, Might), Is.EqualTo("No skill points left"));
            skills.GrantPoints(knight.Id, 1);
            Assert.That(skills.Learn(knight.Id, Fireward), Is.True);
            Assert.That(knight.MaxHealth, Is.EqualTo(132), "120 and a tenth");
            Assert.That(world.Resistances.Of(knight.Id, "fire"), Is.EqualTo(0.5f).Within(1e-4f));

            var battle = world.StartBattle();
            DefendUntil(battle, Knight);
            bool defending = battle.Enemies[0].IsDefending;
            var hit = Expect(battle.Perform(BattleAction.Attack(battle.Enemies[0].Id)));
            Assert.That(hit.Targets.Single().Damage, Is.EqualTo(defending ? 11 : 22), "24 less half of 4 (halved when it defends)");
            PlayToTheEnd(battle);
            Assert.That(knight.Attack, Is.EqualTo(knight.BaseStat(CombatStat.Attack) + 4f), "the battle's end (and a level-up) leaves learned skills");
        }

        [Test]
        public void ASave_KeepsTheSkills_AndANewRunStartsOver()
        {
            var world = _game.Start();
            var mage = _game.Member(Mage);
            world.Skills.Learn(mage.Id, Might);
            world.Skills.GrantPoints(mage.Id, 2);
            var saved = world.CaptureProgress();
            Assert.That(saved.Members.Single(m => m.CharacterId == Mage).Skills.Single().SkillId, Is.EqualTo(Might));

            world.Skills.Reset(mage.Id);
            Assert.That(mage.Attack, Is.EqualTo(6f));
            world.RestoreProgress(saved);
            Assert.That(mage.Attack, Is.EqualTo(10f));
            Assert.That(world.Skills.PointsOf(mage.Id), Is.EqualTo(2), "1 + 2 granted, 1 spent");

            world.Restart();
            Assert.That(world.Skills.LevelOf(mage.Id, Might), Is.EqualTo(0));
            Assert.That(mage.Attack, Is.EqualTo(6f));
            Assert.That(world.Skills.PointsOf(mage.Id), Is.EqualTo(1));
        }
    }
}
