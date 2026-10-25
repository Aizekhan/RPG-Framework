using NUnit.Framework;

namespace RPGFramework.ECS.Tests
{
    public sealed class EntityManagerTests
    {
        private EntityManager _entityManager = null!;

        private struct PositionComponent : IComponent
        {
            public int X;
            public int Y;
        }

        [SetUp]
        public void SetUp()
        {
            _entityManager = new EntityManager();
        }

        [Test]
        public void CreateEntity_ReturnsDistinctIncreasingIds()
        {
            int first = _entityManager.CreateEntity();
            int second = _entityManager.CreateEntity();

            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.GreaterThan(first));
            CollectionAssert.AreEquivalent(new[] { first, second }, _entityManager.GetAllEntities());
        }

        [Test]
        public void AddComponent_CanBeQueriedAndRetrieved()
        {
            int entityId = _entityManager.CreateEntity();
            var expected = new PositionComponent { X = 4, Y = 9 };

            _entityManager.AddComponent(entityId, expected);

            Assert.That(_entityManager.HasComponent<PositionComponent>(entityId), Is.True);
            Assert.That(_entityManager.GetComponent<PositionComponent>(entityId).X, Is.EqualTo(4));
            Assert.That(_entityManager.GetComponent<PositionComponent>(entityId).Y, Is.EqualTo(9));
            CollectionAssert.Contains(_entityManager.GetEntitiesWith<PositionComponent>(), entityId);
        }

        [Test]
        public void AddComponent_RejectsUnknownOrDestroyedEntityWithoutCreatingPhantoms()
        {
            int destroyedEntityId = _entityManager.CreateEntity();
            _entityManager.DestroyEntity(destroyedEntityId);

            Assert.Throws<System.ArgumentException>(() =>
                _entityManager.AddComponent(destroyedEntityId, new PositionComponent { X = 1, Y = 2 }));
            Assert.Throws<System.ArgumentException>(() =>
                _entityManager.AddComponent(100, new PositionComponent { X = 3, Y = 4 }));

            CollectionAssert.IsEmpty(_entityManager.GetAllEntities());
        }

        [Test]
        public void TryGetComponent_ReturnsFalseWhenComponentIsMissing()
        {
            int entityId = _entityManager.CreateEntity();

            bool found = _entityManager.TryGetComponent<PositionComponent>(entityId, out var component);

            Assert.That(found, Is.False);
            Assert.That(component.X, Is.EqualTo(0));
            Assert.That(component.Y, Is.EqualTo(0));
        }

        [Test]
        public void RemoveComponent_UpdatesComponentIndex()
        {
            int entityId = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityId, new PositionComponent { X = 2, Y = 3 });

            _entityManager.RemoveComponent<PositionComponent>(entityId);

            Assert.That(_entityManager.HasComponent<PositionComponent>(entityId), Is.False);
            CollectionAssert.DoesNotContain(_entityManager.GetEntitiesWith<PositionComponent>(), entityId);
            CollectionAssert.Contains(_entityManager.GetAllEntities(), entityId);
        }

        [Test]
        public void DestroyEntity_RemovesEntityAndItsComponentIndexEntries()
        {
            int entityId = _entityManager.CreateEntity();
            _entityManager.AddComponent(entityId, new PositionComponent { X = 8, Y = 5 });

            _entityManager.DestroyEntity(entityId);

            CollectionAssert.DoesNotContain(_entityManager.GetAllEntities(), entityId);
            CollectionAssert.DoesNotContain(_entityManager.GetEntitiesWith<PositionComponent>(), entityId);
            Assert.That(_entityManager.HasComponent<PositionComponent>(entityId), Is.False);
        }

        [Test]
        public void DestroyUnknownEntity_IsANoOp()
        {
            int entityId = _entityManager.CreateEntity();

            Assert.DoesNotThrow(() => _entityManager.DestroyEntity(entityId + 100));
            CollectionAssert.AreEqual(new[] { entityId }, _entityManager.GetAllEntities());
        }

        [Test]
        public void GetComponent_ReturnsDefaultWhenComponentIsMissing()
        {
            int entityId = _entityManager.CreateEntity();

            var component = _entityManager.GetComponent<PositionComponent>(entityId);

            Assert.That(component.X, Is.EqualTo(0));
            Assert.That(component.Y, Is.EqualTo(0));
        }
    }
}
