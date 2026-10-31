using System;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Localization;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// A refusal is a code and a text to translate as well as English words, so a game in another language tells the
    /// player why without matching the English sentences (EchoTower ET-06, GreycloakGuild GG-07).
    /// </summary>
    [TestFixture]
    public sealed class BattleRefusalTests
    {
        private TurnBasedTestGame _game;

        [SetUp]
        public void SetUp() => _game = new TurnBasedTestGame();

        [TearDown]
        public void TearDown() => _game.Dispose();

        private Battle StartBattle()
        {
            _game.Start();
            return _game.World.StartBattle();
        }

        [Test]
        public void Refused_HasACode_AndATextWithTheNumbers()
        {
            _game.EnemiesDefend();
            _game.MageData.MaxMana = 5;
            var battle = StartBattle();

            var result = battle.Perform(BattleAction.Ability(Fire, battle.Enemies[0].Id));

            Assert.That(result.Reason, Is.EqualTo(BattleRefusalReason.NotEnoughMana));
            Assert.That(result.RefusalText.Key, Is.EqualTo("battle.refusal.notEnoughMana"));
            Assert.That(result.RefusalText.Arg("cost"), Is.EqualTo(6));
            Assert.That(result.RefusalText.Arg("has"), Is.EqualTo(5));
            Assert.That(result.Refusal, Is.EqualTo("Not enough mana: Fire costs 6 MP and Mage has 5."));
            Assert.That(battle.RefusalOf(BattleAction.Ability(Fire, battle.Enemies[0].Id)).Reason, Is.EqualTo(BattleRefusalReason.NotEnoughMana));
        }

        [Test]
        public void ARefusal_ReadsInTheGamesLanguage()
        {
            _game.EnemiesDefend();
            _game.MageData.MaxMana = 5;
            var battle = StartBattle();
            var ukrainian = new StringTable("uk", "Українська");
            ukrainian.Set("battle.refusal.notEnoughMana", "Бракує мани: {ability} коштує {cost}, у {actor} лише {has}.");
            var localization = new LocalizationService(new[] { BattleTexts.Table(), ukrainian }, "uk");

            var result = battle.Perform(BattleAction.Ability(Fire, battle.Enemies[0].Id));

            Assert.That(localization.Text(result.RefusalText), Is.EqualTo("Бракує мани: Fire коштує 6, у Mage лише 5."));
        }

        [Test]
        public void ADoneAction_HasNoRefusal()
        {
            _game.EnemiesDefend();
            var battle = StartBattle();

            var result = battle.Perform(BattleAction.Defend());

            Assert.That(result.Done, Is.True);
            Assert.That(result.Reason, Is.EqualTo(BattleRefusalReason.None));
            Assert.That(result.RefusalText.IsEmpty, Is.True);
            Assert.That(result.Refusal, Is.Null);
        }

        [Test]
        public void AClosedMenuCommand_SaysWhyAsACode()
        {
            _game.EnemiesDefend();
            _game.MageData.MaxMana = 5;
            var battle = StartBattle();

            var fire = BattleMenu.For(battle).Single(c => c.Kind == BattleActionKind.Ability && c.Action.AbilityId == Fire);

            Assert.That(fire.IsAvailable, Is.False);
            Assert.That(fire.Why.Reason, Is.EqualTo(BattleRefusalReason.NotEnoughMana));
            Assert.That(fire.Refusal, Is.EqualTo(fire.Why.Message));
        }

        [Test]
        public void EveryReason_HasAnEnglishText()
        {
            foreach (BattleRefusalReason reason in Enum.GetValues(typeof(BattleRefusalReason)))
            {
                if (reason == BattleRefusalReason.None)
                    continue;
                string key = BattleTexts.KeyOf(reason);
                Assert.That(BattleTexts.Keys.ContainsKey(key) || BattleTexts.Keys.ContainsKey(key + ".other"), Is.True, key);
            }
        }
    }
}
