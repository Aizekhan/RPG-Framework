using NUnit.Framework;
using System;

namespace RPGFramework.Pooling.Tests
{
    [TestFixture]
    public class ObjectPoolTests
    {
        private class TestObject
        {
            public int Id { get; set; }
            public bool IsActive { get; set; }
        }

        [Test]
        public void Constructor_WithNullCreateFunc_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ObjectPool<TestObject>(null));
        }

        [Test]
        public void Constructor_WithNegativeMaxSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPool<TestObject>(
                () => new TestObject(),
                maxSize: -1
            ));
        }

        [Test]
        public void Constructor_WithZeroMaxSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectPool<TestObject>(
                () => new TestObject(),
                maxSize: 0
            ));
        }

        [Test]
        public void Constructor_WithInitialSize_PreWarmsPool()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 5,
                maxSize: 10
            );

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(5, pool.CountInactive);
            Assert.AreEqual(5, pool.TotalSize);
        }

        [Test]
        public void Get_FromPreWarmedPool_ReusesObject()
        {
            int createCount = 0;
            var pool = new ObjectPool<TestObject>(
                () => { createCount++; return new TestObject { Id = createCount }; },
                initialSize: 3,
                maxSize: 10
            );

            Assert.AreEqual(3, createCount, "Pre-warming should create 3 objects");

            var obj = pool.Get();

            Assert.IsNotNull(obj);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(2, pool.CountInactive);
            Assert.AreEqual(3, createCount, "Get should reuse pre-warmed object, not create new");
        }

        [Test]
        public void Get_FromEmptyPool_CreatesNewObject()
        {
            int createCount = 0;
            var pool = new ObjectPool<TestObject>(
                () => { createCount++; return new TestObject { Id = createCount }; },
                initialSize: 0,
                maxSize: 10
            );

            var obj = pool.Get();

            Assert.IsNotNull(obj);
            Assert.AreEqual(1, obj.Id);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            Assert.AreEqual(1, createCount);
        }

        [Test]
        public void Get_InvokesOnGetCallback()
        {
            int callbackCount = 0;
            TestObject lastCallback = null;

            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                onGet: obj => { callbackCount++; lastCallback = obj; },
                initialSize: 0,
                maxSize: 10
            );

            var obj = pool.Get();

            Assert.AreEqual(1, callbackCount);
            Assert.AreSame(obj, lastCallback);
        }

        [Test]
        public void Release_ToNonFullPool_ReturnsObjectToPool()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 0,
                maxSize: 10
            );

            var obj = pool.Get();
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            pool.Release(obj);

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
        }

        [Test]
        public void Release_Null_DoesNotCrash()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 0,
                maxSize: 10
            );

            Assert.DoesNotThrow(() => pool.Release(null));
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void Release_InvokesOnReleaseCallback()
        {
            int callbackCount = 0;
            TestObject lastCallback = null;

            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                onRelease: obj => { callbackCount++; lastCallback = obj; },
                initialSize: 0,
                maxSize: 10
            );

            var obj = pool.Get();
            pool.Release(obj);

            Assert.AreEqual(1, callbackCount);
            Assert.AreSame(obj, lastCallback);
        }

        [Test]
        public void Release_WhenPoolFull_InvokesOnDestroyCallback()
        {
            int destroyCount = 0;
            TestObject lastDestroyed = null;

            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                onDestroy: obj => { destroyCount++; lastDestroyed = obj; },
                initialSize: 2,
                maxSize: 2
            );

            var obj1 = pool.Get();
            var obj2 = pool.Get();
            var obj3 = pool.Get(); // Creates new object (pool was empty)

            pool.Release(obj1);
            pool.Release(obj2);

            Assert.AreEqual(0, destroyCount, "Pool has capacity for 2 objects");

            pool.Release(obj3);

            Assert.AreEqual(1, destroyCount, "Third object should be destroyed (pool full)");
            Assert.AreSame(obj3, lastDestroyed);
        }

        [Test]
        public void Clear_EmptiesPool()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 5,
                maxSize: 10
            );

            pool.Get();
            pool.Get();

            Assert.AreEqual(2, pool.CountActive);
            Assert.AreEqual(3, pool.CountInactive);

            pool.Clear();

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void Clear_InvokesOnDestroyForPooledObjects()
        {
            int destroyCount = 0;

            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                onDestroy: obj => destroyCount++,
                initialSize: 5,
                maxSize: 10
            );

            pool.Clear();

            Assert.AreEqual(5, destroyCount, "OnDestroy should be called for all pre-warmed objects");
        }

        [Test]
        public void Resize_ExpandsPool()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 2,
                maxSize: 10
            );

            pool.Resize(5);

            Assert.AreEqual(5, pool.CountInactive);
        }

        [Test]
        public void Resize_ShrinksPool()
        {
            int destroyCount = 0;

            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                onDestroy: obj => destroyCount++,
                initialSize: 5,
                maxSize: 10
            );

            pool.Resize(2);

            Assert.AreEqual(2, pool.CountInactive);
            Assert.AreEqual(3, destroyCount, "3 objects should be destroyed");
        }

        [Test]
        public void Resize_WithNegativeSize_ThrowsArgumentOutOfRangeException()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 2,
                maxSize: 10
            );

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Resize(-1));
        }

        [Test]
        public void GetStatistics_ReturnsAccurateStats()
        {
            int createCount = 0;
            var pool = new ObjectPool<TestObject>(
                () => { createCount++; return new TestObject(); },
                initialSize: 3,
                maxSize: 10
            );

            var obj1 = pool.Get();
            var obj2 = pool.Get();
            pool.Release(obj1);

            var stats = pool.GetStatistics();

            Assert.AreEqual(typeof(TestObject).Name, stats.PoolType);
            Assert.AreEqual(1, stats.ActiveCount); // obj2 still active
            Assert.AreEqual(2, stats.InactiveCount); // started with 3, got 2 (leaving 1), released 1 (now 2)
            Assert.AreEqual(3, stats.TotalCreated); // initial pool size
            Assert.AreEqual(2, stats.TotalGetCount); // got obj1, obj2
            Assert.AreEqual(1, stats.TotalReturnCount); // released obj1
            Assert.AreEqual(2, stats.PeakActiveCount); // peak when both obj1 and obj2 were active
        }

        [Test]
        public void PeakActiveCount_TracksMaximumActiveObjects()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 0,
                maxSize: 10
            );

            var obj1 = pool.Get();
            var obj2 = pool.Get();
            var obj3 = pool.Get();

            var stats1 = pool.GetStatistics();
            Assert.AreEqual(3, stats1.PeakActiveCount);

            pool.Release(obj1);
            pool.Release(obj2);

            var stats2 = pool.GetStatistics();
            Assert.AreEqual(3, stats2.PeakActiveCount, "Peak should remain at 3 even after releases");
        }

        [Test]
        public void Roundtrip_GetAndRelease_MaintainsPoolIntegrity()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 5,
                maxSize: 10
            );

            var objects = new TestObject[5];
            for (int i = 0; i < 5; i++)
            {
                objects[i] = pool.Get();
            }

            Assert.AreEqual(5, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            for (int i = 0; i < 5; i++)
            {
                pool.Release(objects[i]);
            }

            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(5, pool.CountInactive);
        }

        [Test]
        public void PoolStatistics_HitRate_CalculatesCorrectly()
        {
            var pool = new ObjectPool<TestObject>(
                () => new TestObject(),
                initialSize: 2,
                maxSize: 10
            );

            // First 2 Gets reuse pre-warmed objects (100% hit rate)
            pool.Get();
            pool.Get();
            var stats1 = pool.GetStatistics();
            Assert.AreEqual(1.0f, stats1.HitRate, 0.01f, "2 Gets, 2 pre-warmed = 100% reuse");

            // Third Get creates new object (66% hit rate)
            pool.Get();
            var stats2 = pool.GetStatistics();
            Assert.AreEqual(0.66f, stats2.HitRate, 0.01f, "3 Gets, 2 reused, 1 created = 66%");
        }
    }
}
