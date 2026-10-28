using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace RPGFramework.ECS.Tests
{
    /// <summary>
    /// A movement-like system over a thousand moving entities among ten thousand: querying two components into a
    /// reused list and updating them in place allocates nothing per frame, and the benchmark prints its numbers to
    /// the test output (budgets are generous: they catch regressions, not jitter).
    /// </summary>
    public sealed class EcsPerformanceTests
    {
        private struct Position : IComponent
        {
            public float X, Y;
        }

        private struct Velocity : IComponent
        {
            public float X, Y;
        }

        private struct Tag : IComponent
        {
        }

        private EntityManager _entities;
        private readonly List<int> _moving = new List<int>(1024);

        [SetUp]
        public void SetUp()
        {
            _entities = new EntityManager();
            for (int i = 0; i < 10000; i++)
            {
                int id = _entities.CreateEntity();
                _entities.AddComponent(id, new Position { X = i, Y = 0f });
                if (i % 10 == 0)
                    _entities.AddComponent(id, new Velocity { X = 1f, Y = 0.5f });
                if (i % 20 == 0)
                    _entities.AddComponent(id, new Tag());
            }
        }

        private void Move(float deltaTime)
        {
            _entities.GetEntitiesWith<Position, Velocity>(_moving);
            for (int i = 0; i < _moving.Count; i++)
            {
                int id = _moving[i];
                var velocity = _entities.GetComponentRef<Velocity>(id);
                ref var position = ref _entities.GetComponentRef<Position>(id);
                position.X += velocity.X * deltaTime;
                position.Y += velocity.Y * deltaTime;
            }
        }

        [Test]
        public void QueryingIntoAList_FindsExactlyTheEntitiesWithAllComponents()
        {
            var both = new List<int>();
            var three = new List<int>();

            _entities.GetEntitiesWith<Position, Velocity>(both);
            _entities.GetEntitiesWith<Position, Velocity, Tag>(three);

            Assert.That(both, Is.EquivalentTo(_entities.GetEntitiesWith<Position, Velocity>()));
            Assert.That(both, Has.Count.EqualTo(1000));
            Assert.That(three, Is.EquivalentTo(_entities.GetEntitiesWith<Position, Velocity, Tag>()));
            Assert.That(three, Has.Count.EqualTo(500));
        }

        [Test]
        [Retry(3)] // the editor sometimes allocates on the test thread mid-measure (seen in CI); a real allocation repeats
        public void AMovementFrame_AllocatesNothing()
        {
            Move(0.016f); // warm up

            Assert.That(() => Move(0.016f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Benchmark_AThousandMovingEntitiesAmongTenThousand()
        {
            const int frames = 200;
            Move(0.016f);
            var clock = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
                Move(0.016f);
            clock.Stop();

            double perFrame = clock.Elapsed.TotalMilliseconds / frames;
            TestContext.WriteLine($"[Benchmark] ECS: 1000 moving of 10000 entities, query + update: {perFrame:0.000} ms per frame");
            Assert.That(perFrame, Is.LessThan(2.0), "an eighth of a 60 fps frame");
        }
    }
}
