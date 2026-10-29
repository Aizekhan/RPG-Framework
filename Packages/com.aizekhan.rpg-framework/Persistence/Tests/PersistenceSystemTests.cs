using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace RPGFramework.Persistence.Tests
{
    [TestFixture]
    public class PersistenceSystemTests
    {
        private string _testSaveDirectory;
        private ISaveSystem _saveSystem;
        private ISaveSerializer _jsonSerializer;
        private ISaveSerializer _binarySerializer;
        private ISaveValidator _validator;

        [Serializable]
        private class TestGameState
        {
            public string PlayerName { get; set; }
            public int Level { get; set; }
            public float Health { get; set; }
            public List<string> Inventory { get; set; }

            public TestGameState()
            {
                Inventory = new List<string>();
            }
        }

        [SetUp]
        public void SetUp()
        {
            _testSaveDirectory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_Persistence_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testSaveDirectory);

            _jsonSerializer = new JsonSaveSerializer();
            _binarySerializer = new BinarySaveSerializer();
            _validator = new SaveValidator("1.0.0");
            _saveSystem = new SaveSystem(_testSaveDirectory, 3, _jsonSerializer, _validator);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testSaveDirectory))
            {
                Directory.Delete(_testSaveDirectory, true);
            }
        }

        #region Save System Tests

        [Test]
        public void SaveSystem_InitializesWithCorrectMaxSlots()
        {
            Assert.AreEqual(3, _saveSystem.MaxSlots);
            Assert.IsNotNull(_saveSystem.Slots);
            Assert.GreaterOrEqual(_saveSystem.Slots.Count, 3); // At least 3 regular slots + quicksave + autosave
        }

        [Test]
        public void SaveSystem_SaveAndLoad_SingleSlot_Success()
        {
            // Arrange
            var gameState = new TestGameState
            {
                PlayerName = "TestHero",
                Level = 10,
                Health = 85.5f,
                Inventory = new List<string> { "Sword", "Shield", "Potion" }
            };

            var metadata = new SaveMetadata("1.0.0", "TestHero", 3600f, "Forest", 10);

            // Act - Save
            var saveResult = _saveSystem.Save("slot_0", gameState, metadata);

            // Assert - Save
            Assert.IsTrue(saveResult.Success);
            Assert.AreEqual("slot_0", saveResult.SlotId);
            Assert.AreEqual(SaveError.None, saveResult.Error);
            Assert.IsTrue(_saveSystem.HasSave("slot_0"));

            // Act - Load
            var loadResult = _saveSystem.Load<TestGameState>("slot_0");

            // Assert - Load: the state comes back with its data (it used to be an empty object)
            Assert.IsTrue(loadResult.Success);
            Assert.AreEqual("slot_0", loadResult.SlotId);
            var loaded = loadResult.GameState as TestGameState;
            Assert.IsNotNull(loaded);
            Assert.AreEqual("TestHero", loaded.PlayerName);
            Assert.AreEqual(10, loaded.Level);
            Assert.AreEqual(85.5f, loaded.Health);
            CollectionAssert.AreEqual(new[] { "Sword", "Shield", "Potion" }, loaded.Inventory);

            var opened = _saveSystem.Load("slot_0");
            Assert.IsTrue(opened.Success);
            Assert.IsNull(opened.GameState, "the untyped load does not read the state");
            Assert.AreEqual("TestHero", opened.Metadata.CharacterName);
        }

        [Test]
        public void SaveSystem_BinarySerializer_RoundTripsTypedState()
        {
            var binarySystem = new SaveSystem(_testSaveDirectory, 3, _binarySerializer, _validator);
            binarySystem.Save("slot_2", new TestGameState { PlayerName = "Binary", Level = 3 }, new SaveMetadata());

            var loaded = binarySystem.Load<TestGameState>("slot_2").GameState as TestGameState;

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Binary", loaded.PlayerName);
            Assert.AreEqual(3, loaded.Level);
        }

        [Test]
        public void SaveSystem_SaveToNonExistentSlot_ReturnsFailure()
        {
            var gameState = new TestGameState();
            var metadata = new SaveMetadata();

            var result = _saveSystem.Save("invalid_slot", gameState, metadata);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveError.SlotNotFound, result.Error);
        }

        [Test]
        public void SaveSystem_LoadFromEmptySlot_ReturnsFailure()
        {
            var result = _saveSystem.Load("slot_1");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(SaveError.FileNotFound, result.Error);
        }

        [Test]
        public void SaveSystem_SaveMultipleSlots_Success()
        {
            var gameState1 = new TestGameState { PlayerName = "Hero1", Level = 5 };
            var gameState2 = new TestGameState { PlayerName = "Hero2", Level = 10 };
            var gameState3 = new TestGameState { PlayerName = "Hero3", Level = 15 };

            var metadata1 = new SaveMetadata("1.0.0", "Hero1", 1000f, "Town", 5);
            var metadata2 = new SaveMetadata("1.0.0", "Hero2", 2000f, "Forest", 10);
            var metadata3 = new SaveMetadata("1.0.0", "Hero3", 3000f, "Mountain", 15);

            var result1 = _saveSystem.Save("slot_0", gameState1, metadata1);
            var result2 = _saveSystem.Save("slot_1", gameState2, metadata2);
            var result3 = _saveSystem.Save("slot_2", gameState3, metadata3);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.IsTrue(result3.Success);

            Assert.IsTrue(_saveSystem.HasSave("slot_0"));
            Assert.IsTrue(_saveSystem.HasSave("slot_1"));
            Assert.IsTrue(_saveSystem.HasSave("slot_2"));
        }

        [Test]
        public void SaveSystem_DeleteSave_Success()
        {
            var gameState = new TestGameState { PlayerName = "ToDelete", Level = 1 };
            var metadata = new SaveMetadata("1.0.0", "ToDelete", 100f, "Start", 1);

            _saveSystem.Save("slot_0", gameState, metadata);
            Assert.IsTrue(_saveSystem.HasSave("slot_0"));

            var deleteResult = _saveSystem.DeleteSave("slot_0");

            Assert.IsTrue(deleteResult);
            Assert.IsFalse(_saveSystem.HasSave("slot_0"));
        }

        [Test]
        public void SaveSystem_GetMetadata_ReturnsCorrectMetadata()
        {
            var gameState = new TestGameState { PlayerName = "MetaTest", Level = 20 };
            var metadata = new SaveMetadata("1.0.0", "MetaTest", 5000f, "Castle", 20);

            _saveSystem.Save("slot_0", gameState, metadata);

            var retrievedMetadata = _saveSystem.GetMetadata("slot_0");

            Assert.IsNotNull(retrievedMetadata);
            Assert.AreEqual("MetaTest", retrievedMetadata.CharacterName);
            Assert.AreEqual(20, retrievedMetadata.CharacterLevel);
            Assert.AreEqual("Castle", retrievedMetadata.LocationName);
        }

        [Test]
        public void SaveSystem_QuickSave_Success()
        {
            var gameState = new TestGameState { PlayerName = "QuickSaveTest", Level = 7 };
            var metadata = new SaveMetadata("1.0.0", "QuickSaveTest", 1500f, "Cave", 7);

            var result = _saveSystem.QuickSave(gameState, metadata);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("quicksave", result.SlotId);
            Assert.IsTrue(_saveSystem.HasSave("quicksave"));
        }

        [Test]
        public void SaveSystem_QuickLoad_Success()
        {
            var gameState = new TestGameState { PlayerName = "QuickLoadTest", Level = 12 };
            var metadata = new SaveMetadata("1.0.0", "QuickLoadTest", 2500f, "Desert", 12);

            _saveSystem.QuickSave(gameState, metadata);
            var result = _saveSystem.QuickLoad<TestGameState>();

            Assert.IsTrue(result.Success);
            Assert.AreEqual("quicksave", result.SlotId);
            Assert.AreEqual("QuickLoadTest", ((TestGameState)result.GameState).PlayerName);
        }

        [Test]
        public void SaveSystem_AutoSave_TriggersCorrectly()
        {
            var gameState = new TestGameState { PlayerName = "AutoSaveTest", Level = 8 };
            var metadata = new SaveMetadata("1.0.0", "AutoSaveTest", 1800f, "Swamp", 8);

            _saveSystem.TriggerAutoSave(gameState, metadata);

            Assert.IsTrue(_saveSystem.HasSave("autosave"));
        }

        [Test]
        public void SaveSystem_AutoSaveEnabled_GetSet()
        {
            _saveSystem.AutoSaveEnabled = false;
            Assert.IsFalse(_saveSystem.AutoSaveEnabled);

            _saveSystem.AutoSaveEnabled = true;
            Assert.IsTrue(_saveSystem.AutoSaveEnabled);
        }

        [Test]
        public void SaveSystem_AutoSaveInterval_GetSet()
        {
            _saveSystem.AutoSaveIntervalSeconds = 600f;
            Assert.AreEqual(600f, _saveSystem.AutoSaveIntervalSeconds);
        }

        [Test]
        public void SaveSystem_ActiveSlot_UpdatesAfterSave()
        {
            var gameState = new TestGameState { PlayerName = "ActiveSlotTest", Level = 15 };
            var metadata = new SaveMetadata("1.0.0", "ActiveSlotTest", 4000f, "Volcano", 15);

            _saveSystem.Save("slot_1", gameState, metadata);

            Assert.IsNotNull(_saveSystem.ActiveSlot);
            Assert.AreEqual("slot_1", _saveSystem.ActiveSlot.SlotId);
        }

        #endregion

        #region Serializer Tests

        [Test]
        public void JsonSerializer_SerializeDeserialize_Success()
        {
            var original = new TestGameState
            {
                PlayerName = "JsonTest",
                Level = 25,
                Health = 100f,
                Inventory = new List<string> { "Axe", "Armor" }
            };

            var serialized = _jsonSerializer.Serialize(original);
            Assert.IsNotNull(serialized);
            Assert.Greater(serialized.Length, 0);

            var deserialized = _jsonSerializer.Deserialize<TestGameState>(serialized);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.PlayerName, deserialized.PlayerName);
            Assert.AreEqual(original.Level, deserialized.Level);
            Assert.AreEqual(original.Health, deserialized.Health);
        }

        [Test]
        public void JsonSerializer_Format_IsJson()
        {
            Assert.AreEqual(SerializationFormat.Json, _jsonSerializer.Format);
        }

        [Test]
        public void BinarySerializer_SerializeDeserialize_Success()
        {
            var original = new TestGameState
            {
                PlayerName = "BinaryTest",
                Level = 30,
                Health = 95f,
                Inventory = new List<string> { "Bow", "Arrows" }
            };

            var serialized = _binarySerializer.Serialize(original);
            Assert.IsNotNull(serialized);
            Assert.Greater(serialized.Length, 0);

            var deserialized = _binarySerializer.Deserialize<TestGameState>(serialized);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.PlayerName, deserialized.PlayerName);
            Assert.AreEqual(original.Level, deserialized.Level);
            Assert.AreEqual(original.Health, deserialized.Health);
        }

        [Test]
        public void BinarySerializer_Format_IsBinary()
        {
            Assert.AreEqual(SerializationFormat.Binary, _binarySerializer.Format);
        }

        [Test]
        public void BinarySerializer_RejectsDataWithoutHeader()
        {
            // e.g. a legacy or crafted BinaryFormatter payload
            var foreign = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF };

            Assert.Throws<InvalidOperationException>(() => _binarySerializer.Deserialize<TestGameState>(foreign));
        }

        [TestCase("uk-UA")]
        [TestCase("de-DE")]
        public void Serializers_RoundTripFractionalValues_UnderCommaDecimalCulture(string cultureName)
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(cultureName);
                var original = new TestGameState { PlayerName = "Culture", Level = 2, Health = 85.5f };

                foreach (var serializer in new[] { _jsonSerializer, _binarySerializer })
                {
                    var deserialized = serializer.Deserialize<TestGameState>(serializer.Serialize(original));
                    Assert.AreEqual(85.5f, deserialized.Health, serializer.Format.ToString());
                    Assert.AreEqual(2, deserialized.Level, serializer.Format.ToString());
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        #endregion

        #region SaveFile Tests

        [Test]
        public void SaveFile_ValidateIntegrity_ValidChecksum_ReturnsTrue()
        {
            var metadata = new SaveMetadata("1.0.0", "IntegrityTest", 100f, "Test", 1);
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var checksum = _validator.ComputeChecksum(data);

            var saveFile = new SaveFile(metadata, data, checksum);

            Assert.IsTrue(saveFile.ValidateIntegrity());
        }

        [Test]
        public void SaveFile_ValidateIntegrity_InvalidChecksum_ReturnsFalse()
        {
            var metadata = new SaveMetadata("1.0.0", "IntegrityTest", 100f, "Test", 1);
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var invalidChecksum = "invalid_checksum";

            var saveFile = new SaveFile(metadata, data, invalidChecksum);

            Assert.IsFalse(saveFile.ValidateIntegrity());
        }

        [Test]
        public void SaveFile_Properties_AreSetCorrectly()
        {
            var metadata = new SaveMetadata("1.0.0", "PropertyTest", 200f, "Test", 5);
            var data = new byte[] { 10, 20, 30 };
            var checksum = _validator.ComputeChecksum(data);

            var saveFile = new SaveFile(metadata, data, checksum);

            Assert.AreEqual(metadata, saveFile.Metadata);
            Assert.AreEqual(data, saveFile.Data);
            Assert.AreEqual(checksum, saveFile.Checksum);
        }

        #endregion

        #region SaveValidator Tests

        [Test]
        public void SaveValidator_ValidateIntegrity_ValidSave_ReturnsTrue()
        {
            var metadata = new SaveMetadata("1.0.0", "ValidatorTest", 300f, "Test", 10);
            var data = new byte[] { 5, 10, 15, 20 };
            var checksum = _validator.ComputeChecksum(data);
            var saveFile = new SaveFile(metadata, data, checksum);

            Assert.IsTrue(_validator.ValidateIntegrity(saveFile));
        }

        [Test]
        public void SaveValidator_ValidateIntegrity_CorruptedSave_ReturnsFalse()
        {
            var metadata = new SaveMetadata("1.0.0", "CorruptTest", 400f, "Test", 15);
            var data = new byte[] { 1, 2, 3 };
            var saveFile = new SaveFile(metadata, data, "corrupted_checksum");

            Assert.IsFalse(_validator.ValidateIntegrity(saveFile));
        }

        [Test]
        public void SaveValidator_IsCompatible_SameVersion_ReturnsTrue()
        {
            var metadata = new SaveMetadata("1.0.0", "CompatTest", 500f, "Test", 20);
            var data = new byte[] { 1 };
            var checksum = _validator.ComputeChecksum(data);
            var saveFile = new SaveFile(metadata, data, checksum);

            Assert.IsTrue(_validator.IsCompatible(saveFile));
        }

        [Test]
        public void SaveValidator_IsCompatible_DifferentVersion_ReturnsFalse()
        {
            var metadata = new SaveMetadata("2.0.0", "IncompatTest", 600f, "Test", 25);
            var data = new byte[] { 1 };
            var checksum = _validator.ComputeChecksum(data);
            var saveFile = new SaveFile(metadata, data, checksum);

            Assert.IsFalse(_validator.IsCompatible(saveFile));
        }

        [Test]
        public void SaveValidator_TryRepair_RecomputesChecksum()
        {
            var metadata = new SaveMetadata("1.0.0", "RepairTest", 700f, "Test", 30);
            var data = new byte[] { 7, 8, 9 };
            var saveFile = new SaveFile(metadata, data, "wrong_checksum");

            var repaired = _validator.TryRepair(saveFile, out var repairedFile);

            Assert.IsTrue(repaired);
            Assert.IsNotNull(repairedFile);
            Assert.IsTrue(repairedFile.ValidateIntegrity());
        }

        [Test]
        public void SaveValidator_ComputeChecksum_ProducesSameHashForSameData()
        {
            var data = new byte[] { 11, 22, 33, 44 };

            var checksum1 = _validator.ComputeChecksum(data);
            var checksum2 = _validator.ComputeChecksum(data);

            Assert.AreEqual(checksum1, checksum2);
        }

        [Test]
        public void SaveValidator_ComputeChecksum_ProducesDifferentHashForDifferentData()
        {
            var data1 = new byte[] { 1, 2, 3 };
            var data2 = new byte[] { 4, 5, 6 };

            var checksum1 = _validator.ComputeChecksum(data1);
            var checksum2 = _validator.ComputeChecksum(data2);

            Assert.AreNotEqual(checksum1, checksum2);
        }

        #endregion

        #region SaveMetadata Tests

        [Test]
        public void SaveMetadata_DefaultConstructor_InitializesCorrectly()
        {
            var metadata = new SaveMetadata();

            Assert.IsNotNull(metadata.GameVersion);
            Assert.IsNotNull(metadata.CharacterName);
            Assert.IsNotNull(metadata.LocationName);
            Assert.IsNotNull(metadata.CustomData);
            Assert.GreaterOrEqual(metadata.Timestamp, 0);
        }

        [Test]
        public void SaveMetadata_ParameterizedConstructor_SetsProperties()
        {
            var metadata = new SaveMetadata("1.5.0", "TestChar", 9999f, "TestLoc", 50);

            Assert.AreEqual("1.5.0", metadata.GameVersion);
            Assert.AreEqual("TestChar", metadata.CharacterName);
            Assert.AreEqual(9999f, metadata.PlaytimeSeconds);
            Assert.AreEqual("TestLoc", metadata.LocationName);
            Assert.AreEqual(50, metadata.CharacterLevel);
        }

        [Test]
        public void SaveMetadata_CustomData_CanAddKeyValuePairs()
        {
            var metadata = new SaveMetadata();
            metadata.CustomData["difficulty"] = "Hard";
            metadata.CustomData["mode"] = "Survival";

            Assert.AreEqual("Hard", metadata.CustomData["difficulty"]);
            Assert.AreEqual("Survival", metadata.CustomData["mode"]);
        }

        #endregion

        #region Event Tests

        [Test]
        public void SaveSystem_RaisesSaveCreatedEvent_OnSuccessfulSave()
        {
            SaveCreatedEvent raisedEvent = null;
            ((SaveSystem)_saveSystem).OnSaveCreated += evt => raisedEvent = evt;

            var gameState = new TestGameState { PlayerName = "EventTest", Level = 1 };
            var metadata = new SaveMetadata("1.0.0", "EventTest", 50f, "Start", 1);

            _saveSystem.Save("slot_0", gameState, metadata);

            Assert.IsNotNull(raisedEvent);
            Assert.AreEqual("slot_0", raisedEvent.SlotId);
        }

        [Test]
        public void SaveSystem_AlsoPublishesItsEventsOnTheBus()
        {
            using (var bus = new RPGFramework.Events.EventBus())
            {
                var seen = new List<string>();
                bus.Subscribe<SaveCreatedEvent>(e => seen.Add("created " + e.SlotId));
                bus.Subscribe<SaveLoadedEvent>(e => seen.Add("loaded " + e.SlotId));
                ((SaveSystem)_saveSystem).Events = bus;

                _saveSystem.Save("slot_0", new TestGameState { PlayerName = "Bus", Level = 1 }, new SaveMetadata("1.0.0", "Bus", 1f, "Start", 1));
                _saveSystem.Load<TestGameState>("slot_0");

                Assert.That(seen, Is.EqualTo(new[] { "created slot_0", "loaded slot_0" }));
            }
        }

        [Test]
        public void SaveSystem_RaisesSaveLoadedEvent_OnSuccessfulLoad()
        {
            SaveLoadedEvent raisedEvent = null;
            ((SaveSystem)_saveSystem).OnSaveLoaded += evt => raisedEvent = evt;

            var gameState = new TestGameState { PlayerName = "LoadEventTest", Level = 5 };
            var metadata = new SaveMetadata("1.0.0", "LoadEventTest", 100f, "Town", 5);

            _saveSystem.Save("slot_0", gameState, metadata);
            _saveSystem.Load("slot_0");

            Assert.IsNotNull(raisedEvent);
            Assert.AreEqual("slot_0", raisedEvent.SlotId);
        }

        [Test]
        public void SaveSystem_RaisesSaveDeletedEvent_OnSuccessfulDelete()
        {
            SaveDeletedEvent raisedEvent = null;
            ((SaveSystem)_saveSystem).OnSaveDeleted += evt => raisedEvent = evt;

            var gameState = new TestGameState { PlayerName = "DeleteEventTest", Level = 2 };
            var metadata = new SaveMetadata("1.0.0", "DeleteEventTest", 75f, "Field", 2);

            _saveSystem.Save("slot_0", gameState, metadata);
            _saveSystem.DeleteSave("slot_0");

            Assert.IsNotNull(raisedEvent);
            Assert.AreEqual("slot_0", raisedEvent.SlotId);
        }

        [Test]
        public void SaveSystem_RaisesAutoSaveTriggeredEvent_OnAutoSave()
        {
            AutoSaveTriggeredEvent raisedEvent = null;
            ((SaveSystem)_saveSystem).OnAutoSaveTriggered += evt => raisedEvent = evt;

            var gameState = new TestGameState { PlayerName = "AutoSaveEventTest", Level = 3 };
            var metadata = new SaveMetadata("1.0.0", "AutoSaveEventTest", 150f, "Cave", 3);

            _saveSystem.TriggerAutoSave(gameState, metadata);

            Assert.IsNotNull(raisedEvent);
            Assert.AreEqual("autosave", raisedEvent.SlotId);
        }

        #endregion

        #region Save Result Tests

        [Test]
        public void SaveResult_CreateSuccess_HasCorrectProperties()
        {
            var metadata = new SaveMetadata("1.0.0", "ResultTest", 200f, "Test", 10);
            var result = SaveResult.CreateSuccess("slot_0", metadata);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("slot_0", result.SlotId);
            Assert.AreEqual(SaveError.None, result.Error);
            Assert.AreEqual(metadata, result.Metadata);
        }

        [Test]
        public void SaveResult_CreateFailure_HasCorrectProperties()
        {
            var result = SaveResult.CreateFailure("slot_1", SaveError.IOError, "Test error");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("slot_1", result.SlotId);
            Assert.AreEqual(SaveError.IOError, result.Error);
            Assert.AreEqual("Test error", result.ErrorMessage);
        }

        #endregion

        #region Load Result Tests

        [Test]
        public void LoadResult_CreateSuccess_HasCorrectProperties()
        {
            var gameState = new TestGameState { PlayerName = "LoadResultTest" };
            var metadata = new SaveMetadata("1.0.0", "LoadResultTest", 300f, "Test", 15);
            var result = LoadResult.CreateSuccess("slot_0", gameState, metadata);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("slot_0", result.SlotId);
            Assert.AreEqual(SaveError.None, result.Error);
            Assert.AreEqual(gameState, result.GameState);
            Assert.AreEqual(metadata, result.Metadata);
        }

        [Test]
        public void LoadResult_CreateFailure_HasCorrectProperties()
        {
            var result = LoadResult.CreateFailure("slot_2", SaveError.FileNotFound, "File not found");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("slot_2", result.SlotId);
            Assert.AreEqual(SaveError.FileNotFound, result.Error);
            Assert.AreEqual("File not found", result.ErrorMessage);
        }

        #endregion
    }
}
