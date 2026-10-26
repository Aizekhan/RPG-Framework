using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace RPGFramework.Persistence.Tests
{
    /// <summary>
    /// Saves outlive the build that made them: a section saved with an older shape is upgraded by its participant,
    /// saves from before section versions still load, and a save from a newer build is refused before anything changes.
    /// A damaged latest save falls back to the one before it, and a damaged save never pushes out a good previous one.
    /// </summary>
    public sealed class SaveVersioningTests
    {
        private const string Slot = "slot_0";

        // Version 1 of a wallet: whole gold coins
        public sealed class WalletV1
        {
            public int Gold { get; set; }
        }

        // Version 2: copper coins (100 per gold) and a name for the purse
        public sealed class WalletV2
        {
            public long Copper { get; set; }
            public string Purse { get; set; } = "Old purse";
        }

        private sealed class OldWallet : SaveParticipant<WalletV1>
        {
            public int Gold;
            public override string SaveKey => "wallet";
            protected override WalletV1 CaptureState() => new WalletV1 { Gold = Gold };
            protected override void RestoreState(WalletV1 state) => Gold = state.Gold;
        }

        private sealed class Wallet : SaveParticipant<WalletV2>
        {
            public long Copper;
            public string Purse = "";
            public int SeenVersion = -1;
            public int VersionOverride = 2;

            public override string SaveKey => "wallet";
            public override int Version => VersionOverride;

            protected override WalletV2 CaptureState() => new WalletV2 { Copper = Copper, Purse = Purse };

            protected override void RestoreState(WalletV2 state)
            {
                Copper = state.Copper;
                Purse = state.Purse;
            }

            protected override WalletV2 Upgrade(int savedVersion, byte[] data, ISaveSerializer serializer)
            {
                SeenVersion = savedVersion;
                var old = serializer.Deserialize<WalletV1>(data);
                return new WalletV2 { Copper = old.Gold * 100L };
            }
        }

        // How SaveGameCoordinator wrote saves before section versions existed
        public sealed class SaveGameStateV1
        {
            public int Version { get; set; } = 1;
            public Dictionary<string, string> Sections { get; set; } = new Dictionary<string, string>();
        }

        private string _directory;
        private SaveSystem _saveSystem;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RPGFrameworkTests_Versioning_" + Guid.NewGuid());
            _saveSystem = new SaveSystem(_directory, 3, new JsonSaveSerializer(), new SaveValidator("1.0.0"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private SaveGameCoordinator Coordinator(ISaveParticipant participant) => new SaveGameCoordinator(_saveSystem).Register(participant);

        private string SavePath => Path.Combine(_directory, Slot + ".sav");

        [Test]
        public void ASectionSavedWithAnOlderShape_IsUpgradedByItsParticipant()
        {
            Assert.That(Coordinator(new OldWallet { Gold = 7 }).Save(Slot, new SaveMetadata()).Success, Is.True);

            var wallet = new Wallet();
            var coordinator = Coordinator(wallet);
            var result = coordinator.Load(Slot);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(wallet.SeenVersion, Is.EqualTo(1));
            Assert.That(wallet.Copper, Is.EqualTo(700));
            Assert.That(coordinator.LastUpgradedSections, Is.EqualTo(new[] { "wallet" }));

            // Saved again, it is version 2 and reads without upgrading
            Assert.That(coordinator.Save(Slot, new SaveMetadata()).Success, Is.True);
            var again = new Wallet();
            var second = Coordinator(again);
            Assert.That(second.Load(Slot).Success, Is.True);
            Assert.That(again.SeenVersion, Is.EqualTo(-1));
            Assert.That(again.Copper, Is.EqualTo(700));
            Assert.That(second.LastUpgradedSections, Is.Empty);
        }

        [Test]
        public void ASaveFromBeforeSectionVersions_LoadsWithEverySectionAsVersion1()
        {
            var serializer = new JsonSaveSerializer();
            var old = new SaveGameStateV1();
            old.Sections["wallet"] = Convert.ToBase64String(serializer.Serialize(new WalletV1 { Gold = 3 }));
            Assert.That(_saveSystem.Save(Slot, old, new SaveMetadata()).Success, Is.True);

            var wallet = new Wallet();
            var result = Coordinator(wallet).Load(Slot);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(wallet.SeenVersion, Is.EqualTo(1));
            Assert.That(wallet.Copper, Is.EqualTo(300));
        }

        [Test]
        public void ASaveFromANewerBuild_IsRefused_AndNothingChanges()
        {
            Assert.That(Coordinator(new Wallet { Copper = 5, VersionOverride = 3 }).Save(Slot, new SaveMetadata()).Success, Is.True);

            var wallet = new Wallet { Copper = 42 };
            var result = Coordinator(wallet).Load(Slot);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.IncompatibleVersion));
            Assert.That(result.ErrorMessage, Does.Contain("newer version"));
            Assert.That(wallet.Copper, Is.EqualTo(42));
        }

        [Test]
        public void ADamagedLatestSave_FallsBackToTheOneBeforeIt()
        {
            var wallet = new Wallet { Copper = 100 };
            var coordinator = Coordinator(wallet);
            coordinator.Save(Slot, new SaveMetadata());
            wallet.Copper = 200;
            coordinator.Save(Slot, new SaveMetadata());
            Assert.That(File.Exists(SaveSystem.GetPreviousPath(SavePath)), Is.True);

            var bytes = File.ReadAllBytes(SavePath);
            bytes[bytes.Length / 2] ^= 0x5A; // a disk error, an interrupted copy
            File.WriteAllBytes(SavePath, bytes);
            wallet.Copper = 0;

            var result = coordinator.Load(Slot);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.FromPrevious, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("checksum"));
            Assert.That(wallet.Copper, Is.EqualTo(100));
        }

        [Test]
        public void ALatestSaveWhoseContentsCannotBeUsed_FallsBackToTheOneBeforeIt()
        {
            var wallet = new Wallet { Copper = 100 };
            var coordinator = Coordinator(wallet);
            coordinator.Save(Slot, new SaveMetadata());
            // Intact on disk, but its section is not a wallet (a bug in the build that wrote it)
            var broken = new SaveGameState();
            broken.Sections["wallet"] = "not base64 at all!";
            broken.SectionVersions["wallet"] = 2;
            Assert.That(_saveSystem.Save(Slot, broken, new SaveMetadata()).Success, Is.True);

            var result = coordinator.Load(Slot);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.FromPrevious, Is.True);
            Assert.That(result.ErrorMessage, Does.Contain("wallet"));
            Assert.That(wallet.Copper, Is.EqualTo(100));
        }

        [Test]
        public void ADamagedSave_DoesNotPushOutAGoodPreviousOne()
        {
            var wallet = new Wallet { Copper = 1 };
            var coordinator = Coordinator(wallet);
            coordinator.Save(Slot, new SaveMetadata());
            wallet.Copper = 2;
            coordinator.Save(Slot, new SaveMetadata());
            File.WriteAllText(SavePath, "garbage"); // the latest (2) is damaged; the previous (1) is good

            wallet.Copper = 3;
            coordinator.Save(Slot, new SaveMetadata()); // keeps 1 as the previous, not the garbage
            File.WriteAllText(SavePath, "garbage again");

            var result = coordinator.Load(Slot);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(wallet.Copper, Is.EqualTo(1));
        }

        [Test]
        public void Deleting_AlsoRemovesThePreviousSave()
        {
            var coordinator = Coordinator(new Wallet());
            coordinator.Save(Slot, new SaveMetadata());
            coordinator.Save(Slot, new SaveMetadata());

            Assert.That(_saveSystem.DeleteSave(Slot), Is.True);

            Assert.That(Directory.GetFiles(_directory).Where(f => !f.EndsWith(".tmp")), Is.Empty);
        }
    }
}
