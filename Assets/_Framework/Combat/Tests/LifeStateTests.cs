using NUnit.Framework;
using RPGFramework.Combat;
using System;

namespace RPGFramework.Combat.Tests
{
    [TestFixture]
    public class LifeStateTests
    {
        private LifeStateManager _manager;
        private TestLifeStateHandler _testHandler;
        private const int TestEntityId = 1;
        private const int KillerId = 2;

        [SetUp]
        public void SetUp()
        {
            _manager = new LifeStateManager();
            _testHandler = new TestLifeStateHandler();
            _manager.RegisterEntity(TestEntityId, _testHandler);
        }

        #region Life State Transitions (8 tests)

        [Test]
        public void AliveToDeadTransition_ShouldSucceed()
        {
            // Act
            bool result = _manager.Kill(TestEntityId, KillerId, "test_death");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(LifeState.Dead, _manager.GetLifeState(TestEntityId));
            Assert.IsTrue(_testHandler.OnDeathCalled);
            Assert.AreEqual(KillerId, _testHandler.LastKillerId);
            Assert.AreEqual("test_death", _testHandler.LastDeathReason);
        }

        [Test]
        public void DeadToAliveTransition_ShouldSucceed()
        {
            // Arrange
            _manager.Kill(TestEntityId, KillerId, "test_death");

            // Act
            bool result = _manager.Resurrect(TestEntityId, -1, "respawn");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(LifeState.Alive, _manager.GetLifeState(TestEntityId));
            Assert.IsTrue(_testHandler.OnResurrectCalled);
            Assert.AreEqual(-1, _testHandler.LastResurrectorId);
            Assert.AreEqual("respawn", _testHandler.LastResurrectType);
        }

        [Test]
        public void InvulnerableState_ShouldPreventDeath()
        {
            // Arrange
            _manager.SetInvulnerable(TestEntityId, 10f);

            // Act
            bool result = _manager.Kill(TestEntityId, KillerId, "test_death");

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(LifeState.Invulnerable, _manager.GetLifeState(TestEntityId));
            Assert.IsFalse(_testHandler.OnDeathCalled);
        }

        [Test]
        public void RemoveInvulnerability_ShouldTransitionToAlive()
        {
            // Arrange
            _manager.SetInvulnerable(TestEntityId, 10f);

            // Act
            _manager.RemoveInvulnerability(TestEntityId);

            // Assert
            Assert.AreEqual(LifeState.Alive, _manager.GetLifeState(TestEntityId));
        }

        [Test]
        public void InvalidTransition_DeadToDead_ShouldFail()
        {
            // Arrange
            _manager.Kill(TestEntityId, KillerId, "test_death");
            _testHandler.Reset();

            // Act
            bool result = _manager.Kill(TestEntityId, KillerId, "second_death");

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(_testHandler.OnDeathCalled);
        }

        [Test]
        public void InvalidTransition_AliveToAlive_ShouldFail()
        {
            // Act
            bool result = _manager.Resurrect(TestEntityId, -1, "respawn");

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(_testHandler.OnResurrectCalled);
        }

        [Test]
        public void OnLifeStateChanged_ShouldBeCalledOnTransition()
        {
            // Act
            _manager.Kill(TestEntityId, KillerId, "test_death");

            // Assert
            Assert.IsTrue(_testHandler.OnLifeStateChangedCalled);
            Assert.AreEqual(LifeState.Alive, _testHandler.LastOldState);
            Assert.AreEqual(LifeState.Dead, _testHandler.LastNewState);
        }

        [Test]
        public void MultipleTransitions_ShouldWorkCorrectly()
        {
            // Act & Assert - Alive → Dead
            _manager.Kill(TestEntityId, KillerId, "test_death");
            Assert.AreEqual(LifeState.Dead, _manager.GetLifeState(TestEntityId));

            // Dead → Alive
            _manager.Resurrect(TestEntityId, -1, "respawn");
            Assert.AreEqual(LifeState.Alive, _manager.GetLifeState(TestEntityId));

            // Alive → Invulnerable
            _manager.SetInvulnerable(TestEntityId, 5f);
            Assert.AreEqual(LifeState.Invulnerable, _manager.GetLifeState(TestEntityId));

            // Invulnerable → Alive
            _manager.RemoveInvulnerability(TestEntityId);
            Assert.AreEqual(LifeState.Alive, _manager.GetLifeState(TestEntityId));
        }

        #endregion

        #region Death Conditions (6 tests)

        [Test]
        public void ResourceDepletionCondition_ShouldTriggerDeath()
        {
            // Arrange
            var condition = new ResourceDepletionDeathCondition("Health");

            // Act
            bool shouldDie = _manager.ShouldDie(TestEntityId, condition);

            // Assert
            Assert.IsTrue(shouldDie); // TestLifeStateHandler allows death by default
        }

        [Test]
        public void ResourceDepletionCondition_ShouldReturnCorrectReason()
        {
            // Arrange
            var condition = new ResourceDepletionDeathCondition("Health");

            // Act
            string reason = condition.GetDeathReason();

            // Assert
            Assert.AreEqual("resource_depletion_health", reason);
        }

        [Test]
        public void DeathCondition_WithImmortalEntity_ShouldReturnFalse()
        {
            // Arrange
            _testHandler.SetCanDie(false);
            var condition = new ResourceDepletionDeathCondition("Health");

            // Act
            bool shouldDie = _manager.ShouldDie(TestEntityId, condition);

            // Assert
            Assert.IsFalse(shouldDie);
        }

        [Test]
        public void DeathCondition_Priority_ShouldBeConfigurable()
        {
            // Arrange
            var highPriority = new ResourceDepletionDeathCondition("Health", 0f, 200);
            var lowPriority = new ResourceDepletionDeathCondition("Stamina", 0f, 50);

            // Assert
            Assert.AreEqual(200, highPriority.Priority);
            Assert.AreEqual(50, lowPriority.Priority);
        }

        [Test]
        public void DeathCondition_CustomThreshold_ShouldBeStored()
        {
            // Arrange
            var condition = new ResourceDepletionDeathCondition("Oxygen", -10f);

            // Assert
            Assert.AreEqual("Oxygen", condition.ResourceName);
            Assert.AreEqual(-10f, condition.DepletionThreshold);
        }

        [Test]
        public void DeathCondition_MultipleConditions_ShouldEvaluateIndependently()
        {
            // Arrange
            var healthCondition = new ResourceDepletionDeathCondition("Health");
            var staminaCondition = new ResourceDepletionDeathCondition("Stamina");

            // Act
            bool healthCheck = _manager.ShouldDie(TestEntityId, healthCondition);
            bool staminaCheck = _manager.ShouldDie(TestEntityId, staminaCondition);

            // Assert
            Assert.IsTrue(healthCheck);
            Assert.IsTrue(staminaCheck);
        }

        #endregion

        #region Death Detection (6 tests)

        [Test]
        public void Kill_ShouldPublishDeathEvent()
        {
            // Arrange
            DeathEvent? capturedEvent = null;
            _manager.OnDeath += (evt) => capturedEvent = evt;

            // Act
            _manager.Kill(TestEntityId, KillerId, "combat");

            // Assert
            Assert.IsNotNull(capturedEvent);
            Assert.AreEqual(TestEntityId, capturedEvent.Value.EntityId);
            Assert.AreEqual(KillerId, capturedEvent.Value.KillerEntityId);
            Assert.AreEqual("combat", capturedEvent.Value.DeathReason);
        }

        [Test]
        public void Kill_WithEnvironmentalDeath_ShouldUseNegativeKillerId()
        {
            // Act
            _manager.Kill(TestEntityId, -1, "environmental");

            // Assert
            Assert.IsTrue(_testHandler.OnDeathCalled);
            Assert.AreEqual(-1, _testHandler.LastKillerId);
        }

        [Test]
        public void Kill_ShouldRecordKillerIdInComponent()
        {
            // Act
            _manager.Kill(TestEntityId, KillerId, "combat");

            // Assert
            Assert.AreEqual(KillerId, _testHandler.LastKillerId);
        }

        [Test]
        public void Kill_ShouldRecordDeathReason()
        {
            // Act
            _manager.Kill(TestEntityId, KillerId, "instant_death");

            // Assert
            Assert.AreEqual("instant_death", _testHandler.LastDeathReason);
        }

        [Test]
        public void Kill_ImmortalEntity_ShouldFail()
        {
            // Arrange
            _testHandler.SetCanDie(false);
            _manager.UnregisterEntity(TestEntityId);
            _manager.RegisterEntity(TestEntityId, _testHandler, LifeStateComponent.CreateImmortal());

            // Act
            bool result = _manager.Kill(TestEntityId, KillerId, "test_death");

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(LifeState.Alive, _manager.GetLifeState(TestEntityId));
        }

        [Test]
        public void IsDead_ShouldReturnCorrectValue()
        {
            // Assert - Initially alive
            Assert.IsFalse(_manager.IsDead(TestEntityId));

            // Act
            _manager.Kill(TestEntityId, KillerId, "test_death");

            // Assert - Now dead
            Assert.IsTrue(_manager.IsDead(TestEntityId));
        }

        #endregion

        #region Resurrection (6 tests)

        [Test]
        public void Resurrect_ShouldPublishResurrectionEvent()
        {
            // Arrange
            _manager.Kill(TestEntityId, KillerId, "test_death");
            ResurrectionEvent? capturedEvent = null;
            _manager.OnResurrection += (evt) => capturedEvent = evt;

            // Act
            _manager.Resurrect(TestEntityId, -1, "auto_respawn");

            // Assert
            Assert.IsNotNull(capturedEvent);
            Assert.AreEqual(TestEntityId, capturedEvent.Value.EntityId);
            Assert.AreEqual(-1, capturedEvent.Value.ResurrectorEntityId);
            Assert.AreEqual("auto_respawn", capturedEvent.Value.ResurrectionType);
        }

        [Test]
        public void Resurrect_WithPlayerRevive_ShouldRecordResurrector()
        {
            // Arrange
            _manager.Kill(TestEntityId, KillerId, "test_death");
            const int healerId = 5;

            // Act
            _manager.Resurrect(TestEntityId, healerId, "player_revive");

            // Assert
            Assert.IsTrue(_testHandler.OnResurrectCalled);
            Assert.AreEqual(healerId, _testHandler.LastResurrectorId);
        }

        [Test]
        public void Resurrect_NonDeadEntity_ShouldFail()
        {
            // Act
            bool result = _manager.Resurrect(TestEntityId, -1, "respawn");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void Resurrect_EntityWithResurrectionDisabled_ShouldFail()
        {
            // Arrange
            _manager.Kill(TestEntityId, KillerId, "permanent_death");
            _testHandler.SetCanResurrect(false);
            _manager.UnregisterEntity(TestEntityId);
            _manager.RegisterEntity(TestEntityId, _testHandler, new LifeStateComponent
            {
                CurrentState = LifeState.Dead,
                CanDie = true,
                CanResurrect = false,
                InvulnerabilityEndTime = -1f,
                DeathTime = 0f,
                LastKillerId = KillerId,
                LastDeathReason = "permanent_death"
            });

            // Act
            bool result = _manager.Resurrect(TestEntityId, -1, "respawn");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void Resurrect_MultipleResurrections_ShouldWorkCorrectly()
        {
            // First death and resurrection
            _manager.Kill(TestEntityId, KillerId, "death1");
            _manager.Resurrect(TestEntityId, -1, "respawn1");
            Assert.IsTrue(_manager.IsAlive(TestEntityId));

            // Second death and resurrection
            _manager.Kill(TestEntityId, KillerId, "death2");
            _manager.Resurrect(TestEntityId, -1, "respawn2");
            Assert.IsTrue(_manager.IsAlive(TestEntityId));
        }

        [Test]
        public void IsAlive_ShouldReturnCorrectValue()
        {
            // Assert - Initially alive
            Assert.IsTrue(_manager.IsAlive(TestEntityId));

            // Act - Kill
            _manager.Kill(TestEntityId, KillerId, "test_death");
            Assert.IsFalse(_manager.IsAlive(TestEntityId));

            // Act - Resurrect
            _manager.Resurrect(TestEntityId, -1, "respawn");
            Assert.IsTrue(_manager.IsAlive(TestEntityId));
        }

        #endregion

        #region Invulnerability (4 tests)

        [Test]
        public void SetInvulnerable_TemporaryDuration_ShouldSetCorrectly()
        {
            // Act
            _manager.SetInvulnerable(TestEntityId, 5f);

            // Assert
            Assert.IsTrue(_manager.IsInvulnerable(TestEntityId));
            Assert.AreEqual(LifeState.Invulnerable, _manager.GetLifeState(TestEntityId));
        }

        [Test]
        public void SetInvulnerable_PermanentDuration_ShouldWork()
        {
            // Act
            _manager.SetInvulnerable(TestEntityId, -1f);

            // Assert
            Assert.IsTrue(_manager.IsInvulnerable(TestEntityId));
        }

        [Test]
        public void InvulnerableEntity_CanStillTakeDamage_ButNotDie()
        {
            // Arrange
            _manager.SetInvulnerable(TestEntityId, 10f);

            // Act - Try to kill
            bool killResult = _manager.Kill(TestEntityId, KillerId, "test_death");

            // Assert
            Assert.IsFalse(killResult);
            Assert.IsTrue(_manager.IsInvulnerable(TestEntityId));
            Assert.IsFalse(_manager.IsDead(TestEntityId));
        }

        [Test]
        public void IsInvulnerable_ShouldReturnFalseForNonInvulnerableStates()
        {
            // Alive state
            Assert.IsFalse(_manager.IsInvulnerable(TestEntityId));

            // Dead state
            _manager.Kill(TestEntityId, KillerId, "test_death");
            Assert.IsFalse(_manager.IsInvulnerable(TestEntityId));
        }

        #endregion

        #region Component Creation (2 tests)

        [Test]
        public void LifeStateComponent_CreateAlive_ShouldHaveCorrectDefaults()
        {
            // Act
            var component = LifeStateComponent.CreateAlive();

            // Assert
            Assert.AreEqual(LifeState.Alive, component.CurrentState);
            Assert.IsTrue(component.CanDie);
            Assert.IsTrue(component.CanResurrect);
            Assert.AreEqual(-1f, component.InvulnerabilityEndTime);
            Assert.AreEqual(-1f, component.DeathTime);
            Assert.AreEqual(-1, component.LastKillerId);
            Assert.AreEqual(string.Empty, component.LastDeathReason);
        }

        [Test]
        public void LifeStateComponent_CreateImmortal_ShouldHaveCorrectSettings()
        {
            // Act
            var component = LifeStateComponent.CreateImmortal();

            // Assert
            Assert.AreEqual(LifeState.Alive, component.CurrentState);
            Assert.IsFalse(component.CanDie);
            Assert.IsFalse(component.CanResurrect);
            Assert.AreEqual(float.MaxValue, component.InvulnerabilityEndTime);
        }

        #endregion

        #region Edge Cases (2 tests)

        [Test]
        public void UnregisteredEntity_ShouldReturnDeadState()
        {
            // Arrange
            const int unregisteredId = 999;

            // Act
            var state = _manager.GetLifeState(unregisteredId);

            // Assert
            Assert.AreEqual(LifeState.Dead, state);
            Assert.IsFalse(_manager.IsAlive(unregisteredId));
            Assert.IsTrue(_manager.IsDead(unregisteredId));
        }

        [Test]
        public void UnregisterEntity_ShouldRemoveFromManager()
        {
            // Act
            _manager.UnregisterEntity(TestEntityId);

            // Assert
            Assert.AreEqual(LifeState.Dead, _manager.GetLifeState(TestEntityId));
        }

        #endregion

        #region Test Helper Class

        /// <summary>
        /// Test implementation of ILifeStateHandler for unit testing.
        /// </summary>
        private class TestLifeStateHandler : ILifeStateHandler
        {
            private bool _canDie = true;
            private bool _canResurrect = true;

            public LifeState CurrentState { get; set; } = LifeState.Alive;
            public bool CanDie => _canDie;
            public bool CanResurrect => _canResurrect;

            // Track callback invocations
            public bool OnDeathCalled { get; private set; }
            public int LastKillerId { get; private set; }
            public string LastDeathReason { get; private set; }

            public bool OnResurrectCalled { get; private set; }
            public int LastResurrectorId { get; private set; }
            public string LastResurrectType { get; private set; }

            public bool OnLifeStateChangedCalled { get; private set; }
            public LifeState LastOldState { get; private set; }
            public LifeState LastNewState { get; private set; }

            public void OnDeath(int killerId, string reason)
            {
                OnDeathCalled = true;
                LastKillerId = killerId;
                LastDeathReason = reason;
            }

            public void OnResurrect(int resurrectorId, string resurrectType)
            {
                OnResurrectCalled = true;
                LastResurrectorId = resurrectorId;
                LastResurrectType = resurrectType;
            }

            public void OnLifeStateChanged(LifeState oldState, LifeState newState)
            {
                OnLifeStateChangedCalled = true;
                LastOldState = oldState;
                LastNewState = newState;
            }

            public bool IsAlive() => CurrentState == LifeState.Alive || CurrentState == LifeState.Invulnerable;
            public bool IsDead() => CurrentState == LifeState.Dead;
            public bool IsInvulnerable() => CurrentState == LifeState.Invulnerable;

            public void SetCanDie(bool value) => _canDie = value;
            public void SetCanResurrect(bool value) => _canResurrect = value;

            public void Reset()
            {
                OnDeathCalled = false;
                OnResurrectCalled = false;
                OnLifeStateChangedCalled = false;
                LastKillerId = -1;
                LastDeathReason = null;
                LastResurrectorId = -1;
                LastResurrectType = null;
                LastOldState = LifeState.Alive;
                LastNewState = LifeState.Alive;
            }
        }

        #endregion
    }
}
