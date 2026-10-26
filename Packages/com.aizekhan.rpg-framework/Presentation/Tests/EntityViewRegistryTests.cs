using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGFramework.Presentation.Tests
{
    public sealed class EntityViewRegistryTests
    {
        private readonly List<Object> _cleanup = new List<Object>();
        private EntityViewRegistry _registry;

        [SetUp]
        public void SetUp() => _registry = new EntityViewRegistry();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _cleanup.Clear();
        }

        private GameObject View(string name)
        {
            var go = new GameObject(name);
            _cleanup.Add(go);
            return go;
        }

        [Test]
        public void Bind_LinksBothWays()
        {
            var goblin = View("Goblin");

            var view = _registry.Bind(7, goblin);

            Assert.That(view.EntityId, Is.EqualTo(7));
            Assert.That(_registry.TryGetView(7, out var found), Is.True);
            Assert.That(found, Is.SameAs(view));
            Assert.That(_registry.TryGetEntity(goblin, out int entity), Is.True);
            Assert.That(entity, Is.EqualTo(7));
            Assert.That(_registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryGetEntity_FindsTheEntityFromAChildCollider()
        {
            var goblin = View("Goblin");
            var weapon = new GameObject("Weapon");
            weapon.transform.SetParent(goblin.transform);
            _registry.Bind(3, goblin);

            Assert.That(_registry.TryGetEntity(weapon, out int entity), Is.True);
            Assert.That(entity, Is.EqualTo(3));
        }

        [Test]
        public void Unbind_ReturnsTheViewAndForgetsIt()
        {
            var goblin = View("Goblin");
            _registry.Bind(7, goblin);

            Assert.That(_registry.Unbind(7), Is.SameAs(goblin));

            Assert.That(goblin.GetComponent<EntityView>().IsBound, Is.False);
            Assert.That(_registry.TryGetView(7, out _), Is.False);
            Assert.That(_registry.TryGetEntity(goblin, out _), Is.False);
            Assert.That(_registry.Unbind(7), Is.Null);
        }

        [Test]
        public void PooledView_CanBeReboundToAnotherEntity()
        {
            var goblin = View("Goblin");
            _registry.Bind(1, goblin);
            _registry.Unbind(1);

            _registry.Bind(2, goblin);

            Assert.That(_registry.TryGetEntity(goblin, out int entity), Is.True);
            Assert.That(entity, Is.EqualTo(2));
        }

        [Test]
        public void Bind_RejectsDoubleBinding()
        {
            var first = View("First");
            var second = View("Second");
            _registry.Bind(1, first);

            Assert.Throws<InvalidOperationException>(() => _registry.Bind(1, second));
            Assert.Throws<InvalidOperationException>(() => _registry.Bind(2, first));
        }

        [Test]
        public void DestroyedView_IsNotReturned()
        {
            var goblin = View("Goblin");
            _registry.Bind(5, goblin);
            Object.DestroyImmediate(goblin);

            Assert.That(_registry.TryGetView(5, out _), Is.False);
            Assert.That(_registry.Unbind(5), Is.Null);
            Assert.DoesNotThrow(() => _registry.Bind(5, View("Replacement")));
        }
    }
}
