using NUnit.Framework;
using System;

namespace RPGFramework.Serialization.Tests
{
    [TestFixture]
    public class TypeRegistryTests
    {
        private ITypeRegistry _registry;

        [SetUp]
        public void Setup()
        {
            _registry = new TypeRegistry();
        }

        [Test]
        public void RegisterType_WithTypeId_CanRetrieveType()
        {
            // Arrange & Act
            _registry.RegisterType<TestData>("test.data.v1", 1);

            // Assert
            var type = _registry.GetType("test.data.v1", 1);
            Assert.AreEqual(typeof(TestData), type);
        }

        [Test]
        public void RegisterType_WithAttribute_UsesAttributeValues()
        {
            // Arrange & Act
            _registry.RegisterType<AttributedData>();

            // Assert
            var type = _registry.GetType("test.attributed.v1", 1);
            Assert.AreEqual(typeof(AttributedData), type);

            var typeId = _registry.GetTypeId<AttributedData>();
            Assert.AreEqual("test.attributed.v1", typeId);

            var version = _registry.GetVersion<AttributedData>();
            Assert.AreEqual(1, version);
        }

        [Test]
        public void RegisterType_DuplicateTypeId_ThrowsException()
        {
            // Arrange
            _registry.RegisterType<TestData>("test.data.v1", 1);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _registry.RegisterType<OtherData>("test.data.v1", 1));
        }

        [Test]
        public void RegisterType_SameTypeTwice_DoesNotThrow()
        {
            // Arrange & Act
            _registry.RegisterType<TestData>("test.data.v1", 1);
            _registry.RegisterType<TestData>("test.data.v1", 1);

            // Assert - no exception thrown
            var type = _registry.GetType("test.data.v1", 1);
            Assert.AreEqual(typeof(TestData), type);
        }

        [Test]
        public void GetType_MultipleVersions_ReturnsCorrectVersion()
        {
            // Arrange
            _registry.RegisterType<TestDataV1>("test.data", 1);
            _registry.RegisterType<TestDataV2>("test.data", 2);

            // Act
            var v1 = _registry.GetType("test.data", 1);
            var v2 = _registry.GetType("test.data", 2);

            // Assert
            Assert.AreEqual(typeof(TestDataV1), v1);
            Assert.AreEqual(typeof(TestDataV2), v2);
        }

        [Test]
        public void GetType_WithoutVersion_ReturnsLatestVersion()
        {
            // Arrange
            _registry.RegisterType<TestDataV1>("test.data", 1);
            _registry.RegisterType<TestDataV2>("test.data", 2);
            _registry.RegisterType<TestDataV3>("test.data", 3);

            // Act
            var latest = _registry.GetType("test.data");

            // Assert
            Assert.AreEqual(typeof(TestDataV3), latest);
        }

        [Test]
        public void GetType_UnregisteredTypeId_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _registry.GetType("unknown.type", 1));
        }

        [Test]
        public void GetTypeId_UnregisteredType_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _registry.GetTypeId<TestData>());
        }

        [Test]
        public void IsRegistered_RegisteredType_ReturnsTrue()
        {
            // Arrange
            _registry.RegisterType<TestData>("test.data.v1", 1);

            // Act & Assert
            Assert.IsTrue(_registry.IsRegistered("test.data.v1", 1));
            Assert.IsTrue(_registry.IsRegistered<TestData>());
        }

        [Test]
        public void IsRegistered_UnregisteredType_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(_registry.IsRegistered("unknown.type", 1));
            Assert.IsFalse(_registry.IsRegistered<TestData>());
        }

        [Test]
        public void RegisterType_NullTypeId_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _registry.RegisterType<TestData>(null, 1));
        }

        [Test]
        public void RegisterType_EmptyTypeId_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _registry.RegisterType<TestData>("", 1));
        }

        [Test]
        public void RegisterType_WithoutAttribute_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _registry.RegisterType<TestData>());
        }

        [Test]
        public void GetVersion_RegisteredType_ReturnsCorrectVersion()
        {
            // Arrange
            _registry.RegisterType<TestData>("test.data", 5);

            // Act
            var version = _registry.GetVersion<TestData>();

            // Assert
            Assert.AreEqual(5, version);
        }

        // Test data types
        private class TestData { }
        private class OtherData { }
        private class TestDataV1 { }
        private class TestDataV2 { }
        private class TestDataV3 { }

        [SerializableType("test.attributed.v1", 1)]
        private class AttributedData { }
    }
}
