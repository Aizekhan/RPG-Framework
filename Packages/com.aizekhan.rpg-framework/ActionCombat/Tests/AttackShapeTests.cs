using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat.Tests
{
    /// <summary>
    /// An attack's shape (MythRunner FG-31): a held wind-up waits at its hold point with the clip standing still; the
    /// attack's motions move the attacker by phase, over the phase's own time; the weapon's sweep, segment by segment
    /// in time, decides contact without physics.
    /// </summary>
    public class AttackShapeTests
    {
        private const int Goblin = 1;
        private const int Runner = 2;

        private EventBus _bus;
        private CombatRules _rules;
        private List<ContactResolvedEvent> _contacts;

        private static readonly CombatVector Forward = new CombatVector(0f, 0f, 1f);

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _rules = new CombatRules(new AnyDamage(), _bus, new CombatTimeline());
            _rules.Add(Goblin);
            _rules.Add(Runner);
            _rules.SetPose(Goblin, CombatVector.Zero, Forward);
            _contacts = new List<ContactResolvedEvent>();
            _bus.Subscribe<ContactResolvedEvent>(_contacts.Add);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        [Test]
        public void AHeldWindUp_WaitsAtItsHoldPoint_WithTheClipStill_UntilLetGo()
        {
            var held = new List<WindUpHeldEvent>();
            _bus.Subscribe<WindUpHeldEvent>(held.Add);
            var swing = new AttackDefinition("wide swing", 0.8f, 0.2f, 0.4f) { HoldPoint = 0.75f };
            _rules.Attack(Goblin, swing);
            Assert.That(_rules.HoldWindUp(Goblin, true), Is.True);

            _rules.Tick(0.5f);
            Assert.That(_rules.AnimationPaceOf(Goblin), Is.EqualTo(1f), "still rising to the hold point");
            _rules.Tick(2f);
            Assert.That(_rules.StateOf(Goblin), Is.EqualTo(CombatState.WindUp), "held");
            Assert.That(_rules.PhaseElapsed(Goblin), Is.EqualTo(0.6f).Within(1e-4f), "at three quarters of the wind-up");
            Assert.That(_rules.AnimationPaceOf(Goblin), Is.EqualTo(0f), "the clip stands raised");
            Assert.That(_rules.IsWindUpHeld(Goblin), Is.True);

            _rules.HoldWindUp(Goblin, false);
            _rules.Tick(0.25f);
            Assert.That(_rules.StateOf(Goblin), Is.EqualTo(CombatState.Active), "the rest of the wind-up, then the blow");
            Assert.That(held.Select(e => e.Held), Is.EqualTo(new[] { true, false }));
            Assert.That(_rules.HoldWindUp(Goblin, true), Is.False, "only in a wind-up");
        }

        [Test]
        public void AMotion_MovesTheAttackerItsDistance_OverItsPartOfThePhase()
        {
            var lunge = new AttackDefinition("lunge", 0.4f, 0.2f, 0.4f)
            {
                Motions = new[]
                {
                    new AttackMotion(AttackPhase.WindUp, AttackMotionKind.Approach, 1.2f, from: 0.5f),
                    new AttackMotion(AttackPhase.Recovery, AttackMotionKind.Retreat, 0.8f),
                    new AttackMotion(AttackPhase.Recovery, AttackMotionKind.StrafeLeft, 0.4f)
                }
            };
            _rules.Attack(Goblin, lunge);

            _rules.Tick(0.1f);
            Assert.That(_rules.MotionOf(Goblin).IsMoving, Is.False, "the first half of the wind-up stands");
            _rules.Tick(0.15f);
            Assert.That(_rules.MotionOf(Goblin).Forward, Is.EqualTo(6f).Within(1e-3f), "1.2 m in 0.2 s");
            _rules.Tick(0.2f);
            Assert.That(_rules.MotionOf(Goblin).IsMoving, Is.False, "the blow stands");
            _rules.Tick(0.2f);
            var back = _rules.MotionOf(Goblin);
            Assert.That(back.Forward, Is.EqualTo(-2f).Within(1e-3f));
            Assert.That(back.Side, Is.EqualTo(-1f).Within(1e-3f), "left");
            var world = back.InWorld(new CombatVector(1f, 0f, 0f));
            Assert.That(world.X, Is.EqualTo(-2f).Within(1e-3f), "back, facing +x");
            Assert.That(world.Z, Is.EqualTo(1f).Within(1e-3f), "the left of +x is +z");

            // Twice the attack speed: the same distance in half the time
            _rules.Tick(1f);
            _rules.SetAttackSpeed(Goblin, 2f);
            _rules.Attack(Goblin, lunge);
            _rules.Tick(0.15f);
            Assert.That(_rules.MotionOf(Goblin).Forward, Is.EqualTo(12f).Within(1e-3f));
        }

        [Test]
        public void ALeap_RisesInAnArc()
        {
            var leap = new AttackDefinition("leap", 0.5f, 0.1f, 0.1f)
            {
                Motions = new[] { new AttackMotion(AttackPhase.WindUp, AttackMotionKind.Leap, 3f, height: 1f) }
            };
            _rules.Attack(Goblin, leap);
            _rules.Tick(0.25f);
            var top = _rules.MotionOf(Goblin);
            Assert.That(top.Height, Is.EqualTo(1f).Within(1e-3f), "the top halfway");
            Assert.That(top.Forward, Is.EqualTo(6f).Within(1e-3f));
            _rules.Tick(0.2f);
            Assert.That(_rules.MotionOf(Goblin).Height, Is.LessThan(0.5f), "coming down");
        }

        [Test]
        public void ASweep_HitsOnlyWhereItsSegmentIsAtThatMoment_WithoutPhysics()
        {
            // A wide swing from the right to the left over the window, then a short thrust ahead
            var swing = new AttackDefinition("swing", 0.1f, 0.4f, 0.2f)
            {
                Sweep = new[]
                {
                    new SweepSegment(0f, 0.5f, 20f, 80f, 1.6f),
                    new SweepSegment(0.5f, 0f, -80f, -20f, 1.6f)
                }
            };
            var left = new CombatVector(-1f, 0f, 0.5f);
            var right = new CombatVector(1f, 0f, 0.5f);
            _rules.Attack(Goblin, swing);
            Assert.That(_rules.Sweep(Goblin, Runner, right), Is.EqualTo(ContactResult.Ignored), "in the wind-up");

            _rules.Tick(0.15f);
            Assert.That(_rules.TryGetSweepSegment(Goblin, out var first), Is.True);
            Assert.That(first.ArcFrom, Is.EqualTo(20f));
            Assert.That(_rules.Sweep(Goblin, Runner, left), Is.EqualTo(ContactResult.Ignored), "the blade is on the right");
            Assert.That(_contacts, Is.Empty, "nothing reported outside the segment");

            _rules.Tick(0.2f);
            Assert.That(_rules.Sweep(Goblin, Runner, left), Is.EqualTo(ContactResult.Hit), "now on the left");
            Assert.That(_rules.Sweep(Goblin, Runner, left), Is.EqualTo(ContactResult.Ignored), "once per swing");
            Assert.That(_contacts, Has.Count.EqualTo(1));
        }

        [Test]
        public void ASegment_TakesTheBodysSize_AndItsHeight()
        {
            var segment = new SweepSegment(0f, 1f, -30f, 30f, 2f, innerRadius: 0.5f, heightMin: 0f, heightMax: 1.5f);
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(0f, 0f, 1.5f)), Is.True);
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(0f, 0f, 2.3f)), Is.False, "beyond the reach");
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(0f, 0f, 2.3f), radius: 0.4f), Is.True, "its body reaches in");
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(0f, 0f, 0.2f)), Is.False, "inside the inner radius");
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(1.2f, 0f, 1.2f)), Is.False, "45 degrees right");
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(1.2f, 0f, 1.2f), radius: 0.5f), Is.True, "a wide body");
            Assert.That(segment.Contains(CombatVector.Zero, Forward, new CombatVector(0f, 2.5f, 1.5f)), Is.False, "above it");
            Assert.That(new SweepSegment(0f, 1f, 0f, 10f, 0f).Problem(), Is.Not.Null, "no radius");
            Assert.That(() => new AttackDefinition("x", 0f, 0.1f, 0f) { Sweep = new[] { new SweepSegment() } },
                Throws.ArgumentException);
        }

        private sealed class AnyDamage : ICombatDamage
        {
            public bool CanHit(int attackerId, int targetId) => true;

            public float Deal(int attackerId, int targetId, AttackContext context) => 1f;
        }
    }
}
