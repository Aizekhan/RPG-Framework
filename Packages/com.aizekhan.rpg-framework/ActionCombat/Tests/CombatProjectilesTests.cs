using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Events;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace RPGFramework.ActionCombat.Tests
{
    /// <summary>
    /// Projectiles without physics: an arrow loosed in an arc strikes whoever is within its radius where it comes
    /// down, led to where a moving target will be; a bolt stops in the first body or flies through; a fallen archer's
    /// arrow strikes no one; with a swing the strike goes through the rules (a dodge counts); a co-op mirror only
    /// flies; nothing in flight makes garbage.
    /// </summary>
    public sealed class CombatProjectilesTests
    {
        private const int Archer = 10;
        private const int Runner = 1;
        private const int Friend = 2;

        private World _world;
        private EventBus _bus;
        private CombatProjectiles _projectiles;
        private List<ProjectileLandedEvent> _landed;
        private List<ProjectileStruckEvent> _struck;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _bus = new EventBus();
            _projectiles = new CombatProjectiles(_world, _bus);
            _landed = new List<ProjectileLandedEvent>();
            _struck = new List<ProjectileStruckEvent>();
            _bus.Subscribe<ProjectileLandedEvent>(_landed.Add);
            _bus.Subscribe<ProjectileStruckEvent>(_struck.Add);
            _world.Place(Archer, new CombatVector(0f, 0f, 0f));
            _world.Place(Runner, new CombatVector(0f, 0f, 10f));
            _world.Place(Friend, new CombatVector(3f, 0f, 10f));
            _world.Targets.Add(Runner);
            _world.Targets.Add(Friend);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private static ProjectileLaunch Arrow(CombatVector velocity, float seconds, float radius = 0.6f) => new ProjectileLaunch
        {
            ShooterId = Archer, TargetId = Runner, From = CombatVector.Zero, Velocity = velocity, Seconds = seconds,
            Radius = radius, VoidIfShooterDies = true, Kind = "arrow"
        };

        private void Run(float seconds, float step = 0.05f)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += step)
                _projectiles.Tick(step);
        }

        [Test]
        public void AnArrow_StrikesWhoeverIsWithinItsRadius_WhereItComesDown()
        {
            int id = _projectiles.Launch(Arrow(new CombatVector(0f, 0f, 20f), 0.5f));
            Assert.That(_projectiles.InFlight.Single().Id, Is.EqualTo(id));
            Run(0.3f);
            Assert.That(_struck, Is.Empty, "nothing on the way");
            Assert.That(_projectiles.Find(id).EndsAt.Z, Is.EqualTo(10f).Within(1e-3f));
            _projectiles.Tick(0.3f);

            var landed = _landed.Single();
            Assert.That(landed.Struck, Is.EqualTo(new[] { Runner }), "the friend 3 m aside is out of its radius");
            Assert.That(landed.Time, Is.EqualTo(0.5f).Within(1e-4f), "when it came down, not the tick's end");
            Assert.That(landed.MissedItsTarget, Is.False);
            Assert.That(_projectiles.InFlight, Is.Empty);
        }

        [Test]
        public void ARunnerWhoChangesLane_HearsItMiss()
        {
            _projectiles.Launch(Arrow(new CombatVector(0f, 0f, 20f), 0.5f));
            _world.Place(Runner, new CombatVector(1.5f, 0f, 10f));
            Run(0.6f);

            Assert.That(_landed.Single().Landed, Is.False);
            Assert.That(_landed.Single().MissedItsTarget, Is.True);
        }

        [Test]
        public void Lead_AimsWhereAMovingTargetWillBe()
        {
            var runnerVelocity = new CombatVector(0f, 0f, 6f);
            Assert.That(CombatProjectiles.Lead(CombatVector.Zero, new CombatVector(0f, 0f, 10f), runnerVelocity, 20f,
                out var velocity, out float seconds), Is.True);
            Assert.That(velocity.Length, Is.EqualTo(20f).Within(1e-3f), "at its own speed");

            _projectiles.Launch(Arrow(velocity, seconds));
            for (int i = 0; i < 20; i++)
            {
                _world.Place(Runner, new CombatVector(0f, 0f, 10f + 6f * (i + 1) * 0.05f));
                _projectiles.Tick(0.05f);
            }

            Assert.That(_landed.Single().Struck, Is.EqualTo(new[] { Runner }), "the runner ran into it");
            Assert.That(CombatProjectiles.Lead(CombatVector.Zero, new CombatVector(0f, 0f, 10f), new CombatVector(0f, 0f, 30f), 20f,
                out _, out _), Is.False, "too fast to catch");
        }

        [Test]
        public void ABolt_StopsInTheFirstBody_OrFliesThroughWithPierce()
        {
            _world.Place(Friend, new CombatVector(0f, 0f, 5f));
            var bolt = Arrow(new CombatVector(0f, 0f, 20f), 2f, radius: 0.4f);
            bolt.Mode = ProjectileHitMode.Contact;
            _projectiles.Launch(bolt);
            Run(1f);
            Assert.That(_landed.Single().Struck, Is.EqualTo(new[] { Friend }), "the first in its path");
            Assert.That(_landed.Single().Time, Is.LessThan(0.5f));

            _landed.Clear();
            bolt.Pierce = 1;
            _projectiles.Launch(bolt);
            Run(2.1f);
            Assert.That(_landed.Single().Struck, Is.EqualTo(new[] { Friend, Runner }));
        }

        [Test]
        public void AFallenArchersArrow_StrikesNoOne()
        {
            _projectiles.Launch(Arrow(new CombatVector(0f, 0f, 20f), 0.5f));
            _world.Dead.Add(Archer);
            Run(0.6f);

            Assert.That(_landed.Single().Voided, Is.True);
            Assert.That(_landed.Single().Landed, Is.False);
            Assert.That(_struck, Is.Empty);
        }

        [Test]
        public void WithASwing_TheStrikeGoesThroughTheRules_AndADodgeCounts()
        {
            var rules = new CombatRules(new AnyDamage(), _bus);
            var projectiles = new CombatProjectiles(_world, _bus, rules);
            rules.Add(Archer);
            rules.Add(Runner, new CombatantSettings { DodgeSeconds = 0.6f, InvulnerableFrom = 0f, InvulnerableUntil = 0.6f });
            var shot = new AttackDefinition("shot", 0f, 0.05f, 0.1f) { Projectile = true, ProjectileLifetime = 5f };
            rules.Attack(Archer, shot);
            int swing = rules.SwingOf(Archer);
            Assert.That(rules.IsSwingLive(swing), Is.True);

            var launch = Arrow(new CombatVector(0f, 0f, 20f), 0.5f);
            launch.SwingId = swing;
            projectiles.Launch(launch);
            rules.Dodge(Runner, new CombatVector(1f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                rules.Tick(0.05f);
                projectiles.Tick(0.05f);
            }

            Assert.That(_struck.Single().Result, Is.EqualTo(ContactResult.Dodged), "rolled under it");
            Assert.That(rules.IsSwingLive(swing), Is.False, "its swing ended when it came down");
        }

        [Test]
        public void AMirror_OnlyFlies_UnderTheHostsId()
        {
            var mirror = Arrow(new CombatVector(0f, 0f, 20f), 0.5f);
            mirror.Mirror = true;
            mirror.Id = 42;

            Assert.That(_projectiles.Launch(mirror), Is.EqualTo(42));
            Run(0.6f);

            Assert.That(_landed.Single().Mirror, Is.True);
            Assert.That(_landed.Single().Landed, Is.False, "the host decides its strikes");
            Assert.That(_projectiles.Launch(Arrow(CombatVector.Zero, 1f)), Is.EqualTo(43), "new ids go on after it");
            Assert.That(_projectiles.Remove(43), Is.True);
        }

        [Test]
        public void ProjectilesInFlight_MakeNoGarbage()
        {
            for (int i = 0; i < 8; i++)
                _projectiles.Launch(Arrow(new CombatVector(0f, 0f, 2f), 10f));
            _projectiles.Tick(0.016f);

            Assert.That(() => _projectiles.Tick(0.016f), Is.Not.AllocatingGCMemory());
        }

        private sealed class World : IEnemyWorld
        {
            private readonly Dictionary<int, CombatVector> _positions = new Dictionary<int, CombatVector>();
            public readonly List<int> Targets = new List<int>();
            public readonly HashSet<int> Dead = new HashSet<int>();

            public void Place(int id, CombatVector position) => _positions[id] = position;

            public IEnumerable<int> TargetsOf(int enemyId) => Targets;

            public bool TryGetPose(int entityId, out CombatVector position, out CombatVector forward)
            {
                forward = new CombatVector(0f, 0f, 1f);
                return _positions.TryGetValue(entityId, out position);
            }

            public bool CanSee(int fromId, int toId) => true;

            public float HealthShare(int entityId) => 1f;

            public bool IsAlive(int entityId) => !Dead.Contains(entityId);
        }

        private sealed class AnyDamage : ICombatDamage
        {
            public bool CanHit(int attackerId, int targetId) => true;

            public float Deal(int attackerId, int targetId, RPGFramework.Combat.AttackContext context) => 1f;
        }
    }
}
