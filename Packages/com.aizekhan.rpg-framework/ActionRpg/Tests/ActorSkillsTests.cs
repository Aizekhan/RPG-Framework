using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Logging;
using RPGFramework.Skills;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The kit's skill tree: points from the start, the levels and grants buy skill levels within their
    /// requirements, the bonuses become stats and resistances, a reset gives the points back and a save keeps it all.
    /// </summary>
    public sealed class ActorSkillsTests
    {
        private const string Might = "might", Fireskin = "fireskin", Veteran = "veteran";

        private Logger _logger;
        private GameHost _host;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private ActionRpgModule _module;

        private ActorWorld World => _module.World;
        private ActorSkills Skills => World.Skills;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                Seed = 7,
                StartingSkillPoints = 1
            };
            _settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin" });

            var tree = new SkillTree("warrior", "Warrior", "");
            var might = new Skill(Might, "Might", "+5 Attack a level", 3, SkillType.Passive);
            might.AddBonus(new SkillBonus("Attack", 5f, BonusType.Additive));
            tree.AddSkill(might);
            var fireskin = new Skill(Fireskin, "Fireskin", "Resist fire", 1, SkillType.Passive);
            fireskin.AddPrerequisite(new SkillPrerequisite(Might, 2, 0));
            fireskin.AddBonus(new SkillBonus("resist:fire", 0.2f, BonusType.Additive));
            tree.AddSkill(fireskin);
            var veteran = new Skill(Veteran, "Veteran", "", 1, SkillType.Passive);
            veteran.AddPrerequisite(new SkillPrerequisite(null, 0, 3));
            veteran.AddBonus(new SkillBonus("MaxHealth", 0.1f, BonusType.Multiplicative));
            tree.AddSkill(veteran);

            _content = new ActionRpgContent { SkillTree = tree };
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame() =>
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));

        [Test]
        public void Points_BuySkillLevels_WithinTheirRequirements()
        {
            StartGame();
            int hero = World.PlayerId;
            Assert.That(Skills.HasTree, Is.True);
            Assert.That(Skills.PointsOf(hero), Is.EqualTo(1));
            Assert.That(Skills.WhyNot(hero, Fireskin), Is.EqualTo("Its requirements are not met"));

            Assert.That(Skills.Learn(hero, Might), Is.True);
            Assert.That(World.Player.Attack, Is.EqualTo(55f));
            Assert.That(Skills.Learn(hero, Might), Is.False);
            Assert.That(Skills.WhyNot(hero, Might), Is.EqualTo("No skill points left"));

            Skills.GrantPoints(hero, 2);
            Assert.That(Skills.Learn(hero, Might), Is.True);
            Assert.That(World.Player.Attack, Is.EqualTo(60f), "two levels of +5");
            Assert.That(Skills.Learn(hero, Fireskin), Is.True);
            Assert.That(World.Resistances.Of(hero, "fire"), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(Skills.LevelOf(hero, Might), Is.EqualTo(2));
            Assert.That(Skills.PointsOf(hero), Is.EqualTo(0));
            Assert.That(Skills.WhyNot(hero, Veteran), Is.EqualTo("No skill points left"));
            Assert.That(Skills.WhyNot(hero, "nope"), Is.EqualTo("Unknown skill"));

            Skills.Reset(hero);
            Assert.That(World.Player.Attack, Is.EqualTo(50f));
            Assert.That(World.Resistances.Of(hero, "fire"), Is.EqualTo(0f));
            Assert.That(Skills.PointsOf(hero), Is.EqualTo(3), "every point back");
        }

        [Test]
        public void ASave_KeepsTheSkills_AndLevelsGivePoints()
        {
            StartGame();
            int hero = World.PlayerId;
            Skills.GrantPoints(hero, 2);
            Skills.Learn(hero, Might);
            Skills.Learn(hero, Might);
            Skills.Learn(hero, Fireskin);
            var progress = World.CaptureProgress();
            Assert.That(progress.Skills.Count, Is.EqualTo(2));
            Assert.That(progress.BonusSkillPoints, Is.EqualTo(2));

            Skills.Reset(hero);
            Skills.GrantPoints(hero, 5);
            progress.Level = 3;
            World.RestoreProgress(progress);

            Assert.That(Skills.LevelOf(hero, Might), Is.EqualTo(2));
            Assert.That(World.Player.Attack, Is.EqualTo(60f), "the hero does not grow Attack by level here");
            Assert.That(World.Resistances.Of(hero, "fire"), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(Skills.PointsOf(hero), Is.EqualTo(1 + 2 + 2 - 3), "start, two levels, granted, less three spent");

            Assert.That(Skills.Learn(hero, Veteran), Is.True, "level 3 now");
            Assert.That(World.Player.MaxHealth, Is.EqualTo(110f).Within(1e-3f));
        }
    }
}
