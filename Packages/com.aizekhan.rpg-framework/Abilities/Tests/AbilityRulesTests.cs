using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using RPGFramework.Events;

namespace RPGFramework.Abilities.Tests
{
    /// <summary>
    /// Using an ability honours what the designer authored: the target is checked, costs are checked and paid
    /// (percentage costs as a share of the resource maximum), the cooldown lives in the cooldown tracker, a cast pays
    /// and lands only when it completes, passives cannot be pressed, toggles switch on and off, channels keep the
    /// caster busy for their duration.
    /// </summary>
    public sealed class AbilityRulesTests
    {
        private const int Caster = 1;
        private const int Foe = 2;

        private EventBus _bus;
        private AbilityRegistry _registry;
        private CooldownTracker _tracker;
        private readonly List<string> _log = new List<string>();
        private readonly List<string> _paid = new List<string>();
        private readonly List<string> _landed = new List<string>();
        private bool _canPay;

        private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _paid.Clear();
            _landed.Clear();
            _canPay = true;
            _bus = new EventBus();
            _bus.Subscribe<AbilityExecutedEvent>(e => _log.Add($"executed {e.AbilityId} {e.Success} at {F(e.Timestamp)}"));
            _bus.Subscribe<AbilityCanceledEvent>(e => _log.Add($"canceled {e.AbilityId} at {F(e.Timestamp)} ({e.Reason})"));
            _bus.Subscribe<CooldownStartedEvent>(e => _log.Add($"cooldown {e.AbilityId} {F(e.Duration)} at {F(e.Timestamp)}"));
            _bus.Subscribe<AbilityToggledEvent>(e => _log.Add($"toggled {e.AbilityId} {(e.IsOn ? "on" : "off")} at {F(e.Timestamp)}"));
            _registry = new AbilityRegistry();
            _tracker = new CooldownTracker();
        }

        private void Register(string id, AbilityType type = AbilityType.Active, float cooldown = 0f,
            float castTime = 0f, float channel = 0f, bool interruptible = true, TargetType target = TargetType.None,
            float range = 0f, ResourceCost? cost = null, ResourceCost[] costs = null)
        {
            var list = new List<ResourceCost>();
            if (cost.HasValue)
                list.Add(cost.Value);
            if (costs != null)
                list.AddRange(costs);
            _registry.RegisterAbility(new Ability(
                new AbilityMetadata(id, id, type, cooldown, range),
                list,
                new CastInfo(castTime, channel, interruptible),
                target));
        }

        private AbilityExecutor Executor(
            System.Func<int, int, float> distance = null,
            System.Func<int, int, bool> isEnemy = null,
            System.Func<int, string, float> maximum = null)
        {
            return new AbilityExecutor(_registry, _tracker, new TargetValidator(), _bus, distance, isEnemy, maximum);
        }

        private AbilityExecutionResult Use(AbilityExecutor executor, string id, float time, int target = Foe)
        {
            return executor.ExecuteAbility(id, Caster, target, time,
                (entity, resource, amount) => _canPay,
                (entity, resource, amount) => _paid.Add($"{resource} {F(amount)}"),
                (caster, targetId, abilityId) => _landed.Add(abilityId));
        }

        // ---- Cooldowns ----

        [Test]
        public void TheCooldown_IsKeptInTheCooldownTracker()
        {
            Register("bolt", cooldown: 2f);
            var executor = Executor();

            Assert.That(Use(executor, "bolt", 0f).Success, Is.True);

            Assert.That(_tracker.IsOnCooldown(Caster, "bolt", 1f), Is.True);
            Assert.That(_tracker.GetRemainingCooldown(Caster, "bolt", 1f), Is.EqualTo(1f));
            Assert.That(Use(executor, "bolt", 1f).Success, Is.False);
            Assert.That(Use(executor, "bolt", 2f).Success, Is.True);
        }

        [Test]
        public void ClearingTheCooldownInTheTracker_LetsTheAbilityBeUsedAgain()
        {
            Register("bolt", cooldown: 10f);
            var executor = Executor();
            Use(executor, "bolt", 0f);

            _tracker.ClearCooldown(Caster, "bolt");

            Assert.That(executor.CanExecuteAbility("bolt", Caster, 1f), Is.True);
            Assert.That(Use(executor, "bolt", 1f).Success, Is.True);
        }

        [Test]
        public void ATrackerSharingThePublisher_RaisesCooldownStartedOnce()
        {
            Register("bolt", cooldown: 2f);
            _tracker = new CooldownTracker(_bus);

            Use(Executor(), "bolt", 0f);

            CollectionAssert.AreEqual(new[] { "executed bolt True at 0", "cooldown bolt 2 at 0" }, _log);
        }

        // ---- Cast time ----

        [Test]
        public void ACast_PaysLandsAndStartsItsCooldown_OnlyWhenItCompletes()
        {
            Register("heal", cooldown: 4f, castTime: 1.5f, cost: new ResourceCost("MP", 20f));
            var executor = Executor();

            var start = Use(executor, "heal", 10f, Caster);
            Assert.That(start.Success, Is.True, start.FailureReason);
            Assert.That(start.CastStarted, Is.True);
            Assert.That(_paid, Is.Empty, "nothing is paid while casting");
            Assert.That(_landed, Is.Empty);
            Assert.That(_log, Is.Empty, "no executed event while casting");

            var early = executor.CompleteCast(Caster, 11f);
            Assert.That(early.Success, Is.False, "the cast time has not passed");
            Assert.That(executor.IsCasting(Caster), Is.True, "an early completion leaves the cast running");

            var done = executor.CompleteCast(Caster, 11.5f);
            Assert.That(done.Success, Is.True, done.FailureReason);
            Assert.That(done.Timestamp, Is.EqualTo(11.5f));
            Assert.That(_paid, Is.EqualTo(new[] { "MP 20" }));
            Assert.That(_landed, Is.EqualTo(new[] { "heal" }));
            Assert.That(executor.IsCasting(Caster), Is.False);
            Assert.That(_tracker.GetRemainingCooldown(Caster, "heal", 11.5f), Is.EqualTo(4f), "cooldown starts on completion");
            CollectionAssert.AreEqual(new[] { "executed heal True at 11.5", "cooldown heal 4 at 11.5" }, _log);
        }

        [Test]
        public void Update_CompletesCastsWhoseTimeHasCome()
        {
            Register("heal", castTime: 2f, cost: new ResourceCost("MP", 5f));
            var executor = Executor();
            Use(executor, "heal", 0f, Caster);

            Assert.That(executor.Update(1.9f), Is.EqualTo(0));
            Assert.That(_landed, Is.Empty);
            Assert.That(executor.Update(2f), Is.EqualTo(1));
            Assert.That(_landed, Is.EqualTo(new[] { "heal" }));
            Assert.That(_paid, Is.EqualTo(new[] { "MP 5" }));
        }

        [Test]
        public void ACastTheCasterCanNoLongerPayFor_FailsAtCompletion_WithoutEffectOrCooldown()
        {
            Register("heal", cooldown: 4f, castTime: 1f, cost: new ResourceCost("MP", 20f));
            var executor = Executor();
            Use(executor, "heal", 0f, Caster);

            _canPay = false;
            var done = executor.CompleteCast(Caster, 1f);

            Assert.That(done.Success, Is.False);
            Assert.That(done.FailureReason, Does.Contain("MP"));
            Assert.That(_paid, Is.Empty);
            Assert.That(_landed, Is.Empty);
            Assert.That(executor.IsCasting(Caster), Is.False, "the failed cast ends");
            Assert.That(_tracker.IsOnCooldown(Caster, "heal", 1f), Is.False);
        }

        [Test]
        public void ACasterWhoIsCasting_CannotStartAnotherAbility()
        {
            Register("heal", castTime: 2f);
            Register("bolt");
            var executor = Executor();
            Use(executor, "heal", 0f, Caster);

            Assert.That(Use(executor, "bolt", 1f).Success, Is.False);
            Assert.That(executor.CanExecuteAbility("bolt", Caster, 1f), Is.False);
        }

        // ---- Cancel and interrupt ----

        [Test]
        public void CancelingACast_PaysNothing_AndStampsTheCancelTime()
        {
            Register("heal", cooldown: 4f, castTime: 2f, cost: new ResourceCost("MP", 20f));
            var executor = Executor();
            Use(executor, "heal", 1f, Caster);

            Assert.That(executor.CancelCast(Caster, 2f), Is.True);

            Assert.That(_paid, Is.Empty);
            Assert.That(_landed, Is.Empty);
            Assert.That(_tracker.IsOnCooldown(Caster, "heal", 2f), Is.False);
            CollectionAssert.AreEqual(new[] { "canceled heal at 2 (canceled)" }, _log);
            Assert.That(Use(executor, "heal", 2f, Caster).Success, Is.True, "the caster is free again");
        }

        [Test]
        public void CancelWithoutATime_StampsTheLatestTimeTheExecutorWasGiven()
        {
            Register("heal", castTime: 2f);
            var executor = Executor();
            Use(executor, "heal", 3f, Caster);

            executor.CancelCast(Caster);

            CollectionAssert.AreEqual(new[] { "canceled heal at 3 (canceled)" }, _log);
        }

        [Test]
        public void Interrupt_StopsOnlyInterruptibleCasts()
        {
            Register("heal", castTime: 2f);
            Register("ward", castTime: 2f, interruptible: false);
            var executor = Executor();

            Use(executor, "ward", 0f, Caster);
            Assert.That(executor.InterruptCast(Caster, 1f), Is.False);
            Assert.That(executor.IsCasting(Caster), Is.True);
            executor.CancelCast(Caster, 1f); // the caster may still stop it himself

            Use(executor, "heal", 1f, Caster);
            Assert.That(executor.InterruptCast(Caster, 1.5f, "stunned"), Is.True);
            Assert.That(executor.IsCasting(Caster), Is.False);
            Assert.That(_log[_log.Count - 1], Is.EqualTo("canceled heal at 1.5 (stunned)"));
        }

        // ---- Ability types ----

        [Test]
        public void APassiveAbility_CannotBeUsedByHand()
        {
            Register("toughness", AbilityType.Passive, cost: new ResourceCost("MP", 5f));
            var executor = Executor();

            var result = Use(executor, "toughness", 0f);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Does.Contain("Passive"));
            Assert.That(_paid, Is.Empty);
            Assert.That(_landed, Is.Empty);
            Assert.That(executor.CanExecuteAbility("toughness", Caster, 0f), Is.False);
        }

        [Test]
        public void AToggle_SwitchesOnThenOff()
        {
            Register("aura", AbilityType.Toggle, cooldown: 5f, cost: new ResourceCost("MP", 10f));
            var executor = Executor();

            var on = Use(executor, "aura", 0f);
            Assert.That(on.Success, Is.True, on.FailureReason);
            Assert.That(on.ToggledOff, Is.False);
            Assert.That(executor.IsToggledOn(Caster, "aura"), Is.True);
            Assert.That(_paid, Is.EqualTo(new[] { "MP 10" }));
            Assert.That(_landed, Is.EqualTo(new[] { "aura" }));

            Assert.That(executor.CanExecuteAbility("aura", Caster, 1f), Is.True, "switching off ignores the cooldown");
            var off = Use(executor, "aura", 1f);
            Assert.That(off.Success, Is.True, off.FailureReason);
            Assert.That(off.ToggledOff, Is.True);
            Assert.That(executor.IsToggledOn(Caster, "aura"), Is.False);
            Assert.That(_paid.Count, Is.EqualTo(1), "switching off costs nothing");
            Assert.That(_landed.Count, Is.EqualTo(1), "switching off applies nothing");

            Assert.That(Use(executor, "aura", 2f).Success, Is.False, "switching on again waits out the cooldown");
            CollectionAssert.AreEqual(new[]
            {
                "executed aura True at 0",
                "cooldown aura 5 at 0",
                "toggled aura on at 0",
                "toggled aura off at 1",
                "executed aura False at 2"
            }, _log);
        }

        // ---- Costs ----

        [Test]
        public void APercentageCost_IsAShareOfTheResourceMaximum()
        {
            Register("blood_pact", costs: new[] { new ResourceCost("HP", 0.1f, isPercentage: true), new ResourceCost("MP", 30f) });
            var checkedAmounts = new List<float>();
            var executor = Executor(maximum: (entity, resource) => resource == "HP" ? 200f : 100f);

            var result = executor.ExecuteAbility("blood_pact", Caster, -1, 0f,
                (entity, resource, amount) => { checkedAmounts.Add(amount); return true; },
                (entity, resource, amount) => _paid.Add($"{resource} {F(amount)}"),
                null);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(checkedAmounts, Is.EqualTo(new[] { 20f, 30f }));
            Assert.That(_paid, Is.EqualTo(new[] { "HP 20", "MP 30" }));
        }

        [Test]
        public void APercentageCost_WithNoWayToReadTheMaximum_IsRefused()
        {
            Register("blood_pact", cost: new ResourceCost("HP", 0.1f, isPercentage: true));

            var result = Use(Executor(), "blood_pact", 0f);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Does.Contain("HP"));
            Assert.That(_paid, Is.Empty);
        }

        // ---- Targets ----

        [Test]
        public void TheTarget_IsCheckedBeforeAnythingIsPaid()
        {
            Register("bolt", target: TargetType.Enemy, range: 10f, cost: new ResourceCost("MP", 5f));
            float distance = 5f;
            bool enemy = true;
            var executor = Executor((a, b) => distance, (a, b) => enemy);

            Assert.That(Use(executor, "bolt", 0f, Caster).Success, Is.False, "cannot bolt yourself");
            Assert.That(Use(executor, "bolt", 0f, -1).Success, Is.False, "needs a target");
            enemy = false;
            Assert.That(Use(executor, "bolt", 0f).Success, Is.False, "an ally is not an enemy");
            enemy = true;
            distance = 15f;
            Assert.That(Use(executor, "bolt", 0f).Success, Is.False, "out of range");
            Assert.That(_paid, Is.Empty);
            Assert.That(_landed, Is.Empty);

            distance = 10f;
            Assert.That(Use(executor, "bolt", 0f).Success, Is.True);
        }

        [Test]
        public void TheTarget_IsCheckedAgainWhenACastCompletes()
        {
            Register("bolt", castTime: 1f, target: TargetType.Enemy, range: 10f, cost: new ResourceCost("MP", 5f));
            float distance = 5f;
            var executor = Executor((a, b) => distance, (a, b) => true);
            Assert.That(Use(executor, "bolt", 0f).Success, Is.True);

            distance = 30f; // the target ran away during the cast
            var done = executor.CompleteCast(Caster, 1f);

            Assert.That(done.Success, Is.False);
            Assert.That(_paid, Is.Empty);
            Assert.That(_landed, Is.Empty);
        }

        // ---- Channels ----

        [Test]
        public void AChannel_LandsAtItsStart_AndKeepsTheCasterBusyForItsDuration()
        {
            Register("beam", AbilityType.Channeled, cooldown: 1f, channel: 3f, cost: new ResourceCost("MP", 15f));
            Register("bolt");
            var executor = Executor();

            Assert.That(Use(executor, "beam", 0f).Success, Is.True);
            Assert.That(_landed, Is.EqualTo(new[] { "beam" }));
            Assert.That(_paid, Is.EqualTo(new[] { "MP 15" }));
            Assert.That(executor.IsChanneling(Caster), Is.True);
            Assert.That(Use(executor, "bolt", 2f).Success, Is.False, "busy channeling");

            Assert.That(executor.Update(2.9f), Is.EqualTo(0));
            Assert.That(executor.Update(3f), Is.EqualTo(1));
            Assert.That(executor.IsChanneling(Caster), Is.False);
            Assert.That(Use(executor, "bolt", 3f).Success, Is.True);
        }

        [Test]
        public void CancelingAChannel_EndsItAndRaisesCanceled()
        {
            Register("beam", AbilityType.Channeled, castTime: 1f, channel: 3f);
            var executor = Executor();
            Use(executor, "beam", 0f);
            executor.Update(1f); // cast done, channel begins

            Assert.That(executor.IsChanneling(Caster), Is.True);
            Assert.That(executor.CancelCast(Caster, 2f), Is.True);
            Assert.That(executor.IsChanneling(Caster), Is.False);
            Assert.That(_log[_log.Count - 1], Is.EqualTo("canceled beam at 2 (canceled)"));
        }

        // ---- Targets that are a whole side ----

        [Test]
        public void AllEnemiesAndAllAllies_NeedNoPickedTargetAndIgnoreRange()
        {
            Register("quake", target: TargetType.AllEnemies, range: 5f);
            Register("rally", target: TargetType.AllAllies, range: 5f);
            var executor = Executor(distance: (a, b) => 100f, isEnemy: (a, b) => b == Foe);

            Assert.That(Use(executor, "quake", 0f, target: -1).Success, Is.True);
            Assert.That(Use(executor, "rally", 0f, target: -1).Success, Is.True);
            Assert.That(_landed, Is.EqualTo(new[] { "quake", "rally" }));
        }

        // ---- Forgetting an entity ----

        [Test]
        public void UnregisterEntity_DropsCastsTogglesCooldownsQuietly()
        {
            Register("aura", AbilityType.Toggle);
            Register("bolt", cooldown: 10f);
            Register("nova", castTime: 2f);
            var executor = Executor();
            Use(executor, "aura", 0f);
            Use(executor, "bolt", 0f);
            const int other = 9;
            executor.ExecuteAbility("bolt", other, Foe, 0f, null, null, null);
            Assert.That(Use(executor, "nova", 0f).CastStarted, Is.True);
            int logged = _log.Count;

            executor.UnregisterEntity(Caster);

            Assert.That(executor.IsCasting(Caster), Is.False);
            Assert.That(executor.IsToggledOn(Caster, "aura"), Is.False);
            Assert.That(_tracker.IsOnCooldown(Caster, "bolt", 1f), Is.False);
            Assert.That(_tracker.IsOnCooldown(other, "bolt", 1f), Is.True, "other entities keep their state");
            Assert.That(executor.Update(5f), Is.EqualTo(0), "the dropped cast never lands");
            Assert.That(_log.Count, Is.EqualTo(logged), "forgetting raises no events");
            Assert.That(Use(executor, "bolt", 1f).Success, Is.True, "a fresh start");
        }
    }
}
