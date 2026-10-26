using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Logging;
using RPGFramework.Pooling.Unity;
using UnityEngine;
using Logger = RPGFramework.Logging.Logger;
using Object = UnityEngine.Object;

namespace RPGFramework.Pooling.Tests
{
    public sealed class ObjectPoolTrackingTests
    {
        private sealed class Item
        {
        }

        private double _now;

        private ObjectPool<Item> CreatePool(MemoryLogSink sink = null, int maxSize = 10) =>
            new ObjectPool<Item>(() => new Item(), initialSize: 0, maxSize: maxSize,
                logger: sink != null ? new Logger(LogSeverity.Trace, sink) : null, poolName: "items")
            {
                Clock = () => _now
            };

        [SetUp]
        public void SetUp() => _now = 0;

        [Test]
        public void DoubleRelease_IsRejected_CountersStayConsistent()
        {
            var sink = new MemoryLogSink();
            var pool = CreatePool(sink);
            var item = pool.Get();

            pool.Release(item);
            pool.Release(item);

            Assert.That(pool.CountActive, Is.Zero);
            Assert.That(pool.CountInactive, Is.EqualTo(1));
            Assert.That(pool.RejectedReleaseCount, Is.EqualTo(1));
            Assert.That(sink.Snapshot().Any(e => e.Severity == LogSeverity.Warning && e.Message.Contains("Rejected")), Is.True);
        }

        [Test]
        public void ForeignObject_IsRejected()
        {
            var pool = CreatePool();

            pool.Release(new Item());

            Assert.That(pool.CountInactive, Is.Zero);
            Assert.That(pool.RejectedReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void FindLeaks_ReportsObjectsActiveLongerThanThreshold_OldestFirst()
        {
            var pool = CreatePool();
            var old = pool.Get();
            _now = 50;
            var newer = pool.Get();
            _now = 100;
            pool.Get(); // fresh

            var leaks = pool.FindLeaks(40);

            Assert.That(leaks.Select(l => l.Instance), Is.EqualTo(new object[] { old, newer }));
            Assert.That(leaks[0].ActiveSeconds, Is.EqualTo(100));
        }

        [Test]
        public void Statistics_IncludeAverageActiveTime_AndInitialSize()
        {
            var pool = CreatePool();
            var a = pool.Get();
            var b = pool.Get();
            _now = 2;
            pool.Release(a);
            _now = 4;
            pool.Release(b);

            var stats = pool.GetStatistics();

            Assert.That(stats.AverageActiveTime, Is.EqualTo(3f));
            Assert.That(stats.TotalReturnCount, Is.EqualTo(2));
        }

        [Test]
        public void Forget_StopsTrackingWithoutPooling()
        {
            var pool = CreatePool();
            var item = pool.Get();

            Assert.That(pool.Forget(item), Is.True);

            Assert.That(pool.CountActive, Is.Zero);
            Assert.That(pool.CountInactive, Is.Zero);
            Assert.That(pool.FindLeaks(0), Is.Empty);
        }

        [Test]
        public void TrimExcess_DestroysOnlyInactiveAboveLimit()
        {
            int destroyed = 0;
            var pool = new ObjectPool<Item>(() => new Item(), onDestroy: _ => destroyed++, initialSize: 5, maxSize: 10);
            var active = pool.Get();

            Assert.That(pool.TrimExcess(1), Is.EqualTo(3));

            Assert.That(destroyed, Is.EqualTo(3));
            Assert.That(pool.CountInactive, Is.EqualTo(1));
            Assert.That(pool.IsActive(active), Is.True);
        }
    }

    public sealed class PoolManagerTests
    {
        private sealed class Bullet
        {
        }

        private sealed class Coin
        {
        }

        private double _now;
        private PoolManager _manager;

        [SetUp]
        public void SetUp()
        {
            _now = 0;
            _manager = new PoolManager();
        }

        private ObjectPool<T> Register<T>(string key, string owner = null) where T : class, new()
        {
            var pool = new ObjectPool<T>(() => new T(), initialSize: 2, maxSize: 10, poolName: key) { Clock = () => _now };
            _manager.Register(key, pool, owner);
            return pool;
        }

        [Test]
        public void Get_ReturnsTypedPool_WrongTypeOrMissingKey_Throws()
        {
            Register<Bullet>("bullets");

            Assert.That(_manager.Get<Bullet>("bullets").Get(), Is.InstanceOf<Bullet>());
            Assert.Throws<InvalidCastException>(() => _manager.Get<Coin>("bullets"));
            Assert.Throws<KeyNotFoundException>(() => _manager.Get<Bullet>("missing"));
            Assert.Throws<InvalidOperationException>(() => Register<Bullet>("bullets"));
        }

        [Test]
        public void RemoveOwnedBy_ClearsOnlyThatOwnersPools()
        {
            Register<Bullet>("level1-bullets", owner: "Level1");
            Register<Coin>("level1-coins", owner: "Level1");
            Register<Bullet>("global-bullets");

            Assert.That(_manager.RemoveOwnedBy("Level1"), Is.EqualTo(2));

            CollectionAssert.AreEquivalent(new[] { "global-bullets" }, _manager.Keys);
        }

        [Test]
        public void Statistics_LeaksAndTotals_AggregateAcrossPools()
        {
            var bullets = Register<Bullet>("bullets");
            var coins = Register<Coin>("coins");
            bullets.Get();
            _now = 200;
            coins.Get();

            Assert.That(_manager.TotalActive, Is.EqualTo(2));
            Assert.That(_manager.FindLeaks(100).Count, Is.EqualTo(1));
            var stats = _manager.GetAllStatistics();
            CollectionAssert.AreEquivalent(new[] { "bullets", "coins" }, stats.Select(s => s.PoolKey));
            Assert.That(_manager.TrimExcess(0), Is.EqualTo(2)); // one inactive left in each pool
        }

        [Test]
        public void Monitor_ReportsEachLeakOnce_AndAgainAfterReuse()
        {
            var sink = new MemoryLogSink();
            var bullets = Register<Bullet>("bullets");
            var monitor = new PoolMonitor(_manager, new Logger(LogSeverity.Trace, sink))
            {
                LeakThresholdSeconds = 10,
                LeakCheckIntervalSeconds = 1
            };
            var bullet = bullets.Get();

            _now = 20;
            monitor.Update(1f);
            monitor.Update(1f);
            Assert.That(monitor.ReportedLeakCount, Is.EqualTo(1));

            bullets.Release(bullet);
            monitor.Update(1f);
            var again = bullets.Get();
            _now = 40;
            monitor.Update(1f);

            Assert.That(again, Is.SameAs(bullet));
            Assert.That(monitor.ReportedLeakCount, Is.EqualTo(2));
            Assert.That(sink.Snapshot().Count(e => e.Message.Contains("Possible pool leak")), Is.EqualTo(2));
        }
    }

    public sealed class GameObjectPoolTests
    {
        private sealed class ResetCounter : MonoBehaviour, IPoolable
        {
            public int Spawned;
            public int Despawned;
            public void OnSpawned() => Spawned++;
            public void OnDespawned() => Despawned++;
        }

        private GameObject _prefab;
        private GameObjectPool _pool;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Enemy");
            _prefab.AddComponent<ResetCounter>();
            _pool = new GameObjectPool(_prefab, initialSize: 2, maxSize: 4);
        }

        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void PrewarmedInstances_AreInactiveUnderContainer_AndMarked()
        {
            Assert.That(_pool.CountInactive, Is.EqualTo(2));
            Assert.That(_pool.Container.childCount, Is.EqualTo(2));
            foreach (Transform child in _pool.Container)
            {
                Assert.That(child.gameObject.activeSelf, Is.False);
                Assert.That(child.GetComponent<PooledObject>().Pool, Is.SameAs(_pool));
            }
        }

        [Test]
        public void Spawn_ActivatesPlacesAndNotifies_ReturnToPool_Reverses()
        {
            var parent = new GameObject("Parent").transform;
            try
            {
                var enemy = _pool.Spawn(new Vector3(1, 2, 3), Quaternion.identity, parent);
                var counter = enemy.GetComponent<ResetCounter>();

                Assert.That(enemy.activeSelf, Is.True);
                Assert.That(enemy.transform.parent, Is.SameAs(parent));
                Assert.That(enemy.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
                Assert.That(counter.Spawned, Is.EqualTo(1));
                Assert.That(enemy.GetComponent<PooledObject>().IsSpawned, Is.True);

                enemy.GetComponent<PooledObject>().ReturnToPool();

                Assert.That(enemy.activeSelf, Is.False);
                Assert.That(enemy.transform.parent, Is.SameAs(_pool.Container));
                Assert.That(counter.Despawned, Is.EqualTo(1));
                Assert.That(_pool.CountActive, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void ExternallyDestroyedInstance_IsForgotten_NotLeaked()
        {
            var enemy = _pool.Get();
            Object.DestroyImmediate(enemy);

            _pool.Release(enemy);

            Assert.That(_pool.CountActive, Is.Zero);
            Assert.That(_pool.FindLeaks(0), Is.Empty);
        }

        [Test]
        public void ReleaseBeyondMaxSize_DestroysInstance()
        {
            var spawned = Enumerable.Range(0, 6).Select(_ => _pool.Get()).ToList();

            foreach (var instance in spawned)
                _pool.Release(instance);

            Assert.That(_pool.CountInactive, Is.EqualTo(4));
            Assert.That(spawned.Count(go => go == null), Is.EqualTo(2));
        }

        [Test]
        public void Dispose_DestroysOwnedContainer_ReturnToPoolThenDestroysInstance()
        {
            var enemy = _pool.Get();
            var container = _pool.Container.gameObject;

            _pool.Dispose();
            Assert.That(container == null, Is.True);

            enemy.GetComponent<PooledObject>().ReturnToPool();
            Assert.That(enemy == null, Is.True);
        }

        [Test]
        public void ManagerExtensions_CreateAndSpawnByKey()
        {
            var manager = new PoolManager();
            var pool = manager.CreateGameObjectPool("enemies", _prefab, owner: "Level1");
            try
            {
                var enemy = manager.Spawn("enemies", Vector3.one, Quaternion.identity);

                Assert.That(pool.IsActive(enemy), Is.True);
                Assert.That(manager.RemoveOwnedBy("Level1"), Is.EqualTo(1));
            }
            finally
            {
                pool.Dispose();
            }
        }
    }
}
