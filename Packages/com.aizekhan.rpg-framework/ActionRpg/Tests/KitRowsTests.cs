using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Bootstrap;
using RPGFramework.Dialogue;
using RPGFramework.Factions;
using RPGFramework.Items;
using RPGFramework.Logging;
using RPGFramework.Skills;
using RPGFramework.UI.Panels;

namespace RPGFramework.ActionRpg.Tests
{
    /// <summary>
    /// The kit's pages for a list panel: the skill tree learns, a potion is used, a conversation is talked through
    /// with its choices, and the standing reads the reputation; the rows read the world as it is now.
    /// </summary>
    public sealed class KitRowsTests
    {
        private Logger _logger;
        private GameHost _host;
        private ActionRpgSettings _settings;
        private ActionRpgContent _content;
        private ActionRpgModule _module;

        private ActorWorld World => _module.World;

        [SetUp]
        public void SetUp()
        {
            _logger = new Logger(LogSeverity.Warning, new MemoryLogSink(256));
            _host = new GameHost(_logger);
            _settings = new ActionRpgSettings
            {
                Player = new CharacterData { Id = "hero", DisplayName = "Hero", MaxHealth = 100f, Attack = 50f, Defense = 5f },
                Waves = new WaveSettings { Enabled = false },
                StartingSkillPoints = 1,
                Seed = 7
            };
            _settings.Enemies.Add(new CharacterData { Id = "goblin", DisplayName = "Goblin" });

            var tree = new SkillTree("warrior", "Warrior", "");
            var might = new Skill("might", "Might", "", 2, SkillType.Passive);
            might.AddBonus(new SkillBonus("Attack", 5f, BonusType.Additive));
            tree.AddSkill(might);
            _content = new ActionRpgContent { SkillTree = tree };
            _content.Items.Add(new Consumable("potion", "Potion", "", ConsumableEffects.RestoreHealth, 30f, cooldownCategory: "Potion", cooldownDuration: 5f));
            _content.Factions.Add(new Faction("guards", "City Guard", "", FactionType.Military, defaultReputation: 30));
            var nodes = new List<IDialogueNode>
            {
                new DialogueNode("hi", DialogueNodeType.Line, "Elder", "Welcome.", new[] { "ask" }, new IDialogueChoice[0]),
                new DialogueNode("ask", DialogueNodeType.Choice, "Elder", "Help us?", new string[0], new IDialogueChoice[]
                {
                    new DialogueChoice("yes", "I will.", "bye"),
                    new DialogueChoice("rich", "Gold?", "bye", new DialogueCondition(ConditionType.StatCheck, "Gold", 100))
                }),
                new DialogueNode("bye", DialogueNodeType.End, "Elder", "Farewell.", new string[0], new IDialogueChoice[0])
            };
            _content.Dialogues.Add(new RPGFramework.Dialogue.Dialogue("elder", "The Elder", "", "hi", nodes));
        }

        [TearDown]
        public void TearDown()
        {
            _host.Dispose();
            _logger.Dispose();
        }

        private void StartGame() =>
            _host.Start(builder => _module = ActionRpgComposition.Configure(builder, _settings, _content));

        private static string[] Texts(IEnumerable<ActionRow> rows) => rows.Select(r => r.Text()).ToArray();

        [Test]
        public void TheSkillPage_Learns_AndTheConsumablePage_Uses()
        {
            StartGame();
            int hero = World.PlayerId;
            var skills = KitRows.Skills(World, hero);
            Assert.That(Texts(skills), Is.EqualTo(new[] { "Skill points: 1", "Might  0/2", "" }));
            var learn = skills[1].Buttons[0];
            Assert.That(learn.Enabled(), Is.True);
            learn.Click();
            Assert.That(Texts(skills).Take(2), Is.EqualTo(new[] { "Skill points: 0", "Might  1/2" }));
            Assert.That(learn.Enabled(), Is.False, "no points");

            World.GiveItem("potion", 2);
            World.Player.ApplyDamage(50f);
            var consumables = KitRows.Consumables(World, hero);
            Assert.That(Texts(consumables), Is.EqualTo(new[] { "Potion  x2" }));
            consumables[0].Buttons[0].Click();
            Assert.That(Texts(consumables), Is.EqualTo(new[] { "Potion  x1  (5s)" }));
            Assert.That(consumables[0].Buttons[0].Enabled(), Is.False);
        }

        [Test]
        public void TheDialoguePage_TalksThrough_AndTheStandingPage_ReadsTheReputation()
        {
            StartGame();
            int hero = World.PlayerId;
            Assert.That(KitRows.Dialogue(World, hero), Is.Empty, "not talking");
            World.Dialogue.Start(hero, "elder");

            var page = KitRows.Dialogue(World, hero);
            Assert.That(page[0].Text(), Is.EqualTo("Elder: Welcome."));
            Assert.That(page[1].Buttons[0].Label(), Is.EqualTo("Continue"));
            page[1].Buttons[0].Click();

            page = KitRows.Dialogue(World, hero);
            Assert.That(page.Skip(1).Select(r => r.Buttons[0].Label()), Is.EqualTo(new[] { "I will.", "Leave" }), "no gold for the other");
            page[1].Buttons[0].Click();
            Assert.That(KitRows.Dialogue(World, hero), Is.Empty, "the last line ends the talk");

            World.Dialogue.Start(hero, "elder");
            KitRows.Dialogue(World, hero).Last().Buttons[0].Click();
            Assert.That(World.Dialogue.IsTalking(hero), Is.False, "walked away");

            var standing = KitRows.Reputation(World, hero);
            Assert.That(Texts(standing), Is.EqualTo(new[] { "City Guard  <b>Friendly</b> (30)" }));
        }
    }
}
