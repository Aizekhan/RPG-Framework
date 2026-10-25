using NUnit.Framework;
using System;

namespace RPGFramework.Serialization.Tests
{
    [TestFixture]
    public class VersionedSerializerTests
    {
        private ITypeRegistry _typeRegistry;
        private IMigrationRegistry _migrationRegistry;
        private VersionedSerializer _serializer;

        [SetUp]
        public void Setup()
        {
            _typeRegistry = new TypeRegistry();
            _migrationRegistry = new MigrationRegistry();
            _serializer = new VersionedSerializer(_typeRegistry, _migrationRegistry);
        }

        [Test]
        public void Serialize_Deserialize_Roundtrip_Succeeds()
        {
            // Arrange
            _typeRegistry.RegisterType<TestDataV1>("test.data", 1);
            var original = new TestDataV1 { Value = 42, Name = "Test" };

            // Act
            var bytes = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestDataV1>(bytes);

            // Assert
            Assert.AreEqual(original.Value, deserialized.Value);
            Assert.AreEqual(original.Name, deserialized.Name);
        }

        [Test]
        public void Deserialize_TypeMismatch_ThrowsException()
        {
            // Arrange
            _typeRegistry.RegisterType<TestDataV1>("test.data.v1", 1);
            _typeRegistry.RegisterType<OtherData>("test.data.v2", 1);

            var data = new TestDataV1 { Value = 42, Name = "Test" };
            var bytes = _serializer.Serialize(data);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _serializer.Deserialize<OtherData>(bytes));
        }

        [Test]
        public void Deserialize_NewerVersion_ThrowsException()
        {
            // Arrange
            _typeRegistry.RegisterType<TestDataV1>("test.data", 1);
            _typeRegistry.RegisterType<TestDataV2>("test.data", 2);

            var newData = new TestDataV2 { Value = 42, Name = "Test", Extra = "New" };
            var bytes = _serializer.Serialize(newData);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _serializer.Deserialize<TestDataV1>(bytes));
            Assert.That(ex.Message, Does.Contain("newer version"));
        }

        [Test]
        public void Deserialize_WithMigration_UpgradesData()
        {
            // Arrange
            _typeRegistry.RegisterType<TestDataV1>("test.data", 1);
            _typeRegistry.RegisterType<TestDataV2>("test.data", 2);

            var migrator = new TestDataV1ToV2Migrator();
            _migrationRegistry.RegisterMigration("test.data", 1, 2, migrator);

            var oldData = new TestDataV1 { Value = 42, Name = "Test" };
            var bytes = _serializer.Serialize(oldData);

            // Act
            var upgraded = _serializer.Deserialize<TestDataV2>(bytes);

            // Assert
            Assert.AreEqual(42, upgraded.Value);
            Assert.AreEqual("Test", upgraded.Name);
            Assert.AreEqual("migrated", upgraded.Extra);
        }

        [Test]
        public void Deserialize_NoMigration_ThrowsException()
        {
            // Arrange
            _typeRegistry.RegisterType<TestDataV1>("test.data", 1);
            _typeRegistry.RegisterType<TestDataV2>("test.data", 2);

            var oldData = new TestDataV1 { Value = 42, Name = "Test" };
            var bytes = _serializer.Serialize(oldData);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                _serializer.Deserialize<TestDataV2>(bytes));
            Assert.That(ex.Message, Does.Contain("No migration registered"));
        }

        [Test]
        public void DeserializePolymorphic_ReturnsCorrectType()
        {
            // Arrange
            _typeRegistry.RegisterType<TestDataV1>("test.data", 1);
            var original = new TestDataV1 { Value = 42, Name = "Test" };
            var bytes = _serializer.Serialize(original);

            // Act
            var deserialized = _serializer.DeserializePolymorphic(bytes);

            // Assert
            Assert.IsInstanceOf<TestDataV1>(deserialized);
            var typed = (TestDataV1)deserialized;
            Assert.AreEqual(42, typed.Value);
            Assert.AreEqual("Test", typed.Name);
        }

        // Test data classes
        [SerializableType("test.data", 1)]
        public class TestDataV1 : ISerializable
        {
            public int Value { get; set; }
            public string Name { get; set; }

            public byte[] Serialize()
            {
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(Value);
                    writer.Write(Name ?? "");
                    return stream.ToArray();
                }
            }

            public void Deserialize(byte[] data)
            {
                using (var stream = new System.IO.MemoryStream(data))
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    Value = reader.ReadInt32();
                    Name = reader.ReadString();
                }
            }
        }

        [SerializableType("test.data", 2)]
        public class TestDataV2 : ISerializable
        {
            public int Value { get; set; }
            public string Name { get; set; }
            public string Extra { get; set; }

            public byte[] Serialize()
            {
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(Value);
                    writer.Write(Name ?? "");
                    writer.Write(Extra ?? "");
                    return stream.ToArray();
                }
            }

            public void Deserialize(byte[] data)
            {
                using (var stream = new System.IO.MemoryStream(data))
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    Value = reader.ReadInt32();
                    Name = reader.ReadString();
                    Extra = reader.ReadString();
                }
            }
        }

        [SerializableType("test.other", 1)]
        public class OtherData : ISerializable
        {
            public int Value { get; set; }

            public byte[] Serialize()
            {
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.BinaryWriter(stream))
                {
                    writer.Write(Value);
                    return stream.ToArray();
                }
            }

            public void Deserialize(byte[] data)
            {
                using (var stream = new System.IO.MemoryStream(data))
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    Value = reader.ReadInt32();
                }
            }
        }

        public class TestDataV1ToV2Migrator : IMigrator<TestDataV1, TestDataV2>
        {
            public TestDataV2 Migrate(TestDataV1 oldVersion)
            {
                return new TestDataV2
                {
                    Value = oldVersion.Value,
                    Name = oldVersion.Name,
                    Extra = "migrated"
                };
            }
        }
    }
}
