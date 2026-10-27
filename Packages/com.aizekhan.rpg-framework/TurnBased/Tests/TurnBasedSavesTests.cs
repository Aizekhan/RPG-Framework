using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static RPGFramework.TurnBased.Tests.TurnBasedTestGame;

namespace RPGFramework.TurnBased.Tests
{
    /// <summary>
    /// A run survives a restart of the game: each member's level, experience, health and mana, the inventory, the gold
    /// and the encounter reached come back, and the next fight is the one after the last won.
    /// </summary>
    public sealed class TurnBasedSavesTests
    {
        private string _directory;
        private readonly List<TurnBasedTestGame> _sessions = new List<TurnBasedTestGame>();

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_TurnBasedSaves_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var session in _sessions)
                session.Dispose();
            _sessions.Clear();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private TurnBasedTestGame StartSession()
        {
            var game = new TurnBasedTestGame();
            game.EnemiesAttack(Knight);
            _sessions.Add(game);
            game.Start();
            return game;
        }

        [Test]
        public void SaveThenLoad_MidRun_InANewSession_RestoresTheParty()
        {
            var first = StartSession();
            var world = first.World;
            var battle = world.StartBattle();
            Expect(battle.Perform(BattleAction.Item(Potion, first.Member(Mage).Id)));
            PlayToTheEnd(battle);
            Assume.That(world.EncounterIndex, Is.EqualTo(1));
            Assume.That(first.Member(Knight).Health, Is.LessThan(first.Member(Knight).MaxHealth));
            var saved = world.Party.Select(m => new { m.CharacterId, m.Level, Xp = world.ExperienceOf(m.Id), m.Health, m.Mana }).ToList();
            var items = world.Inventory.GetAllItems().Select(i => i.ItemId).ToList();
            Assert.That(new TurnBasedSaves(world, _directory).Save(), Is.Null);

            var second = StartSession();
            var saves = new TurnBasedSaves(second.World, _directory);
            Assert.That(saves.HasSave, Is.True);
            Assert.That(saves.Load(), Is.Null);

            var restored = second.World;
            var now = restored.Party.Select(m => new { m.CharacterId, m.Level, Xp = restored.ExperienceOf(m.Id), m.Health, m.Mana }).ToList();
            Assert.That(now, Is.EqualTo(saved));
            Assert.That(second.Member(Knight).MaxHealth, Is.EqualTo(140), "level 2 stats");
            Assert.That(restored.Inventory.GetAllItems().Select(i => i.ItemId), Is.EqualTo(items));
            Assert.That(restored.Gold, Is.EqualTo(world.Gold));
            Assert.That(restored.EncounterIndex, Is.EqualTo(1));
            Assert.That(restored.StartBattle().Encounter.Id, Is.EqualTo("camp"));
        }

        [Test]
        public void Load_WithoutASave_ChangesNothing()
        {
            var game = StartSession();
            var saves = new TurnBasedSaves(game.World, _directory);

            Assert.That(saves.HasSave, Is.False);
            Assert.That(saves.Load(), Is.Null);
            Assert.That(game.World.EncounterIndex, Is.EqualTo(0));
            Assert.That(game.World.Inventory.Count, Is.EqualTo(7));
        }

        [Test]
        public void ABattleInProgress_IsNotSaved_AndCannotBeLoadedOver()
        {
            var first = StartSession();
            first.World.StartBattle();
            Assert.That(new TurnBasedSaves(first.World, _directory).Save(), Is.Null);

            Assert.Throws<InvalidOperationException>(() => first.World.RestoreProgress(first.World.CaptureProgress()));

            var second = StartSession();
            Assert.That(new TurnBasedSaves(second.World, _directory).Load(), Is.Null);
            Assert.That(second.World.IsInBattle, Is.False);
            Assert.That(second.World.EncounterIndex, Is.EqualTo(0));
            Assert.That(second.World.NextEncounter.Id, Is.EqualTo("pack"));
        }
    }
}
