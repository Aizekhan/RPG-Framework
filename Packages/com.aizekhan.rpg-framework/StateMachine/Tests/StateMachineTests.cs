using NUnit.Framework;
using System;

namespace RPGFramework.StateMachine.Tests
{
    [TestFixture]
    public class StateMachineTests
    {
        private enum TestState
        {
            None,
            Idle,
            Walking,
            Running,
            Attacking
        }

        private class TestStateImpl : IState<TestState>
        {
            public TestState StateId { get; }
            public int EnterCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int ExitCount { get; private set; }
            public TestState LastPreviousState { get; private set; }

            public TestStateImpl(TestState stateId)
            {
                StateId = stateId;
            }

            public void Enter(TestState previousState)
            {
                EnterCount++;
                LastPreviousState = previousState;
            }

            public void Update()
            {
                UpdateCount++;
            }

            public void Exit()
            {
                ExitCount++;
            }

            public void Reset()
            {
                EnterCount = 0;
                UpdateCount = 0;
                ExitCount = 0;
                LastPreviousState = TestState.None;
            }
        }

        private StateMachine<TestState> _stateMachine;
        private TestStateImpl _idleState;
        private TestStateImpl _walkingState;
        private TestStateImpl _runningState;
        private TestStateImpl _attackingState;

        [SetUp]
        public void Setup()
        {
            _stateMachine = new StateMachine<TestState>();
            _idleState = new TestStateImpl(TestState.Idle);
            _walkingState = new TestStateImpl(TestState.Walking);
            _runningState = new TestStateImpl(TestState.Running);
            _attackingState = new TestStateImpl(TestState.Attacking);
        }

        [Test]
        public void Initialize_SetsInitializedFlag()
        {
            // Act
            _stateMachine.Initialize();

            // Assert - should not throw on subsequent operations
            Assert.DoesNotThrow(() => _stateMachine.Update());
        }

        [Test]
        public void Initialize_CalledTwice_ThrowsException()
        {
            // Arrange
            _stateMachine.Initialize();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _stateMachine.Initialize());
        }

        [Test]
        public void RegisterState_AddsStateToMachine()
        {
            // Act
            _stateMachine.RegisterState(_idleState);

            // Assert
            Assert.IsTrue(_stateMachine.HasState(TestState.Idle));
        }

        [Test]
        public void RegisterState_NullState_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _stateMachine.RegisterState(null));
        }

        [Test]
        public void RegisterState_DuplicateState_ThrowsInvalidOperationException()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);

            // Act & Assert
            var duplicateIdle = new TestStateImpl(TestState.Idle);
            Assert.Throws<InvalidOperationException>(() => _stateMachine.RegisterState(duplicateIdle));
        }

        [Test]
        public void HasState_RegisteredState_ReturnsTrue()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);

            // Act & Assert
            Assert.IsTrue(_stateMachine.HasState(TestState.Idle));
        }

        [Test]
        public void HasState_UnregisteredState_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(_stateMachine.HasState(TestState.Idle));
        }

        [Test]
        public void ChangeState_WithoutInitialize_ThrowsInvalidOperationException()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _stateMachine.ChangeState(TestState.Idle));
        }

        [Test]
        public void ChangeState_UnregisteredState_ThrowsInvalidOperationException()
        {
            // Arrange
            _stateMachine.Initialize();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _stateMachine.ChangeState(TestState.Idle));
        }

        [Test]
        public void ChangeState_ValidTransition_CallsEnterOnNewState()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.Initialize();

            // Act
            _stateMachine.ChangeState(TestState.Idle);

            // Assert
            Assert.AreEqual(1, _idleState.EnterCount);
            Assert.AreEqual(TestState.None, _idleState.LastPreviousState);
        }

        [Test]
        public void ChangeState_ValidTransition_CallsExitOnPreviousState()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.RegisterState(_walkingState);
            _stateMachine.Initialize();
            _stateMachine.ChangeState(TestState.Idle);

            // Act
            _stateMachine.ChangeState(TestState.Walking);

            // Assert
            Assert.AreEqual(1, _idleState.ExitCount);
            Assert.AreEqual(1, _walkingState.EnterCount);
            Assert.AreEqual(TestState.Idle, _walkingState.LastPreviousState);
        }

        [Test]
        public void ChangeState_TransitionChain_MaintainsCorrectPreviousState()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.RegisterState(_walkingState);
            _stateMachine.RegisterState(_runningState);
            _stateMachine.Initialize();

            // Act
            _stateMachine.ChangeState(TestState.Idle);
            _stateMachine.ChangeState(TestState.Walking);
            _stateMachine.ChangeState(TestState.Running);

            // Assert
            Assert.AreEqual(TestState.None, _idleState.LastPreviousState);
            Assert.AreEqual(TestState.Idle, _walkingState.LastPreviousState);
            Assert.AreEqual(TestState.Walking, _runningState.LastPreviousState);
        }

        [Test]
        public void CurrentState_BeforeAnyTransition_ReturnsDefault()
        {
            // Arrange
            _stateMachine.Initialize();

            // Assert
            Assert.AreEqual(TestState.None, _stateMachine.CurrentState);
        }

        [Test]
        public void CurrentState_AfterTransition_ReturnsNewState()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.Initialize();

            // Act
            _stateMachine.ChangeState(TestState.Idle);

            // Assert
            Assert.AreEqual(TestState.Idle, _stateMachine.CurrentState);
        }

        [Test]
        public void Update_WithoutInitialize_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _stateMachine.Update());
        }

        [Test]
        public void Update_NoCurrentState_DoesNotThrow()
        {
            // Arrange
            _stateMachine.Initialize();

            // Act & Assert
            Assert.DoesNotThrow(() => _stateMachine.Update());
        }

        [Test]
        public void Update_WithCurrentState_CallsStateUpdate()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.Initialize();
            _stateMachine.ChangeState(TestState.Idle);

            // Act
            _stateMachine.Update();
            _stateMachine.Update();
            _stateMachine.Update();

            // Assert
            Assert.AreEqual(3, _idleState.UpdateCount);
        }

        [Test]
        public void Update_AfterStateChange_CallsNewStateUpdate()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.RegisterState(_walkingState);
            _stateMachine.Initialize();
            _stateMachine.ChangeState(TestState.Idle);
            _stateMachine.Update();

            // Act
            _stateMachine.ChangeState(TestState.Walking);
            _stateMachine.Update();

            // Assert
            Assert.AreEqual(1, _idleState.UpdateCount);
            Assert.AreEqual(1, _walkingState.UpdateCount);
        }

        [Test]
        public void ChangeState_ComplexScenario_MaintainsCorrectLifecycle()
        {
            // Arrange
            _stateMachine.RegisterState(_idleState);
            _stateMachine.RegisterState(_walkingState);
            _stateMachine.RegisterState(_runningState);
            _stateMachine.RegisterState(_attackingState);
            _stateMachine.Initialize();

            // Act - Simulate game flow: Idle -> Walking -> Running -> Attacking -> Idle
            _stateMachine.ChangeState(TestState.Idle);
            _stateMachine.Update();

            _stateMachine.ChangeState(TestState.Walking);
            _stateMachine.Update();
            _stateMachine.Update();

            _stateMachine.ChangeState(TestState.Running);
            _stateMachine.Update();

            _stateMachine.ChangeState(TestState.Attacking);
            _stateMachine.Update();

            _stateMachine.ChangeState(TestState.Idle);

            // Assert
            Assert.AreEqual(2, _idleState.EnterCount);
            Assert.AreEqual(1, _idleState.ExitCount);
            Assert.AreEqual(1, _idleState.UpdateCount);

            Assert.AreEqual(1, _walkingState.EnterCount);
            Assert.AreEqual(1, _walkingState.ExitCount);
            Assert.AreEqual(2, _walkingState.UpdateCount);

            Assert.AreEqual(1, _runningState.EnterCount);
            Assert.AreEqual(1, _runningState.ExitCount);
            Assert.AreEqual(1, _runningState.UpdateCount);

            Assert.AreEqual(1, _attackingState.EnterCount);
            Assert.AreEqual(1, _attackingState.ExitCount);
            Assert.AreEqual(1, _attackingState.UpdateCount);

            Assert.AreEqual(TestState.Idle, _stateMachine.CurrentState);
        }
    }
}
