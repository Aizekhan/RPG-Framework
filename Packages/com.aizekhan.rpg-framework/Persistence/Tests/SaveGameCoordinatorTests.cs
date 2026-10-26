using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace RPGFramework.Persistence.Tests
{
    public sealed class SaveGameCoordinatorTests
    {
        public sealed class InventoryState
        {
            public List<string> Items { get; set; } = new List<string>();
            public int Gold { get; set; }
        }

        public sealed class QuestState
        {
            public List<string> Completed { get; set; } = new List<string>();
        }

        private sealed class InventorySystem : SaveParticipant<InventoryState>
        {
            public List<string> Items = new List<string>();
            public int Gold;

            public override string SaveKey => "inventory";

            protected override InventoryState CaptureState() => new InventoryState { Items = new List<string>(Items), Gold = Gold };

            protected override void RestoreState(InventoryState state)
            {
                Items = new List<string>(state.Items);
                Gold = state.Gold;
            }
        }

        private sealed class QuestSystem : SaveParticipant<QuestState>
        {
            public List<string> Completed = new List<string>();
            public Exception CaptureFailure;
            public Exception RestoreFailure;
            public int Restores;

            public override string SaveKey => "quests";

            protected override QuestState CaptureState()
            {
                if (CaptureFailure != null)
                    throw CaptureFailure;
                return new QuestState { Completed = new List<string>(Completed) };
            }

            protected override void RestoreState(QuestState state)
            {
                Restores++;
                if (RestoreFailure != null)
                    throw RestoreFailure;
                Completed = new List<string>(state.Completed);
            }
        }

        private string _directory;
        private SaveSystem _saveSystem;
        private SaveGameCoordinator _coordinator;
        private InventorySystem _inventory;
        private QuestSystem _quests;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_Coordinator_" + Guid.NewGuid());
            _saveSystem = new SaveSystem(_directory, 3, new BinarySaveSerializer(), new SaveValidator("1.0.0"));
            _inventory = new InventorySystem { Items = { "sword", "potion" }, Gold = 120 };
            _quests = new QuestSystem { Completed = { "intro" } };
            _coordinator = new SaveGameCoordinator(_saveSystem).Register(_inventory).Register(_quests);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void SaveThenLoad_RestoresEveryParticipant()
        {
            Assert.That(_coordinator.Save("slot_0", new SaveMetadata()).Success, Is.True);

            _inventory.Items.Clear();
            _inventory.Gold = 0;
            _quests.Completed.Add("dragon");

            var result = _coordinator.Load("slot_0");

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            CollectionAssert.AreEqual(new[] { "sword", "potion" }, _inventory.Items);
            Assert.That(_inventory.Gold, Is.EqualTo(120));
            CollectionAssert.AreEqual(new[] { "intro" }, _quests.Completed);
            Assert.That(_coordinator.LastMissingSections, Is.Empty);
        }

        [Test]
        public void ParticipantAddedAfterSave_KeepsItsStateAndIsReported()
        {
            var coordinator = new SaveGameCoordinator(_saveSystem).Register(_inventory);
            coordinator.Save("slot_0", new SaveMetadata());
            coordinator.Register(_quests);
            _quests.Completed.Add("new");

            var result = coordinator.Load("slot_0");

            Assert.That(result.Success, Is.True);
            CollectionAssert.AreEqual(new[] { "intro", "new" }, _quests.Completed);
            CollectionAssert.AreEqual(new[] { "quests" }, coordinator.LastMissingSections);
        }

        [Test]
        public void CaptureFailure_WritesNothing()
        {
            _quests.CaptureFailure = new InvalidOperationException("quest log busy");

            var result = _coordinator.Save("slot_1", new SaveMetadata());

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.SerializationFailed));
            StringAssert.Contains("quests", result.ErrorMessage);
            Assert.That(_saveSystem.HasSave("slot_1"), Is.False);
        }

        [Test]
        public void UnreadableSection_FailsBeforeAnyParticipantChanges()
        {
            _coordinator.Save("slot_0", new SaveMetadata());
            _inventory.Gold = 5;
            // A save written by a different game: same key, incompatible data
            var other = new SaveGameState();
            other.Sections["inventory"] = "not base64!";
            _saveSystem.Save("slot_1", other, new SaveMetadata());

            var result = _coordinator.Load("slot_1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.DeserializationFailed));
            Assert.That(_inventory.Gold, Is.EqualTo(5));
            Assert.That(_quests.Restores, Is.Zero);
        }

        [Test]
        public void RestoreFailure_IsReported_OtherParticipantsStillRestored()
        {
            _coordinator.Save("slot_0", new SaveMetadata());
            _inventory.Gold = 0;
            _quests.RestoreFailure = new InvalidOperationException("unknown quest id");

            var result = _coordinator.Load("slot_0");

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("quests", result.ErrorMessage);
            Assert.That(_inventory.Gold, Is.EqualTo(120));
        }

        [Test]
        public void NewerFormatOrForeignPayload_IsRejected()
        {
            _saveSystem.Save("slot_0", new SaveGameState { Version = SaveGameState.CurrentVersion + 1 }, new SaveMetadata());
            Assert.That(_coordinator.Load("slot_0").Error, Is.EqualTo(SaveError.IncompatibleVersion));

            Assert.That(_coordinator.Load("slot_2").Error, Is.EqualTo(SaveError.FileNotFound));
        }

        [Test]
        public void QuickSaveAndAutoSave_UseTheirSlots()
        {
            Assert.That(_coordinator.QuickSave(new SaveMetadata()).Success, Is.True);
            _inventory.Gold = 1;
            Assert.That(_coordinator.QuickLoad().Success, Is.True);
            Assert.That(_inventory.Gold, Is.EqualTo(120));

            _saveSystem.AutoSaveEnabled = true;
            Assert.That(_coordinator.AutoSave(new SaveMetadata()), Is.True);
            Assert.That(_saveSystem.HasSave("autosave"), Is.True);
        }

        [Test]
        public void Registration_RejectsDuplicateKeys()
        {
            Assert.Throws<InvalidOperationException>(() => _coordinator.Register(new InventorySystem()));
            Assert.That(_coordinator.Unregister("inventory"), Is.True);
            Assert.That(_coordinator.Unregister("inventory"), Is.False);
            CollectionAssert.AreEqual(new[] { "quests" }, _coordinator.ParticipantKeys);
        }
    }
}
