using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RPGFramework.ECS.Tests
{
    public struct HealthComponent : IComponent
    {
        public int Current;
        public int Max;
    }

    public struct SpeedComponent : IComponent
    {
        public float Value;
    }

    public struct BossComponent : IComponent
    {
        public int Phase;
    }

    public sealed class ComponentStorageTests
    {
        private EntityManager _entities;

        [SetUp]
        public void SetUp() => _entities = new EntityManager();

        [Test]
        public void GetComponentRef_MutatesStoredValueInPlace()
        {
            int e = _entities.CreateEntity();
            _entities.AddComponent(e, new HealthComponent { Current = 10, Max = 10 });

            ref var health = ref _entities.GetComponentRef<HealthComponent>(e);
            health.Current -= 3;

            Assert.That(_entities.GetComponent<HealthComponent>(e).Current, Is.EqualTo(7));
        }

        [Test]
        public void GetComponentRef_MissingComponent_Throws()
        {
            int e = _entities.CreateEntity();
            Assert.Throws<KeyNotFoundException>(() => _entities.GetComponentRef<HealthComponent>(e));
        }

        [Test]
        public void AddComponent_Twice_ReplacesValue_AndRaisesAddedOnce()
        {
            int e = _entities.CreateEntity();
            int added = 0;
            _entities.ComponentAdded += (_, __) => added++;

            _entities.AddComponent(e, new SpeedComponent { Value = 1 });
            _entities.AddComponent(e, new SpeedComponent { Value = 2 });

            Assert.That(_entities.GetComponent<SpeedComponent>(e).Value, Is.EqualTo(2));
            Assert.That(_entities.CountWith<SpeedComponent>(), Is.EqualTo(1));
            Assert.That(added, Is.EqualTo(1));
        }

        [Test]
        public void RemoveFromMiddle_KeepsOtherEntitiesValuesIntact()
        {
            var ids = Enumerable.Range(0, 5).Select(i =>
            {
                int e = _entities.CreateEntity();
                _entities.AddComponent(e, new SpeedComponent { Value = i });
                return e;
            }).ToArray();

            _entities.RemoveComponent<SpeedComponent>(ids[1]);
            _entities.DestroyEntity(ids[3]);

            Assert.That(_entities.CountWith<SpeedComponent>(), Is.EqualTo(3));
            Assert.That(_entities.GetComponent<SpeedComponent>(ids[0]).Value, Is.EqualTo(0));
            Assert.That(_entities.GetComponent<SpeedComponent>(ids[2]).Value, Is.EqualTo(2));
            Assert.That(_entities.GetComponent<SpeedComponent>(ids[4]).Value, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(new[] { ids[0], ids[2], ids[4] }, _entities.GetEntitiesWith<SpeedComponent>());
        }

        [Test]
        public void PoolGrowsBeyondInitialCapacity()
        {
            for (int i = 0; i < 100; i++)
            {
                int e = _entities.CreateEntity();
                _entities.AddComponent(e, new HealthComponent { Current = i });
            }

            Assert.That(_entities.CountWith<HealthComponent>(), Is.EqualTo(100));
            Assert.That(_entities.GetComponent<HealthComponent>(100).Current, Is.EqualTo(99));
        }

        [Test]
        public void MultiComponentQueries_ReturnIntersection()
        {
            int a = _entities.CreateEntity();
            int b = _entities.CreateEntity();
            int c = _entities.CreateEntity();
            _entities.AddComponent(a, new HealthComponent());
            _entities.AddComponent(a, new SpeedComponent());
            _entities.AddComponent(b, new HealthComponent());
            _entities.AddComponent(c, new HealthComponent());
            _entities.AddComponent(c, new SpeedComponent());
            _entities.AddComponent(c, new BossComponent());

            CollectionAssert.AreEquivalent(new[] { a, c }, _entities.GetEntitiesWith<HealthComponent, SpeedComponent>());
            CollectionAssert.AreEquivalent(new[] { c }, _entities.GetEntitiesWith<HealthComponent, SpeedComponent, BossComponent>());
            CollectionAssert.IsEmpty(_entities.GetEntitiesWith<BossComponent, ArchetypeTag>());
        }

        [Test]
        public void NonAllocQuery_ClearsAndFillsList()
        {
            int e = _entities.CreateEntity();
            _entities.AddComponent(e, new SpeedComponent());
            var results = new List<int> { 999 };

            _entities.GetEntitiesWith<SpeedComponent>(results);

            CollectionAssert.AreEqual(new[] { e }, results);
        }

        [Test]
        public void QueryResult_IsACopy_SafeToDestroyWhileIterating()
        {
            for (int i = 0; i < 10; i++)
                _entities.AddComponent(_entities.CreateEntity(), new SpeedComponent());

            foreach (int e in _entities.GetEntitiesWith<SpeedComponent>())
                _entities.DestroyEntity(e);

            Assert.That(_entities.EntityCount, Is.Zero);
            Assert.That(_entities.CountWith<SpeedComponent>(), Is.Zero);
        }

        [Test]
        public void Events_ReportLifecycle_DestroyedFiresBeforeComponentsAreRemoved()
        {
            var log = new List<string>();
            _entities.EntityCreated += e => log.Add($"created {e}");
            _entities.ComponentAdded += (e, t) => log.Add($"added {t.Name}");
            _entities.ComponentRemoved += (e, t) => log.Add($"removed {t.Name}");
            _entities.EntityDestroyed += e => log.Add($"destroyed {e} hasHealth={_entities.HasComponent<HealthComponent>(e)}");

            int id = _entities.CreateEntity();
            _entities.AddComponent(id, new HealthComponent());
            _entities.DestroyEntity(id);

            CollectionAssert.AreEqual(new[]
            {
                $"created {id}", "added HealthComponent", $"destroyed {id} hasHealth=True", "removed HealthComponent"
            }, log);
            Assert.That(_entities.Exists(id), Is.False);
        }

        [Test]
        public void GetComponentTypes_ListsEntityComponents()
        {
            int e = _entities.CreateEntity();
            _entities.AddComponent(e, new HealthComponent());
            _entities.AddComponent(e, new SpeedComponent());
            var types = new List<Type>();

            _entities.GetComponentTypes(e, types);

            CollectionAssert.AreEquivalent(new[] { typeof(HealthComponent), typeof(SpeedComponent) }, types);
        }

        [Test]
        public void EntityIds_AreNotReused()
        {
            int first = _entities.CreateEntity();
            _entities.DestroyEntity(first);

            Assert.That(_entities.CreateEntity(), Is.Not.EqualTo(first));
        }
    }

    public sealed class ArchetypeTests
    {
        private EntityManager _entities;
        private ArchetypeCatalog _catalog;
        private EntityArchetype _creature;
        private EntityArchetype _boss;

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            _catalog = new ArchetypeCatalog();
            _creature = _catalog.Define("Creature", b => b
                .With(new HealthComponent { Current = 50, Max = 50 })
                .With(new SpeedComponent { Value = 3 }));
            _boss = _catalog.Define("Boss", b => b
                .Extends(_creature)
                .With(new HealthComponent { Current = 500, Max = 500 })
                .With<BossComponent>());
        }

        [Test]
        public void Create_AddsComponentsAndTag_ChildOverridesParentValues()
        {
            int boss = _catalog.Spawn("Boss", _entities);

            Assert.That(_entities.GetComponent<HealthComponent>(boss).Max, Is.EqualTo(500));
            Assert.That(_entities.GetComponent<SpeedComponent>(boss).Value, Is.EqualTo(3));
            Assert.That(_entities.HasComponent<BossComponent>(boss), Is.True);
            Assert.That(ArchetypeCatalog.GetArchetypeId(_entities, boss), Is.EqualTo("Boss"));
            CollectionAssert.AreEquivalent(
                new[] { typeof(HealthComponent), typeof(SpeedComponent), typeof(BossComponent) }, _boss.ComponentTypes);
        }

        [Test]
        public void Spawn_CustomizeOverridesTemplate()
        {
            int e = _catalog.Spawn("Creature", _entities, id =>
                _entities.AddComponent(id, new SpeedComponent { Value = 9 }));

            Assert.That(_entities.GetComponent<SpeedComponent>(e).Value, Is.EqualTo(9));
        }

        [Test]
        public void FactoryInitializer_RunsPerEntity()
        {
            int counter = 0;
            var catalog = new ArchetypeCatalog();
            catalog.Define("Numbered", b => b.With(() => new HealthComponent { Current = ++counter }));

            int a = catalog.Spawn("Numbered", _entities);
            int b2 = catalog.Spawn("Numbered", _entities);

            Assert.That(_entities.GetComponent<HealthComponent>(a).Current, Is.EqualTo(1));
            Assert.That(_entities.GetComponent<HealthComponent>(b2).Current, Is.EqualTo(2));
        }

        [Test]
        public void Detect_PicksMostSpecificArchetype_AndTagsEntity()
        {
            int plain = _entities.CreateEntity();
            _entities.AddComponent(plain, new HealthComponent());
            _entities.AddComponent(plain, new SpeedComponent());
            int boss = _entities.CreateEntity();
            _entities.AddComponent(boss, new HealthComponent());
            _entities.AddComponent(boss, new SpeedComponent());
            _entities.AddComponent(boss, new BossComponent());
            int other = _entities.CreateEntity();

            Assert.That(_catalog.Detect(_entities, plain), Is.SameAs(_creature));
            Assert.That(_catalog.Detect(_entities, boss), Is.SameAs(_boss));
            Assert.That(_catalog.Detect(_entities, other), Is.Null);
            Assert.That(ArchetypeCatalog.GetArchetypeId(_entities, boss), Is.EqualTo("Boss"));
            Assert.That(ArchetypeCatalog.GetArchetypeId(_entities, other), Is.Null);
        }

        [Test]
        public void Require_PredicateParticipatesInMatching()
        {
            var catalog = new ArchetypeCatalog();
            var wounded = catalog.Define("Wounded", b => b.Require<HealthComponent>(h => h.Current < h.Max / 2));
            int e = _entities.CreateEntity();
            _entities.AddComponent(e, new HealthComponent { Current = 90, Max = 100 });

            Assert.That(wounded.Matches(_entities, e), Is.False);
            _entities.GetComponentRef<HealthComponent>(e).Current = 10;
            Assert.That(wounded.Matches(_entities, e), Is.True);
            Assert.That(wounded.ComponentTypes, Is.Empty);
        }

        [Test]
        public void GetEntities_ByArchetype_OptionallyIncludingDerived()
        {
            int creature = _catalog.Spawn("Creature", _entities);
            int boss = _catalog.Spawn("Boss", _entities);

            CollectionAssert.AreEquivalent(new[] { creature }, _catalog.GetEntities(_entities, "Creature"));
            CollectionAssert.AreEquivalent(new[] { creature, boss }, _catalog.GetEntities(_entities, "Creature", includeDerived: true));
        }

        [Test]
        public void DuplicateId_Throws_UnknownId_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _catalog.Define("Creature", null));
            Assert.Throws<KeyNotFoundException>(() => _catalog.Spawn("Dragon", _entities));
        }
    }
}
