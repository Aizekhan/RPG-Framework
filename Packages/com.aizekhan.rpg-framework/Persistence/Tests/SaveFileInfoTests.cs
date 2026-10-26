using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace RPGFramework.Persistence.Tests
{
    /// <summary>
    /// Metadata and checksum survive a restart (a new SaveSystem over the same directory).
    /// </summary>
    public sealed class SaveFileInfoTests
    {
        public class State
        {
            public string Hero { get; set; }
            public int Gold { get; set; }
        }

        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_SaveInfo_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private SaveSystem CreateSystem() => new SaveSystem(_directory, 2, new JsonSaveSerializer(), new SaveValidator("1.0.0"));

        private static SaveMetadata Metadata()
        {
            var metadata = new SaveMetadata("1.0.0", "Aria", 3600f, "Old Mill", 7);
            metadata.CustomData["difficulty"] = "hard";
            return metadata;
        }

        [Test]
        public void Metadata_IsAvailableAfterRestart()
        {
            Assert.That(CreateSystem().Save("slot_0", new State { Hero = "Aria", Gold = 5 }, Metadata()).Success, Is.True);

            var restarted = CreateSystem();
            var metadata = restarted.GetMetadata("slot_0");

            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.CharacterName, Is.EqualTo("Aria"));
            Assert.That(metadata.CharacterLevel, Is.EqualTo(7));
            Assert.That(metadata.LocationName, Is.EqualTo("Old Mill"));
            Assert.That(metadata.PlaytimeSeconds, Is.EqualTo(3600f));
            Assert.That(metadata.CustomData["difficulty"], Is.EqualTo("hard"));

            var loaded = restarted.Load<State>("slot_0");
            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Metadata.CharacterName, Is.EqualTo("Aria"));
            Assert.That(((State)loaded.GameState).Gold, Is.EqualTo(5));
        }

        [Test]
        public void TamperedSave_FailsTheChecksum()
        {
            CreateSystem().Save("slot_1", new State { Hero = "Aria", Gold = 5 }, Metadata());
            var path = Path.Combine(_directory, "slot_1.sav");
            File.WriteAllText(path, File.ReadAllText(path).Replace("5", "9999"));

            var result = CreateSystem().Load<State>("slot_1");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.InvalidChecksum));
        }

        [Test]
        public void SaveWithoutSidecar_StillLoads_AndDeleteRemovesBothFiles()
        {
            var system = CreateSystem();
            system.Save("slot_0", new State { Hero = "Old", Gold = 1 }, Metadata());
            File.Delete(Path.Combine(_directory, "slot_0.info"));

            var restarted = CreateSystem();
            Assert.That(restarted.Load<State>("slot_0").Success, Is.True);
            Assert.That(restarted.GetMetadata("slot_0"), Is.Null);

            restarted.Save("slot_0", new State(), Metadata());
            Assert.That(restarted.DeleteSave("slot_0"), Is.True);
            Assert.That(Directory.GetFiles(_directory), Is.Empty);
        }
    }
}
