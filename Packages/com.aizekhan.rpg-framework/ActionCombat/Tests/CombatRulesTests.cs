using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat.Tests
{
    public class CombatRulesTests
    {
        private const int Hero = 1;
        private const int Monster = 2;
        private const int Friend = 3;

        private FakeDamage _damage;
        private EventBus _bus;
        private CombatTimeline _timeline;
        private CombatRules _rules;
        private List<ContactResolvedEvent> _contacts;

        private static AttackDefinition Slash => new AttackDefinition("slash", 0.2f, 0.1f, 0.3f);

        [SetUp]
        public void SetUp()
        {
            _damage = new FakeDamage();
            _bus = new EventBus();
            _timeline = new CombatTimeline();
            _rules = new CombatRules(_damage, _bus, _timeline);
            _contacts = new List<ContactResolvedEvent>();
            _bus.Subscribe<ContactResolvedEvent>(e => _contacts.Add(e));
            _rules.Add(Hero);
            _rules.Add(Monster);
            _rules.Add(Friend);
        }

        [Test]
        public void AnAttack_GoesThroughItsPhases_AndBackToIdle()
        {
            var phases = new List<string>();
            _bus.Subscribe<AttackStartedEvent>(e => phases.Add(At("started", e.Time)));
            _bus.Subscribe<AttackActiveStartedEvent>(e => phases.Add(At("active", e.Time)));
            _bus.Subscribe<AttackActiveEndedEvent>(e => phases.Add(At("inactive", e.Time)));
            _bus.Subscribe<AttackEndedEvent>(e => phases.Add(At("ended", e.Time) + $" {e.Interrupted}"));

            Assert.That(_rules.Attack(Hero, Slash), Is.True);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.WindUp));
            Assert.That(_rules.Attack(Hero, Slash), Is.False, "one attack at a time");
            _rules.Tick(0.15f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.WindUp));
            _rules.Tick(0.1f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Active));
            _rules.Tick(0.1f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Recovery));
            _rules.Tick(0.3f);

            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Idle));
            // Each phase at its exact time, though the ticks did not fall on it
            Assert.That(phases, Is.EqualTo(new[] { "started 0.00", "active 0.20", "inactive 0.30", "ended 0.60 False" }));
        }

        [Test]
        public void OneLongTick_RunsEveryPhaseItCovers()
        {
            _rules.Attack(Hero, Slash);
            _rules.Tick(5f);

            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Idle));
            Assert.That(_timeline.For(Hero).Select(e => e.Kind), Is.EqualTo(new[]
            {
                TimelineKind.AttackStarted, TimelineKind.ActiveStarted, TimelineKind.ActiveEnded, TimelineKind.AttackEnded
            }));
        }

        [Test]
        public void AContact_LandsOnlyInTheActiveWindow_OncePerTargetPerSwing()
        {
            _rules.Attack(Hero, Slash);
            int swing = _rules.SwingOf(Hero);

            Assert.That(_rules.ReportContact(Hero, Monster, swing), Is.EqualTo(ContactResult.Ignored), "too early");
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.OutsideActiveWindow));
            _rules.Tick(0.25f);
            Assert.That(_rules.ReportContact(Hero, Monster, swing), Is.EqualTo(ContactResult.Hit));
            Assert.That(_rules.ReportContact(Hero, Monster, swing), Is.EqualTo(ContactResult.Ignored));
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.AlreadyHit));
            _rules.Tick(0.1f);
            Assert.That(_rules.ReportContact(Hero, Friend, swing), Is.EqualTo(ContactResult.Ignored), "too late");
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.OutsideActiveWindow));

            Assert.That(_damage.Dealt, Has.Count.EqualTo(1));
            Assert.That(_damage.Dealt[0].Target, Is.EqualTo(Monster));
        }

        [Test]
        public void AContact_OfAnotherAttackersSwing_OrOfItself_OrOnAFriend_IsIgnored()
        {
            _rules.Attack(Hero, Slash);
            int swing = _rules.SwingOf(Hero);
            _rules.Tick(0.25f);
            _damage.Friends.Add((Hero, Friend));

            Assert.That(_rules.ReportContact(Monster, Hero, swing), Is.EqualTo(ContactResult.Ignored));
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.UnknownSwing));
            Assert.That(_rules.ReportContact(Hero, Hero, swing), Is.EqualTo(ContactResult.Ignored));
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.Self));
            Assert.That(_rules.ReportContact(Hero, Friend, swing), Is.EqualTo(ContactResult.Ignored));
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.NotHostile));
            Assert.That(_damage.Dealt, Is.Empty);
        }

        [Test]
        public void AHit_DealsDamageWithTheAttacksMultiplierTagsAndSource()
        {
            var heavy = new AttackDefinition("heavy", 0f, 0.1f, 0f) { DamageMultiplier = 1.5f, Tags = 4, SourceId = 7 };
            _rules.Attack(Hero, heavy);

            _rules.ReportContact(Hero, Monster, _rules.SwingOf(Hero));

            var context = _damage.Dealt.Single().Context;
            Assert.That(context.Multiplier, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(context.Tags, Is.EqualTo(4));
            Assert.That(context.SourceId, Is.EqualTo(7));
            Assert.That(_contacts.Last().Damage, Is.EqualTo(10f * 1.5f).Within(1e-4f));
        }

        [Test]
        public void AHit_StaggersTheTarget_AndInterruptsItsAttack()
        {
            _rules.Attack(Monster, Slash);
            _rules.Attack(Hero, new AttackDefinition("jab", 0f, 0.1f, 0.1f));
            var ended = new List<AttackEndedEvent>();
            _bus.Subscribe<AttackEndedEvent>(e => ended.Add(e));

            _rules.ReportContact(Hero, Monster, _rules.SwingOf(Hero));

            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.HitReact));
            Assert.That(ended.Single().EntityId, Is.EqualTo(Monster));
            Assert.That(ended.Single().Interrupted, Is.True);
            _rules.Tick(0.4f);
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.Idle));
        }

        [Test]
        public void AnUninterruptibleAttack_TakesTheHit_ButKeepsGoing()
        {
            _rules.Attack(Monster, new AttackDefinition("slam", 0.5f, 0.1f, 0.5f) { Interruptible = false });
            _rules.Attack(Hero, new AttackDefinition("jab", 0f, 0.1f, 0.1f));

            Assert.That(_rules.ReportContact(Hero, Monster, _rules.SwingOf(Hero)), Is.EqualTo(ContactResult.Hit));

            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.WindUp));
            Assert.That(_damage.Dealt, Has.Count.EqualTo(1));
        }

        [Test]
        public void Poise_ResistsStaggerUntilItRunsOut_AndRefillsAfterAQuietSpell()
        {
            _rules.Add(Monster, new CombatantSettings { Poise = 2.5f, PoiseRecoverySeconds = 2f });
            var jab = new AttackDefinition("jab", 0f, 0.1f, 0f) { Stagger = 1f };

            void Jab()
            {
                _rules.Tick(0.2f);
                _rules.Attack(Hero, jab);
                _rules.ReportContact(Hero, Monster, _rules.SwingOf(Hero));
            }

            Jab();
            Jab();
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.Idle), "1.5 poise left");
            Jab();
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.HitReact), "the third breaks it");

            _rules.Tick(3f);
            Jab();
            Jab();
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.Idle), "refilled after the quiet spell");
        }

        [Test]
        public void ADodge_IsInvulnerableForExactlyItsOwnTime()
        {
            _rules.Add(Hero, new CombatantSettings { DodgeSeconds = 0.4f });
            var longSwing = new AttackDefinition("sweep", 0f, 1f, 0f);

            Assert.That(_rules.Dodge(Hero, new CombatVector(1f, 0f, 0f)), Is.True);
            _rules.Attack(Monster, longSwing);
            _rules.Tick(0.39f);
            Assert.That(_rules.IsInvulnerable(Hero), Is.True);
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Dodged));
            Assert.That(_damage.Dealt, Is.Empty);

            // The same swing does not come back for the dodger; a new one after the dodge lands
            _rules.Tick(0.02f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Idle));
            Assert.That(_rules.IsInvulnerable(Hero), Is.False);
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Ignored));
            _rules.Tick(1f);
            _rules.Attack(Monster, longSwing);
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Hit));
        }

        [Test]
        public void ADodgesInvulnerability_CanBeAWindowInsideIt()
        {
            _rules.Add(Hero, new CombatantSettings { DodgeSeconds = 0.6f, InvulnerableFrom = 0.1f, InvulnerableUntil = 0.3f, HitReactSeconds = 0.2f });
            var started = new List<DodgeStartedEvent>();
            var dodgeEnded = new List<DodgeEndedEvent>();
            _bus.Subscribe<DodgeStartedEvent>(e => started.Add(e));
            _bus.Subscribe<DodgeEndedEvent>(e => dodgeEnded.Add(e));

            _rules.Dodge(Hero, CombatVector.Zero);
            Assert.That(started.Single().InvulnerableFrom, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(started.Single().InvulnerableUntil, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(_rules.IsInvulnerable(Hero), Is.False, "not yet");
            _rules.Tick(0.2f);
            Assert.That(_rules.IsInvulnerable(Hero), Is.True);
            _rules.Tick(0.15f);
            Assert.That(_rules.IsInvulnerable(Hero), Is.False, "the window is over, the dodge is not");
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Dodging));

            _rules.Attack(Monster, new AttackDefinition("jab", 0f, 0.1f, 0f));
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Hit));
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.HitReact), "a hit cuts the dodge short");
            Assert.That(dodgeEnded.Single().Interrupted, Is.True);
        }

        [Test]
        public void ADodge_IsRefusedMidBlow_ButCancelsARecovery()
        {
            _rules.Attack(Hero, Slash);
            Assert.That(_rules.Dodge(Hero, CombatVector.Zero), Is.False, "in the wind-up");
            Assert.That(_timeline.Entries.Last().Kind, Is.EqualTo(TimelineKind.Refused));
            Assert.That(_timeline.Entries.Last().Note, Does.Contain("WindUp"));
            _rules.Tick(0.35f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Recovery));

            Assert.That(_rules.Dodge(Hero, CombatVector.Zero), Is.True);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Dodging));
        }

        [Test]
        public void AHeldBlock_StopsABlowFromTheFront_NotFromBehind()
        {
            _rules.Add(Hero, new CombatantSettings { BlockAngle = 90f });
            _rules.SetPose(Hero, CombatVector.Zero, new CombatVector(0f, 0f, 1f));
            _rules.SetBlocking(Hero, true);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Blocking));

            _rules.SetPose(Monster, new CombatVector(0.3f, 0f, 2f), new CombatVector(0f, 0f, -1f));
            _rules.Attack(Monster, new AttackDefinition("jab", 0f, 0.1f, 0.1f));
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Blocked));
            Assert.That(_damage.Dealt, Is.Empty);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Blocking));

            _rules.Tick(1f);
            _rules.SetPose(Monster, new CombatVector(0f, 0f, -2f), new CombatVector(0f, 0f, 1f));
            _rules.Attack(Monster, new AttackDefinition("jab", 0f, 0.1f, 0.1f));
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Hit), "from behind");

            _rules.Tick(1f);
            _rules.SetBlocking(Hero, false);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Idle));
        }

        [Test]
        public void ABlock_LetsThroughItsShare_AndCostsTheStock_AndBreaksWhenItRunsOut()
        {
            _rules.Add(Hero, new CombatantSettings { BlockDamageMultiplier = 0.25f });
            var stamina = new FakeStock { Left = 3f };
            _rules.BlockStock = stamina;
            _rules.SetBlocking(Hero, true);
            var heavy = new AttackDefinition("heavy", 0f, 0.1f, 0.1f) { BlockCost = 2f };

            _rules.Attack(Monster, heavy);
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Blocked));
            Assert.That(_damage.Dealt.Single().Context.Multiplier, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(stamina.Left, Is.EqualTo(1f));

            _rules.Tick(1f);
            _rules.Attack(Monster, heavy);
            Assert.That(_rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Hit));
            Assert.That(_contacts.Last().GuardBroken, Is.True);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.HitReact), "a broken guard staggers");

            // Still held: the block comes back after the stagger
            _rules.Tick(1f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Blocking));
        }

        [Test]
        public void ABlockHeldDuringAnAttack_RisesWhenTheAttackEnds()
        {
            var raised = new List<BlockRaisedEvent>();
            _bus.Subscribe<BlockRaisedEvent>(e => raised.Add(e));
            _rules.Attack(Hero, Slash);
            _rules.SetBlocking(Hero, true);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.WindUp));
            Assert.That(raised, Is.Empty);

            _rules.Tick(1f);

            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Blocking));
            Assert.That(raised.Single().Time, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void AChargedAttack_ScalesWithTheHold_ClampedToItsRange()
        {
            var heavy = new AttackDefinition("heavy", 0.1f, 0.1f, 0.2f) { MinCharge = 0.5f, MaxCharge = 1.5f, MaxChargeMultiplier = 3f };
            var released = new List<AttackReleasedEvent>();
            _bus.Subscribe<AttackReleasedEvent>(e => released.Add(e));

            Assert.That(_rules.BeginCharge(Hero, heavy), Is.True);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Charging));
            _rules.Tick(1f);
            Assert.That(_rules.ChargeHeld(Hero), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_rules.Release(Hero), Is.True);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.WindUp));
            Assert.That(released.Last().Multiplier, Is.EqualTo(2f).Within(1e-5f), "halfway from min to max");

            _rules.Tick(0.15f);
            _rules.ReportContact(Hero, Monster, _rules.SwingOf(Hero));
            Assert.That(_damage.Dealt.Single().Context.Multiplier, Is.EqualTo(2f).Within(1e-5f));

            _rules.Tick(1f);
            _rules.BeginCharge(Hero, heavy);
            _rules.Tick(9f);
            _rules.Release(Hero);
            Assert.That(released.Last().Multiplier, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(released.Last().ChargeSeconds, Is.EqualTo(1.5f).Within(1e-5f), "clamped to the max");
        }

        [Test]
        public void ACharge_FollowsItsCurve()
        {
            var heavy = new AttackDefinition("heavy", 0f, 0.1f, 0f) { MaxCharge = 1f, MaxChargeMultiplier = 2f, ChargeCurve = s => s * s };

            Assert.That(heavy.ChargeMultiplier(0.5f), Is.EqualTo(1.25f).Within(1e-5f));
            Assert.That(heavy.ChargeMultiplier(1f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(new AttackDefinition("jab", 0f, 0.1f, 0f).ChargeMultiplier(5f), Is.EqualTo(1f), "not chargeable");
        }

        [Test]
        public void AReleaseUnderTheMinimum_IsTheQuickAttack()
        {
            var quick = new AttackDefinition("quick shot", 0f, 0.05f, 0.1f);
            var bow = new AttackDefinition("bow", 0.05f, 0.05f, 0.3f) { MinCharge = 0.3f, MaxCharge = 1f, MaxChargeMultiplier = 2f, QuickAttack = quick };
            var released = new List<AttackReleasedEvent>();
            _bus.Subscribe<AttackReleasedEvent>(e => released.Add(e));

            _rules.BeginCharge(Hero, bow);
            _rules.Tick(0.1f);
            _rules.Release(Hero);

            Assert.That(released.Single().Quick, Is.True);
            Assert.That(released.Single().AttackId, Is.EqualTo("quick shot"));
            Assert.That(released.Single().Multiplier, Is.EqualTo(1f));
            Assert.That(_rules.AttackOf(Hero), Is.SameAs(quick));
        }

        [Test]
        public void AHitWhileCharging_DropsTheCharge()
        {
            var heavy = new AttackDefinition("heavy", 0.1f, 0.1f, 0.2f) { MaxCharge = 1f };
            _rules.BeginCharge(Hero, heavy);
            _rules.Attack(Monster, new AttackDefinition("jab", 0f, 0.1f, 0f));

            _rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster));

            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.HitReact));
            Assert.That(_rules.Release(Hero), Is.False);
        }

        [Test]
        public void AProjectilesWindow_IsItsFlight_UntilItEnds()
        {
            var arrow = new AttackDefinition("arrow", 0.1f, 0.05f, 0.3f) { Projectile = true, ProjectileLifetime = 2f };
            var fired = new List<AttackActiveStartedEvent>();
            _bus.Subscribe<AttackActiveStartedEvent>(e => fired.Add(e));

            _rules.Attack(Hero, arrow);
            int swing = _rules.SwingOf(Hero);
            _rules.Tick(0.1f);
            Assert.That(fired.Single().Projectile, Is.True);
            _rules.Tick(1f);
            Assert.That(_rules.StateOf(Hero), Is.EqualTo(CombatState.Idle), "the archer is free again");
            Assert.That(_rules.IsSwingLive(swing), Is.True, "the arrow still flies");

            Assert.That(_rules.ReportContact(Hero, Monster, swing), Is.EqualTo(ContactResult.Hit));
            Assert.That(_rules.EndSwing(swing), Is.True);
            Assert.That(_rules.ReportContact(Hero, Friend, swing), Is.EqualTo(ContactResult.Ignored));
            Assert.That(_contacts.Last().Ignored, Is.EqualTo(ContactIgnored.UnknownSwing));
        }

        [Test]
        public void AProjectile_RunsOutOfTime()
        {
            var arrow = new AttackDefinition("arrow", 0f, 0.05f, 0f) { Projectile = true, ProjectileLifetime = 1f };
            _rules.Attack(Hero, arrow);
            int swing = _rules.SwingOf(Hero);

            _rules.Tick(1.01f);

            Assert.That(_rules.IsSwingLive(swing), Is.False);
            Assert.That(_timeline.Entries.Last().Kind, Is.EqualTo(TimelineKind.SwingEnded));
            Assert.That(_timeline.Entries.Last().Time, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ATargetNotInTheFight_JustTakesTheHit()
        {
            const int crate = 40;
            _rules.Attack(Hero, new AttackDefinition("jab", 0f, 0.1f, 0f));

            Assert.That(_rules.ReportContact(Hero, crate, _rules.SwingOf(Hero)), Is.EqualTo(ContactResult.Hit));
            Assert.That(_damage.Dealt.Single().Target, Is.EqualTo(crate));
        }

        [Test]
        public void Leaving_EndsTheAttack()
        {
            var ended = new List<AttackEndedEvent>();
            _bus.Subscribe<AttackEndedEvent>(e => ended.Add(e));
            _rules.Attack(Monster, Slash);
            int swing = _rules.SwingOf(Monster);

            Assert.That(_rules.Remove(Monster), Is.True);
            _rules.Tick(1f);

            Assert.That(ended.Single().Interrupted, Is.True);
            Assert.That(_rules.Contains(Monster), Is.False);
            Assert.That(_rules.IsSwingLive(swing), Is.False);
            Assert.That(_rules.Attack(Monster, Slash), Is.False);
        }

        [Test]
        public void TheTimeline_ShowsStrikeFrameContactAndDamage_ForEachSide()
        {
            _rules.Attack(Monster, Slash);
            _rules.Tick(0.25f);
            _rules.ReportContact(Monster, Hero, _rules.SwingOf(Monster), new CombatVector(1f, 1f, 0f));

            var heroSide = _timeline.For(Hero).ToList();
            Assert.That(heroSide.Select(e => e.Kind), Is.EqualTo(new[] { TimelineKind.Contact, TimelineKind.HitReactStarted }));
            var contact = heroSide[0];
            Assert.That(contact.EntityId, Is.EqualTo(Monster));
            Assert.That(contact.OtherId, Is.EqualTo(Hero));
            Assert.That(contact.Result, Is.EqualTo(ContactResult.Hit));
            Assert.That(contact.Value, Is.EqualTo(10f));
            Assert.That(contact.Time, Is.EqualTo(0.25f).Within(1e-5f));
            var active = _timeline.For(Monster).First(e => e.Kind == TimelineKind.ActiveStarted);
            Assert.That(active.Time, Is.EqualTo(0.2f).Within(1e-5f), "the strike frame");
        }

        [Test]
        public void TheTimeline_KeepsTheLatestEntries()
        {
            var timeline = new CombatTimeline(3);
            for (int i = 0; i < 5; i++)
                timeline.Mark(i, Hero, $"input {i}");

            Assert.That(timeline.Count, Is.EqualTo(3));
            Assert.That(timeline.Entries.Select(e => e.Note), Is.EqualTo(new[] { "input 2", "input 3", "input 4" }));
            Assert.That(timeline.Since(3f).Count(), Is.EqualTo(2));
            timeline.Clear();
            Assert.That(timeline.Entries, Is.Empty);
        }

        [Test]
        public void ADefinition_RefusesNegativeTimes()
        {
            Assert.That(() => new AttackDefinition("x", -1f, 0.1f, 0f), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new AttackDefinition("", 0f, 0.1f, 0f), Throws.ArgumentException);
            Assert.That(() => _rules.BeginCharge(Hero, Slash), Throws.ArgumentException, "cannot be charged");
        }

        private static string At(string what, float time) =>
            what + " " + time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        private sealed class FakeDamage : ICombatDamage
        {
            public readonly List<(int Attacker, int Target, AttackContext Context)> Dealt = new List<(int, int, AttackContext)>();
            public readonly HashSet<(int, int)> Friends = new HashSet<(int, int)>();

            public bool CanHit(int attackerId, int targetId) => !Friends.Contains((attackerId, targetId));

            public float Deal(int attackerId, int targetId, AttackContext context)
            {
                Dealt.Add((attackerId, targetId, context));
                return 10f * context.Multiplier;
            }
        }

        private sealed class FakeStock : IBlockStock
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
