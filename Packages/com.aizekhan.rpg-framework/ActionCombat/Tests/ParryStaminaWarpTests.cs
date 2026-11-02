using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat.Tests
{
    /// <summary>
    /// Parry, stamina for attacks and motion warping (WhiteWrite, the UMCF combat templates): a parry inside its window
    /// stops the blow and opens a melee attacker, a blow in its recovery is a failed parry; an attack an attacker cannot
    /// pay for does not start; a warp pulls the attacker to its target and turns it, within its limits.
    /// </summary>
    public class ParryStaminaWarpTests
    {
        private const int Knight = 1;
        private const int Goblin = 2;

        private static readonly CombatVector Forward = new CombatVector(0f, 0f, 1f);
        private static readonly CombatVector Back = new CombatVector(0f, 0f, -1f);

        private EventBus _bus;
        private TenDamage _damage;
        private CombatRules _rules;
        private List<ParriedEvent> _parried;

        private static AttackDefinition Stab => new AttackDefinition("stab", 0.2f, 0.2f, 0.3f);

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _damage = new TenDamage();
            _rules = new CombatRules(_damage, _bus, new CombatTimeline());
            _rules.Add(Knight, new CombatantSettings { ParryWindow = 0.2f, ParryRecovery = 0.3f, ParryStunSeconds = 1.2f });
            _rules.Add(Goblin);
            // The knight faces the goblin, a metre in front of it
            _rules.SetPose(Knight, CombatVector.Zero, Forward);
            _rules.SetPose(Goblin, new CombatVector(0f, 0f, 1f), Back);
            _parried = new List<ParriedEvent>();
            _bus.Subscribe<ParriedEvent>(_parried.Add);
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        // The goblin's stab lands its contact `after` seconds after the knight's parry starts
        private ContactResult StabMeetsParry(float after)
        {
            _rules.Attack(Goblin, Stab);
            _rules.Tick(0.2f);
            Assert.That(_rules.StateOf(Goblin), Is.EqualTo(CombatState.Active));
            Assert.That(_rules.Parry(Knight), Is.True);
            _rules.Tick(after);
            return _rules.ReportContact(Goblin, Knight, _rules.SwingOf(Goblin));
        }

        [Test]
        public void AParryInItsWindow_StopsTheBlow_AndOpensTheAttacker()
        {
            var result = StabMeetsParry(0.1f);

            Assert.That(result, Is.EqualTo(ContactResult.Parried));
            Assert.That(_damage.Dealt, Is.Empty, "no damage");
            Assert.That(_parried.Single().StunSeconds, Is.EqualTo(1.2f));
            Assert.That(_rules.StateOf(Goblin), Is.EqualTo(CombatState.HitReact), "its attack is broken");
            Assert.That(_rules.IsOpened(Goblin), Is.True);
            _rules.Tick(1.25f);
            Assert.That(_rules.IsOpened(Goblin), Is.False, "the stun runs out");
            Assert.That(_rules.StateOf(Knight), Is.EqualTo(CombatState.Idle), "the parry is over");
        }

        [Test]
        public void ABlowInTheParrysRecovery_IsAFailedParry_DealingItsShare()
        {
            _rules.Add(Knight, new CombatantSettings { ParryWindow = 0.1f, ParryRecovery = 0.5f, ParryFailDamageMultiplier = 1.5f });

            var result = StabMeetsParry(0.15f);

            Assert.That(result, Is.EqualTo(ContactResult.Hit));
            Assert.That(_damage.Dealt.Single(), Is.EqualTo(15f), "a failed parry takes 1.5 times the blow");
            Assert.That(_parried, Is.Empty);
            Assert.That(_rules.StateOf(Knight), Is.EqualTo(CombatState.HitReact), "and staggers");
        }

        [Test]
        public void AnUnparryableBlow_OrOneFromBehind_IsNotParried()
        {
            var unparryable = new AttackDefinition("grab", 0.2f, 0.2f, 0.3f) { Parryable = false };
            _rules.Attack(Goblin, unparryable);
            _rules.Tick(0.2f);
            _rules.Parry(Knight);
            Assert.That(_rules.ReportContact(Goblin, Knight, _rules.SwingOf(Goblin)), Is.EqualTo(ContactResult.Hit));

            _rules.Tick(2f);
            _rules.SetPose(Knight, CombatVector.Zero, Back);
            Assert.That(StabMeetsParry(0.05f), Is.EqualTo(ContactResult.Hit), "the parry faces away");
        }

        [Test]
        public void ARaisedBlock_ParriesInItsFirstMoments_ThenBlocks_AndAParryCanCost()
        {
            var stock = new Stock { Left = 2f };
            _rules.BlockStock = stock;
            _rules.Add(Knight, new CombatantSettings { ParryWindow = 0.15f, ParryOnBlockRaise = true, ParryCost = 0.5f });

            _rules.Attack(Goblin, Stab);
            _rules.Tick(0.2f);
            _rules.SetBlocking(Knight, true);
            _rules.Tick(0.05f);
            Assert.That(_rules.IsParrying(Knight), Is.True, "the block just rose");
            Assert.That(_rules.ReportContact(Goblin, Knight, _rules.SwingOf(Goblin)), Is.EqualTo(ContactResult.Parried));
            Assert.That(_parried.Single().OnBlockRaise, Is.True);
            Assert.That(stock.Left, Is.EqualTo(1.5f), "half the block cost");

            _rules.Tick(2f);
            _rules.Attack(Goblin, Stab);
            _rules.Tick(0.2f);
            Assert.That(_rules.IsParrying(Knight), Is.False, "the block has been up a while");
            Assert.That(_rules.ReportContact(Goblin, Knight, _rules.SwingOf(Goblin)), Is.EqualTo(ContactResult.Blocked));
        }

        [Test]
        public void ACombatantWithoutAParryWindow_IsRefused()
        {
            Assert.That(_rules.Parry(Goblin), Is.False);
            Assert.That(_rules.StateOf(Goblin), Is.EqualTo(CombatState.Idle));
        }

        [Test]
        public void AnAttackItCannotPayFor_DoesNotStart()
        {
            var stock = new Stock { Left = 1.5f };
            _rules.AttackStock = stock;
            var heavy = new AttackDefinition("heavy", 0.3f, 0.2f, 0.4f) { StaminaCost = 1f };

            Assert.That(_rules.Attack(Goblin, heavy), Is.True);
            Assert.That(stock.Left, Is.EqualTo(0.5f));
            _rules.Tick(2f);
            Assert.That(_rules.Attack(Goblin, heavy), Is.False, "too tired");
            Assert.That(_rules.StateOf(Goblin), Is.EqualTo(CombatState.Idle));
            Assert.That(_rules.Timeline.Entries.Last().Note, Does.Contain("cannot pay"));
            Assert.That(_rules.Attack(Goblin, Stab), Is.True, "a free attack still starts");
        }

        [Test]
        public void AWarp_PullsTheAttackerToItsTarget_AndTurnsIt_WithinItsLimits()
        {
            var lunge = new AttackDefinition("lunge", 0.5f, 0.2f, 0.3f)
            {
                Warp = new AttackWarp(AttackPhase.WindUp, maxDistance: 1f, maxAngle: 45f, stopDistance: 0.5f)
            };
            _rules.SetPose(Goblin, CombatVector.Zero, Forward);
            _rules.Attack(Goblin, lunge);

            // The target 1.5 m ahead and a little to the right: 1 m to cover over the 0.5 s, a small turn right
            var target = new CombatVector(0.2f, 0f, 1.5f);
            var warp = _rules.WarpOf(Goblin, target);
            Assert.That(warp.IsWarping, Is.True);
            Assert.That(warp.Velocity.Length, Is.EqualTo(2f).Within(1e-3f), "1 m over the wind-up, at most");
            Assert.That(warp.Velocity.Z, Is.GreaterThan(0f));
            Assert.That(warp.TurnSpeed, Is.GreaterThan(0f), "turns right");

            // Far away: the most it may, 1 m over 0.5 s; well round: the most it may turn, 45 degrees over 0.5 s
            Assert.That(_rules.WarpOf(Goblin, new CombatVector(0f, 0f, 10f)).Velocity.Length, Is.EqualTo(2f).Within(1e-3f));
            Assert.That(_rules.WarpOf(Goblin, new CombatVector(-5f, 0f, 0.1f)).TurnSpeed, Is.EqualTo(-90f).Within(1e-2f));

            // Already at the gap: it only turns; the active window has no warp
            Assert.That(_rules.WarpOf(Goblin, new CombatVector(0f, 0f, 0.4f)).Velocity.Length, Is.EqualTo(0f));
            _rules.Tick(0.5f);
            Assert.That(_rules.WarpOf(Goblin, target).IsWarping, Is.False);
            Assert.That(() => new AttackDefinition("x", 0f, 0.1f, 0f) { Warp = new AttackWarp(AttackPhase.WindUp, -1f, 0f) },
                Throws.ArgumentException);
        }

        private sealed class TenDamage : ICombatDamage
        {
            public readonly List<float> Dealt = new List<float>();

            public bool CanHit(int attackerId, int targetId) => true;

            public float Deal(int attackerId, int targetId, AttackContext context)
            {
                Dealt.Add(10f * context.Multiplier);
                return 10f * context.Multiplier;
            }
        }

        private sealed class Stock : IBlockStock
        {
            public float Left;

            public bool TrySpend(int entityId, float amount)
            {
                if (Left < amount)
                    return false;
                Left -= amount;
                return true;
            }
        }
    }
}
