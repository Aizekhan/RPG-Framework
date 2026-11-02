using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RPGFramework.Combat;
using RPGFramework.Events;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace RPGFramework.ActionCombat.Tests
{
    public class EnemyDirectorTests
    {
        private const int Player = 1;
        private const int Monster = 10;

        private FakeWorld _world;
        private EventBus _bus;
        private CombatRules _rules;
        private EnemyDirector _director;

        private static readonly CombatVector Forward = new CombatVector(0f, 0f, 1f);

        [SetUp]
        public void SetUp()
        {
            _world = new FakeWorld();
            _bus = new EventBus();
            _rules = new CombatRules(new AnyDamage(), _bus, new CombatTimeline());
            _director = new EnemyDirector(_rules, _world, _bus, seed: 7);
            _rules.Add(Player);
            _world.Place(Player, new CombatVector(0f, 0f, 8f), Forward);
            _world.Targets.Add(Player);
        }

        [TearDown]
        public void TearDown() => _director.Dispose();

        private static AttackDefinition Jab => new AttackDefinition("jab", 0.3f, 0.2f, 0.3f);

        private EnemyBehaviour AddMonster(int id, CombatVector at, EnemyBehaviour behaviour = null)
        {
            behaviour = behaviour ?? new EnemyBehaviour { NoticeSeconds = 0f };
            if (behaviour.Moves.Count == 0)
                behaviour.Moves.Add(new EnemyMove(Jab, 0f, 2.5f));
            _rules.Add(id);
            _world.Place(id, at, Forward);
            _director.Add(id, behaviour, at);
            return behaviour;
        }

        private void Step(float seconds = 0.1f)
        {
            _rules.Tick(seconds);
            _director.Tick();
        }

        private void Run(float seconds, float step = 0.05f)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += step)
                Step(step);
        }

        [Test]
        public void ItNotices_ATargetInItsViewCone_ThenChases()
        {
            var noticed = new List<EnemyNoticedEvent>();
            _bus.Subscribe<EnemyNoticedEvent>(e => noticed.Add(e));
            AddMonster(Monster, CombatVector.Zero, new EnemyBehaviour { NoticeSeconds = 0.5f });

            Step();
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Notice), "a roar first");
            Assert.That(noticed.Single().TargetId, Is.EqualTo(Player));
            Run(0.6f);
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Chase));
            var intent = _director.IntentOf(Monster);
            Assert.That(intent.Move.Z, Is.GreaterThan(0.9f), "runs at the target");
            Assert.That(intent.Speed, Is.EqualTo(4.5f));
        }

        [Test]
        public void ItDoesNotSee_BehindItOrThroughWalls_ButHearsWhoIsClose()
        {
            AddMonster(Monster, CombatVector.Zero);

            _world.Place(Player, new CombatVector(0f, 0f, -8f), Forward);
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(-1), "behind");

            _world.Place(Player, new CombatVector(0f, 0f, 8f), Forward);
            _world.Walls = true;
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(-1), "a wall in between");

            _world.Place(Player, new CombatVector(0f, 0f, -2f), Forward);
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(Player), "heard close behind");
        }

        [Test]
        public void ItRemembersALostTarget_ForAWhile_ThenGoesHome()
        {
            AddMonster(Monster, CombatVector.Zero);
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(Player));

            _world.Place(Monster, new CombatVector(0f, 0f, 3f), Forward);
            _world.Place(Player, new CombatVector(0f, 0f, 14f), Forward);
            _world.Walls = true;
            Run(3.5f);
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(Player), "still chasing from memory");
            Run(1f);

            Assert.That(_director.TargetOf(Monster), Is.EqualTo(-1));
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Returning));
            Assert.That(_director.IntentOf(Monster).Move.Z, Is.LessThan(-0.9f), "walks home");
            _world.Place(Monster, CombatVector.Zero, Forward);
            Step();
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void Leashed_ItGoesHome_EvenWithTheTargetInSight_AndNoticesAgainOnlyAtHome()
        {
            AddMonster(Monster, CombatVector.Zero, new EnemyBehaviour { NoticeSeconds = 0f, LeashRadius = 10f, ViewAngle = 360f });
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(Player));

            // The chase takes it past the leash, the target running beside it
            _world.Place(Monster, new CombatVector(0f, 0f, 11f), Forward);
            _world.Place(Player, new CombatVector(0f, 0f, 13f), Forward);
            for (int i = 0; i < 10; i++)
            {
                Step();
                Assert.That(_director.TargetOf(Monster), Is.EqualTo(-1), $"tick {i}: no re-engage");
                Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Returning));
            }

            // Struck while still out of bounds: ignored
            _rules.Attack(Player, new AttackDefinition("arrow", 0f, 0.1f, 0f));
            _rules.ReportContact(Player, Monster, _rules.SwingOf(Player));
            Run(2f);
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(-1), "struck out of bounds");
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Returning), "goes on home after the hit");

            // Inside the leash but not home: still ignores what it sees
            _world.Place(Monster, new CombatVector(0f, 0f, 5f), Forward);
            _world.Place(Player, new CombatVector(0f, 0f, 7f), Forward);
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(-1), "on the way home");

            _world.Place(Monster, CombatVector.Zero, Forward);
            Step();
            Step();
            Assert.That(_director.TargetOf(Monster), Is.EqualTo(Player), "home: notices again");
        }

        [Test]
        public void ATick_MakesNoGarbage_ExceptTheRulesNewSwing()
        {
            var world = new StillWorld();
            var rules = new CombatRules(new AnyDamage());
            var director = new EnemyDirector(rules, world, seed: 5);
            rules.Add(Player);
            world.Place(Player, CombatVector.Zero);
            for (int i = 0; i < 6; i++)
            {
                int id = Monster + i;
                rules.Add(id);
                var at = new CombatVector(i % 2 == 0 ? 1.8f : -1.8f, 0f, (i - 2.5f) * 0.6f);
                world.Place(id, at);
                var behaviour = new EnemyBehaviour { NoticeSeconds = 0f, ViewAngle = 360f, RecoveryPause = 0.2f };
                behaviour.Moves.Add(new EnemyMove(new AttackDefinition("jab", 0.1f, 0.1f, 0.1f), 0f, 3f) { Cooldown = 0.5f });
                director.Add(id, behaviour, at);
            }
            for (int i = 0; i < 100; i++)
            {
                rules.Tick(1f / 60f);
                director.Tick();
            }

            var allocates = new AllocatingGCMemoryConstraint();
            Assert.That(allocates.ApplyTo(new TestDelegate(() => new int[16].GetHashCode())).IsSuccess, Is.True,
                "the check sees garbage");

            // Only a new swing in the rules (CombatRules.Attack) may make garbage: none of the director's own
            const int Frames = 600;
            int allocating = 0, otherGarbage = 0, attacksStarted = 0;
            for (int i = 0; i < Frames; i++)
            {
                rules.Tick(1f / 60f);
                int wasAttacking = Enumerable.Range(Monster, 6).Count(id => director.StateOf(id) == EnemyState.Attacking);
                bool garbage = allocates.ApplyTo(new TestDelegate(director.Tick)).IsSuccess;
                int started = System.Math.Max(0,
                    Enumerable.Range(Monster, 6).Count(id => director.StateOf(id) == EnemyState.Attacking) - wasAttacking);
                attacksStarted += started;
                if (garbage)
                    allocating++;
                if (garbage && started == 0)
                    otherGarbage++;
            }
            director.Dispose();

            TestContext.WriteLine($"{allocating} of {Frames} director ticks made garbage, {attacksStarted} attacks started");
            Assert.That(attacksStarted, Is.GreaterThan(10), "the pack fights while measured");
            // A stray runtime allocation can fall into a measured tick (CI saw one in 600); garbage of the director's
            // own would be on hundreds of them
            Assert.That(otherGarbage, Is.LessThanOrEqualTo(2), "garbage on ticks that started no attack");
        }

        [Test]
        public void Grant_FillsTheGivenList()
        {
            var tokens = new AttackTokens(1);
            var granted = new List<(int Enemy, int Target)> { (99, 99) };
            tokens.Want(10, Player, 1f);
            tokens.Want(11, Player, 2f);

            tokens.Grant(granted);

            Assert.That(granted, Is.EqualTo(new[] { (11, Player) }));
            tokens.Grant(granted);
            Assert.That(granted, Is.Empty, "cleared each round");
        }

        [Test]
        public void OnlyTwoOfFourEnemies_AttackOneTargetAtOnce_TheOthersCircle()
        {
            _world.Place(Player, CombatVector.Zero, Forward);
            var places = new[] { new CombatVector(2f, 0f, 0f), new CombatVector(-2f, 0f, 0f), new CombatVector(0f, 0f, 2f), new CombatVector(0f, 0f, -2f) };
            for (int i = 0; i < places.Length; i++)
                AddMonster(Monster + i, places[i], new EnemyBehaviour { NoticeSeconds = 0f, ViewAngle = 360f });
            var granted = new List<AttackTokenGrantedEvent>();
            _bus.Subscribe<AttackTokenGrantedEvent>(e => granted.Add(e));

            bool twoAttackTwoCircle = false;
            for (int frame = 0; frame < 40; frame++)
            {
                Step(0.05f);
                int holders = Enumerable.Range(Monster, 4).Count(id => _director.Tokens.Holds(id));
                int attacking = Enumerable.Range(Monster, 4).Count(id => _director.StateOf(id) == EnemyState.Attacking);
                int circling = Enumerable.Range(Monster, 4).Count(id => _director.StateOf(id) == EnemyState.Circle);
                Assert.That(holders, Is.LessThanOrEqualTo(2), $"frame {frame}");
                Assert.That(attacking, Is.LessThanOrEqualTo(2), $"frame {frame}");
                twoAttackTwoCircle |= attacking == 2 && circling == 2;
            }

            Assert.That(twoAttackTwoCircle, Is.True);
            Assert.That(granted.Select(g => g.EnemyId).Distinct().Count(), Is.EqualTo(4), "the waiting ones get their turn");
        }

        [Test]
        public void AMove_IsChosenByDistance_AGapCloserFromAfar()
        {
            var close = new EnemyMove(new AttackDefinition("slash", 0.3f, 0.2f, 0.3f), 0f, 2f);
            var lunge = new EnemyMove(new AttackDefinition("lunge", 0.3f, 0.2f, 0.3f), 3f, 6f);
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            behaviour.Moves.Add(close);
            behaviour.Moves.Add(lunge);
            var chosen = new List<AttackChosenEvent>();
            _bus.Subscribe<AttackChosenEvent>(e => chosen.Add(e));

            _world.Place(Player, new CombatVector(0f, 0f, 4f), Forward);
            AddMonster(Monster, CombatVector.Zero, behaviour);
            Run(0.3f);
            Assert.That(chosen.First().AttackId, Is.EqualTo("lunge"));

            _director.Remove(Monster);
            _rules.Remove(Monster);
            chosen.Clear();
            _world.Place(Player, new CombatVector(0f, 0f, 1f), Forward);
            AddMonster(Monster, CombatVector.Zero, behaviour);
            Run(0.3f);
            Assert.That(chosen.First().AttackId, Is.EqualTo("slash"));
        }

        [Test]
        public void AComboGoesOn_OnlyWhileItLands()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f, RecoveryPause = 0.5f };
            var first = new EnemyMove(new AttackDefinition("light", 0.1f, 0.2f, 0.1f), 0f, 3f) { Cooldown = 10f };
            first.Next = new EnemyMove(new AttackDefinition("heavy", 0.1f, 0.2f, 0.1f), 0f, 3f);
            behaviour.Moves.Add(first);
            var chosen = new List<AttackChosenEvent>();
            _bus.Subscribe<AttackChosenEvent>(e => chosen.Add(e));
            _rules.Add(Player, new CombatantSettings { HitReactSeconds = 0f });
            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);
            AddMonster(Monster, CombatVector.Zero, behaviour);

            // Lands: the heavy follows
            while (_rules.StateOf(Monster) != CombatState.Active)
                Step(0.05f);
            Assert.That(_rules.ReportContact(Monster, Player, _rules.SwingOf(Monster)), Is.EqualTo(ContactResult.Hit));
            Run(0.6f);
            Assert.That(chosen.Select(c => (c.AttackId, c.Combo)), Is.EqualTo(new[] { ("light", false), ("heavy", true) }));

            // Misses (another monster, no contact): no heavy, a pause instead
            chosen.Clear();
            AddMonster(Monster + 1, new CombatVector(1f, 0f, 0f), behaviour);
            Run(1.5f);
            var second = chosen.Where(c => c.EnemyId == Monster + 1).ToList();
            Assert.That(second.Select(c => c.AttackId), Is.EqualTo(new[] { "light" }));
        }

        [Test]
        public void AfterAnAttack_ItPausesForThePunishWindow()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f, RecoveryPause = 1f };
            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);
            AddMonster(Monster, CombatVector.Zero, behaviour);

            while (_rules.StateOf(Monster) != CombatState.Recovery)
                Step(0.05f);
            Run(0.35f);

            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Recovering));
            Assert.That(_director.Tokens.Holds(Monster), Is.False, "the token went back");
            Assert.That(_director.IntentOf(Monster).Speed, Is.EqualTo(0f));
        }

        [Test]
        public void InReach_WithNoMoveReady_ItGivesUpItsTurn_InsteadOfFlipFlopping()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f, RecoveryPause = 0f };
            behaviour.Moves.Add(new EnemyMove(new AttackDefinition("slam", 0.1f, 0.1f, 0.1f), 0f, 3f) { Cooldown = 5f });
            _world.Place(Player, new CombatVector(0f, 0f, 2f), Forward);
            AddMonster(Monster, CombatVector.Zero, behaviour);
            var changes = new List<EnemyStateChangedEvent>();
            var released = new List<AttackTokenReleasedEvent>();
            _bus.Subscribe<EnemyStateChangedEvent>(e => changes.Add(e));
            _bus.Subscribe<AttackTokenReleasedEvent>(e => released.Add(e));

            Run(1f);
            changes.Clear();
            released.Clear();
            Run(2f);

            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Circle));
            Assert.That(changes.Where(c => c.To == EnemyState.BackOff || c.To == EnemyState.Approach), Is.Empty, "no flip-flop");
            Assert.That(released.Count, Is.LessThanOrEqualTo(1), "asks again only when a move is ready");
            Run(3f);
            Assert.That(_rules.Timeline.Entries.Count(e => e.Kind == TimelineKind.Decision && e.Note != null && e.Note.StartsWith("chose slam")),
                Is.EqualTo(2), "attacks again once the cooldown is over");
        }

        [Test]
        public void BelowEveryMovesMinimumRange_ItStepsBack()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            behaviour.Moves.Add(new EnemyMove(new AttackDefinition("lunge", 0.1f, 0.1f, 0.1f), 2.5f, 5f));
            _world.Place(Player, new CombatVector(0f, 0f, 1.6f), Forward);
            AddMonster(Monster, CombatVector.Zero, behaviour);

            Run(0.3f);

            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.BackOff));
            Assert.That(_director.IntentOf(Monster).Move.Z, Is.LessThan(-0.5f));
        }

        [Test]
        public void ItBlocksASwing_WhenItsChanceSaysSo()
        {
            _world.Place(Player, new CombatVector(0f, 0f, 2f), Forward);
            AddMonster(Monster, CombatVector.Zero, new EnemyBehaviour { NoticeSeconds = 0f, BlockChance = 1f, BlockSeconds = 0.5f, RecoveryPause = 0f });
            _director.Tokens.MaxPerTarget = 1;
            _director.Tokens.Want(99, Player, 100f);
            _director.Tokens.Grant();
            Step();

            _rules.Attack(Player, Jab);
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.Blocking));
            Step();
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Defending));
            Run(0.6f);
            Assert.That(_rules.StateOf(Monster), Is.Not.EqualTo(CombatState.Blocking), "lowered after its time");
        }

        [Test]
        public void BeingStruck_AlertsAtOnce_WithoutTheRoar()
        {
            _world.Place(Player, new CombatVector(0f, 0f, -10f), Forward);
            AddMonster(Monster, CombatVector.Zero, new EnemyBehaviour { NoticeSeconds = 2f });
            _rules.Attack(Player, new AttackDefinition("arrow", 0f, 0.1f, 0f));

            _rules.ReportContact(Player, Monster, _rules.SwingOf(Player));
            Run(0.5f);

            Assert.That(_director.TargetOf(Monster), Is.EqualTo(Player));
            Assert.That(_director.StateOf(Monster), Is.Not.EqualTo(EnemyState.Notice));
        }

        [Test]
        public void AHit_StopsItsAttack_AndItReacts()
        {
            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);
            AddMonster(Monster, CombatVector.Zero);
            while (_rules.StateOf(Monster) != CombatState.WindUp)
                Step(0.05f);

            _rules.Attack(Player, new AttackDefinition("jab", 0f, 0.1f, 0f));
            _rules.ReportContact(Player, Monster, _rules.SwingOf(Player));
            Step(0.05f);

            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Reacting));
            Assert.That(_director.Tokens.Holds(Monster), Is.False);
            Assert.That(_director.IntentOf(Monster).TurnSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void ItDoesNotTurn_InAnAttacksHitWindow()
        {
            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);
            AddMonster(Monster, CombatVector.Zero);
            while (_rules.StateOf(Monster) != CombatState.WindUp)
                Step(0.05f);
            Step(0.01f);
            Assert.That(_director.IntentOf(Monster).TurnSpeed, Is.GreaterThan(0f), "tracks in the wind-up");
            while (_rules.StateOf(Monster) != CombatState.Active)
                Step(0.05f);
            Step(0.01f);
            Assert.That(_director.IntentOf(Monster).TurnSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void ItDies_ReleasingItsToken_WithADeathVariant()
        {
            var died = new List<EnemyDiedEvent>();
            _bus.Subscribe<EnemyDiedEvent>(e => died.Add(e));
            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);
            AddMonster(Monster, CombatVector.Zero, new EnemyBehaviour { NoticeSeconds = 0f, DeathVariants = 3 });
            Run(0.2f);
            Assert.That(_director.Tokens.Holds(Monster), Is.True);

            _world.Dead.Add(Monster);
            Step();

            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Dead));
            Assert.That(_director.Tokens.Holds(Monster), Is.False);
            Assert.That(died.Single().Variant, Is.InRange(0, 2));
            Step();
            Assert.That(died, Has.Count.EqualTo(1), "once");
        }

        [Test]
        public void ItsDecisions_AreInTheTimeline()
        {
            AddMonster(Monster, CombatVector.Zero);
            Step();

            var notes = _rules.Timeline.For(Monster).Where(e => e.Kind == TimelineKind.Decision).Select(e => e.Note).ToList();
            Assert.That(notes, Has.Some.Contains("noticed"));
            Assert.That(notes, Has.Some.Contains("Chase"));
        }

        [Test]
        public void TheSameSeed_MakesTheSameFight()
        {
            List<string> Fight(int seed)
            {
                var bus = new EventBus();
                var rules = new CombatRules(new AnyDamage(), bus);
                var director = new EnemyDirector(rules, _world, bus, seed);
                var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
                behaviour.Moves.Add(new EnemyMove(new AttackDefinition("a", 0.1f, 0.1f, 0.1f), 0f, 3f));
                behaviour.Moves.Add(new EnemyMove(new AttackDefinition("b", 0.1f, 0.1f, 0.1f), 0f, 3f));
                rules.Add(Monster);
                director.Add(Monster, behaviour, CombatVector.Zero);
                var chosen = new List<string>();
                bus.Subscribe<AttackChosenEvent>(e => chosen.Add(e.AttackId));
                for (int i = 0; i < 100; i++)
                {
                    rules.Tick(0.05f);
                    director.Tick();
                }
                director.Dispose();
                return chosen;
            }

            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);
            _world.Place(Monster, CombatVector.Zero, Forward);
            Assert.That(Fight(3), Is.EqualTo(Fight(3)));
            Assert.That(Fight(3), Has.Count.GreaterThan(2));
        }

        // FG-31: a goblin with a dagger closes to three quarters of its reach before a quick blow
        [Test]
        public void AQuickBlow_StartsOnlyInsideItsCommitRange()
        {
            var chosen = new List<AttackChosenEvent>();
            _bus.Subscribe<AttackChosenEvent>(chosen.Add);
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            behaviour.Moves.Add(new EnemyMove(Jab, 0f, 2.5f) { CommitShare = 0.75f });
            AddMonster(Monster, CombatVector.Zero, behaviour);
            _world.Place(Player, new CombatVector(0f, 0f, 2.2f), Forward);

            Run(0.3f);
            Assert.That(chosen, Is.Empty, "2.2 m is in its reach but not its commit range (1.875 m)");
            Assert.That(_director.StateOf(Monster), Is.EqualTo(EnemyState.Approach));
            Assert.That(_director.IntentOf(Monster).Move.Z, Is.GreaterThan(0.9f), "closes in");

            _world.Place(Monster, new CombatVector(0f, 0f, 0.5f), Forward);
            Step();
            Assert.That(chosen.Single().Distance, Is.EqualTo(1.7f).Within(1e-3f), "strikes from inside");
        }

        [Test]
        public void AWideSwing_StartsAtItsReach_AndHoldsTheWindUpWhileClosingIn()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            var swing = new AttackDefinition("swing", 0.3f, 0.2f, 0.3f);
            behaviour.Moves.Add(new EnemyMove(swing, 0f, 3f) { CommitShare = 0.5f, HoldUntilInRange = true, MaxHold = 2f });
            AddMonster(Monster, CombatVector.Zero, behaviour);
            _world.Place(Player, new CombatVector(0f, 0f, 2.8f), Forward);

            Run(1f);
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.WindUp), "raised, waiting");
            Assert.That(_rules.IsWindUpHeld(Monster), Is.True);
            var intent = _director.IntentOf(Monster);
            Assert.That(intent.State, Is.EqualTo(EnemyState.Attacking));
            Assert.That(intent.Move.Z, Is.GreaterThan(0.9f), "closing in with the swing raised");
            Assert.That(intent.Speed, Is.EqualTo(behaviour.ApproachSpeed));

            _world.Place(Monster, new CombatVector(0f, 0f, 1.5f), Forward);
            Step();
            Assert.That(_rules.IsWindUpHeld(Monster), Is.False, "1.3 m: in range, let go");
            Run(0.1f);
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.Active), "the blow");
        }

        [Test]
        public void AHeldSwing_GoesOnWhenItsHoldRunsOut()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            var swing = new AttackDefinition("swing", 0.3f, 0.2f, 0.3f);
            behaviour.Moves.Add(new EnemyMove(swing, 0f, 3f) { CommitShare = 0.5f, HoldUntilInRange = true, MaxHold = 0.5f });
            AddMonster(Monster, CombatVector.Zero, behaviour);
            _world.Place(Player, new CombatVector(0f, 0f, 2.8f), Forward);

            Run(1f);
            Assert.That(_rules.Timeline.For(Monster).Select(e => e.Kind), Has.Member(TimelineKind.WindUpReleased));
            Assert.That(_rules.Timeline.For(Monster).Select(e => e.Note).Where(n => n != null), Has.Some.Contains("held too long"));
        }

        [Test]
        public void AnAttacksMotion_MovesItsBody_AndStopsAtTheTarget()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            var lunge = new AttackDefinition("lunge", 0.4f, 0.2f, 0.3f)
            {
                Motions = new[] { new AttackMotion(AttackPhase.WindUp, AttackMotionKind.Approach, 1f) }
            };
            behaviour.Moves.Add(new EnemyMove(lunge, 0f, 2.5f));
            AddMonster(Monster, CombatVector.Zero, behaviour);
            _world.Place(Player, new CombatVector(0f, 0f, 2f), Forward);

            Run(0.15f);
            var intent = _director.IntentOf(Monster);
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.WindUp));
            Assert.That(intent.Move.Z, Is.GreaterThan(0.9f), "steps into the blow");
            Assert.That(intent.Speed, Is.EqualTo(2.5f).Within(1e-3f), "1 m over the 0.4 s wind-up");

            _world.Place(Monster, new CombatVector(0f, 0f, 1.5f), Forward);
            Step(0.05f);
            Assert.That(_director.IntentOf(Monster).Speed, Is.EqualTo(0f), "at the target it stops");
        }

        // WhiteWrite: motion warping moves and turns the enemy onto its target in the warp's part of the attack
        [Test]
        public void AnAttacksWarp_PullsTheBodyOntoTheTarget_AndTurnsIt()
        {
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            var lunge = new AttackDefinition("lunge", 0.4f, 0.2f, 0.3f)
            {
                Warp = new AttackWarp(AttackPhase.WindUp, maxDistance: 1f, maxAngle: 60f)
            };
            behaviour.Moves.Add(new EnemyMove(lunge, 0f, 2.5f) { TrackDuringWindUp = false });
            AddMonster(Monster, CombatVector.Zero, behaviour);
            _world.Place(Player, new CombatVector(0.5f, 0f, 1.8f), Forward);

            Run(0.15f);
            var intent = _director.IntentOf(Monster);
            Assert.That(_rules.StateOf(Monster), Is.EqualTo(CombatState.WindUp));
            Assert.That(intent.Speed, Is.GreaterThan(0f), "pulled on to the target");
            Assert.That(intent.Move.X, Is.GreaterThan(0f), "towards it, a little to the right");
            Assert.That(intent.TurnSpeed, Is.GreaterThan(0f), "turned to face it, though the move does not track");
        }

        [Test]
        public void WithSweepAttacks_ABlowLandsByItsSegment_WithoutHitboxes()
        {
            var hits = new List<ContactResolvedEvent>();
            _bus.Subscribe<ContactResolvedEvent>(e =>
            {
                if (e.Result == ContactResult.Hit)
                    hits.Add(e);
            });
            _director.SweepAttacks = true;
            var behaviour = new EnemyBehaviour { NoticeSeconds = 0f };
            var thrust = new AttackDefinition("thrust", 0.2f, 0.2f, 0.3f)
            {
                Sweep = new[] { new SweepSegment(0f, 1f, -20f, 20f, 2f) }
            };
            behaviour.Moves.Add(new EnemyMove(thrust, 0f, 2f));
            AddMonster(Monster, CombatVector.Zero, behaviour);
            _rules.SetPose(Monster, CombatVector.Zero, Forward);
            _world.Place(Player, new CombatVector(0f, 0f, 1.5f), Forward);

            Run(0.6f);
            Assert.That(hits.Single().TargetId, Is.EqualTo(Player), "landed by the sweep, once");

            // The same blow at a target beside it (the body still facing ahead) misses
            _world.Place(Player, new CombatVector(1.4f, 0f, 0.4f), Forward);
            Run(1.5f);
            hits.Clear();
            Run(1.5f);
            Assert.That(_rules.Timeline.For(Monster).Count(e => e.Kind == TimelineKind.AttackStarted), Is.GreaterThan(1));
            Assert.That(hits, Is.Empty, "outside the segment's arc");
        }

        private sealed class FakeWorld : IEnemyWorld
        {
            private readonly Dictionary<int, (CombatVector Position, CombatVector Forward)> _poses =
                new Dictionary<int, (CombatVector, CombatVector)>();

            public readonly HashSet<int> Targets = new HashSet<int>();
            public readonly HashSet<int> Dead = new HashSet<int>();
            public bool Walls;

            public void Place(int id, CombatVector position, CombatVector forward) => _poses[id] = (position, forward);

            public IEnumerable<int> TargetsOf(int enemyId) => Targets.Where(t => !Dead.Contains(t));

            public bool TryGetPose(int entityId, out CombatVector position, out CombatVector forward)
            {
                bool known = _poses.TryGetValue(entityId, out var pose);
                position = pose.Position;
                forward = pose.Forward;
                return known;
            }

            public bool CanSee(int fromId, int toId) => !Walls;

            public float HealthShare(int entityId) => 1f;

            public bool IsAlive(int entityId) => !Dead.Contains(entityId);
        }

        // A world with no garbage of its own, so the director's is measured
        private sealed class StillWorld : IEnemyWorld
        {
            private readonly Dictionary<int, CombatVector> _positions = new Dictionary<int, CombatVector>();
            private readonly List<int> _targets = new List<int> { Player };

            public void Place(int id, CombatVector position) => _positions[id] = position;

            public IEnumerable<int> TargetsOf(int enemyId) => _targets;

            public bool TryGetPose(int entityId, out CombatVector position, out CombatVector forward)
            {
                forward = Forward;
                return _positions.TryGetValue(entityId, out position);
            }

            public bool CanSee(int fromId, int toId) => true;

            public float HealthShare(int entityId) => 1f;

            public bool IsAlive(int entityId) => true;
        }

        private sealed class AnyDamage : ICombatDamage
        {
            public bool CanHit(int attackerId, int targetId) => true;

            public float Deal(int attackerId, int targetId, AttackContext context) => 1f;
        }
    }
}
