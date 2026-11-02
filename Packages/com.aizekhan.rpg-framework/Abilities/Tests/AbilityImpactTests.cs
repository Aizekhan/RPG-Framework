using System;
using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Abilities.Tests
{
    /// <summary>
    /// Delayed hits: an ability goes off (paid, cooldown) and its hits come later, each at its own time, sped up by
    /// the game's attack speed; a flurry makes several; an interrupt stops the hits of a swing but not of an arrow
    /// already loosed; a hit can check its target again; an ability without them still hits at once.
    /// </summary>
    public sealed class AbilityImpactTests
    {
        private const int Caster = 1;
        private const int Target = 2;

        private EventBus _bus;
        private CooldownTracker _cooldowns;
        private readonly List<string> _log = new List<string>();
        private readonly List<string> _hits = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _hits.Clear();
            _bus = new EventBus();
            _cooldowns = new CooldownTracker();
            _bus.Subscribe<AbilityImpactEvent>(e =>
                _log.Add(FormattableString.Invariant($"{e.Hit}/{e.Hits} {(e.Landed ? "landed" : "missed " + e.Reason)} at {e.Timestamp:0.##}")));
        }

        [TearDown]
        public void TearDown() => _bus.Dispose();

        private AbilityExecutor Executor(Func<int, int, float> distance, params IAbility[] abilities)
        {
            var registry = new AbilityRegistry();
            foreach (var ability in abilities)
                registry.RegisterAbility(ability);
            return new AbilityExecutor(registry, _cooldowns, new TargetValidator(), _bus, distance);
        }

        private AbilityExecutor Executor(params IAbility[] abilities) => Executor(null, abilities);

        private static IAbility Swing(float delay = 0.4f, int count = 0, float interval = 0f, bool survive = false,
            bool recheck = false, float range = 0f) =>
            new Ability(new AbilityMetadata("swing", "Swing", cooldown: 1f, range: range),
                new List<ResourceCost> { new ResourceCost("Stamina", 10f) },
                new CastInfo(0f)
                {
                    ImpactDelay = delay, ImpactCount = count, ImpactInterval = interval,
                    ImpactsSurviveInterrupt = survive, RecheckTargetOnImpact = recheck
                },
                TargetType.Enemy);

        private AbilityExecutionResult Use(AbilityExecutor executor, string id, float time, Action<string, float> pay = null) =>
            executor.ExecuteAbility(id, Caster, Target, time, (e, type, amount) => true,
                (e, type, amount) => pay?.Invoke(type, amount),
                (caster, target, ability) => _hits.Add(FormattableString.Invariant($"{ability} hit {((IAbilityImpacts)executor).Current.Hit}")));

        [Test]
        public void ADelayedHit_IsPaidAtOnce_AndLandsAtItsTime()
        {
            var executor = Executor(Swing());
            float paid = 0f;
            var executed = new List<AbilityExecutedEvent>();
            _bus.Subscribe<AbilityExecutedEvent>(executed.Add);

            var result = Use(executor, "swing", 0f, (type, amount) => paid += amount);

            Assert.That(result.Success, Is.True);
            Assert.That(result.ImpactsPending, Is.EqualTo(1));
            Assert.That(result.ImpactAt, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(paid, Is.EqualTo(10f), "paid when it goes off");
            Assert.That(_cooldowns.IsOnCooldown(Caster, "swing", 0.1f), Is.True, "the cooldown runs from the use");
            Assert.That(_hits, Is.Empty, "nothing hit yet");
            Assert.That(executed[0].ImpactAt, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(executed[0].UseId, Is.EqualTo(result.UseId));

            executor.Update(0.3f);
            Assert.That(_hits, Is.Empty);
            Assert.That(((IAbilityImpacts)executor).HasPendingImpacts(Caster), Is.True);
            executor.Update(0.55f);

            Assert.That(_hits, Is.EqualTo(new[] { "swing hit 1" }));
            Assert.That(_log, Is.EqualTo(new[] { "1/1 landed at 0.4" }), "stamped with its own time");
            Assert.That(((IAbilityImpacts)executor).HasPendingImpacts(Caster), Is.False);
        }

        [Test]
        public void AttackSpeed_BringsTheHitsSooner_AFlurryMakesSeveral()
        {
            var executor = Executor(Swing(delay: 0.4f, count: 3, interval: 0.2f));
            ((IAbilityImpacts)executor).ImpactSpeedFor = (caster, ability) => 2f;

            Use(executor, "swing", 0f);
            executor.Update(1f);

            Assert.That(_log, Is.EqualTo(new[] { "1/3 landed at 0.2", "2/3 landed at 0.3", "3/3 landed at 0.4" }));
        }

        [Test]
        public void AFlurryWithoutADelay_HitsOnceAtOnce_ThenTheRest()
        {
            var executor = Executor(Swing(delay: 0f, count: 2, interval: 0.25f));

            var result = Use(executor, "swing", 1f);

            Assert.That(_hits, Is.EqualTo(new[] { "swing hit 1" }));
            Assert.That(result.ImpactsPending, Is.EqualTo(1));
            executor.Update(1.3f);
            Assert.That(_hits, Is.EqualTo(new[] { "swing hit 1", "swing hit 2" }));
        }

        [Test]
        public void AnInterrupt_StopsASwingBeforeContact_ButNotAnArrowAlreadyLoosed()
        {
            var arrow = new Ability(new AbilityMetadata("arrow", "Arrow"),
                castInfo: new CastInfo(0f) { ImpactDelay = 0.5f, ImpactsSurviveInterrupt = true }, targetType: TargetType.Enemy);
            var executor = Executor(Swing(), arrow);
            var missed = new List<string>();
            ((IAbilityImpacts)executor).ImpactMissed += (impact, reason) => missed.Add(impact.AbilityId + " " + reason);

            Use(executor, "swing", 0f);
            Use(executor, "arrow", 0f);
            Assert.That(executor.InterruptCast(Caster, 0.2f, "stunned"), Is.True);
            executor.Update(1f);

            Assert.That(_hits, Is.EqualTo(new[] { "arrow hit 1" }));
            Assert.That(missed, Is.EqualTo(new[] { "swing stunned" }));
            Assert.That(_log, Is.EqualTo(new[] { "1/1 missed stunned at 0.2", "1/1 landed at 0.5" }));
            Assert.That(executor.InterruptCast(Caster, 1f), Is.False, "nothing left to stop");
        }

        [Test]
        public void AHitThatChecksAgain_MissesATargetThatGotAway()
        {
            float distance = 1f;
            var executor = Executor((caster, target) => distance, Swing(recheck: true, range: 2f));

            Use(executor, "swing", 0f);
            distance = 5f; // rolled out of reach during the swing
            executor.Update(0.5f);

            Assert.That(_hits, Is.Empty);
            Assert.That(_log, Is.EqualTo(new[] { "1/1 missed " + AbilityImpactReasons.InvalidTarget + " at 0.4" }));
        }

        [Test]
        public void ACasterThatLeaves_TakesItsHitsWithIt()
        {
            var executor = Executor(Swing());
            Use(executor, "swing", 0f);

            executor.UnregisterEntity(Caster);
            executor.Update(1f);

            Assert.That(_hits, Is.Empty);
            Assert.That(_log, Is.Empty, "forgotten without events");
        }

        [Test]
        public void AnAbilityWithoutDelayedHits_StillHitsAtOnce()
        {
            var executor = Executor(Swing(delay: 0f));

            var result = Use(executor, "swing", 0f);

            Assert.That(_hits, Is.EqualTo(new[] { "swing hit 1" }));
            Assert.That(result.ImpactsPending, Is.Zero);
            Assert.That(result.ImpactAt, Is.Zero);
            Assert.That(_log, Is.Empty, "no impact events for a plain ability");
            Assert.That(((IAbilityImpacts)executor).Current.UseId, Is.Zero, "only valid during a call");
        }
    }
}
