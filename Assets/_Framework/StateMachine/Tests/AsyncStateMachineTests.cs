using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace RPGFramework.StateMachine.Tests
{
    public sealed class AsyncStateMachineTests
    {
        private sealed class RecordingState : AsyncState<string>
        {
            private readonly string _id;
            private readonly List<string> _log;

            public RecordingState(string id, List<string> log)
            {
                _id = id;
                _log = log;
            }

            public override string Id => _id;
            public UniTaskCompletionSource EnterGate { get; set; }
            public Exception EnterFailure { get; set; }
            public Exception ExitFailure { get; set; }
            public StateTransition<string> LastEnter { get; private set; }
            public int Updates { get; private set; }

            public override async UniTask EnterAsync(StateTransition<string> transition, CancellationToken cancellationToken)
            {
                _log.Add($"enter {_id}");
                LastEnter = transition;
                if (EnterGate != null)
                    await EnterGate.Task.AttachExternalCancellation(cancellationToken);
                if (EnterFailure != null)
                    throw EnterFailure;
                _log.Add($"entered {_id}");
            }

            public override UniTask ExitAsync(StateTransition<string> transition, CancellationToken cancellationToken)
            {
                _log.Add($"exit {_id}");
                if (ExitFailure != null)
                    throw ExitFailure;
                return UniTask.CompletedTask;
            }

            public override void Update(float deltaTime) => Updates++;
        }

        private List<string> _log;
        private AsyncStateMachine<string> _machine;
        private RecordingState _boot;
        private RecordingState _menu;
        private RecordingState _game;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            _boot = new RecordingState("Boot", _log);
            _menu = new RecordingState("Menu", _log);
            _game = new RecordingState("Game", _log);
            _machine = new AsyncStateMachine<string>().Register(_boot).Register(_menu).Register(_game);
        }

        private static void Complete(UniTask task)
        {
            Assert.That(task.Status, Is.Not.EqualTo(UniTaskStatus.Pending));
            task.GetAwaiter().GetResult();
        }

        [Test]
        public void FirstTransition_HasNoFrom_LaterOnesCarryFromAndPayload()
        {
            Complete(_machine.ChangeStateAsync("Boot"));
            Assert.That(_boot.LastEnter.HasFrom, Is.False);

            Complete(_machine.ChangeStateAsync("Game", payload: new[] { "Warrior" }));

            Assert.That(_machine.CurrentStateId, Is.EqualTo("Game"));
            Assert.That(_game.LastEnter.From, Is.EqualTo("Boot"));
            Assert.That(_game.LastEnter.TryGetPayload<string[]>(out var heroes), Is.True);
            Assert.That(heroes, Is.EqualTo(new[] { "Warrior" }));
            CollectionAssert.AreEqual(new[] { "enter Boot", "entered Boot", "exit Boot", "enter Game", "entered Game" }, _log);
        }

        [Test]
        public void SameState_IsNoOp_UnlessReenter()
        {
            Complete(_machine.ChangeStateAsync("Menu"));
            _log.Clear();

            Complete(_machine.ChangeStateAsync("Menu"));
            Assert.That(_log, Is.Empty);

            Complete(_machine.ChangeStateAsync("Menu", reenter: true));
            CollectionAssert.AreEqual(new[] { "exit Menu", "enter Menu", "entered Menu" }, _log);
        }

        [Test]
        public void Update_ReachesStateOnlyAfterEnterCompleted()
        {
            _game.EnterGate = new UniTaskCompletionSource();
            var change = _machine.ChangeStateAsync("Game");

            _machine.Update(1f);
            Assert.That(_machine.IsTransitioning, Is.True);
            Assert.That(_game.Updates, Is.Zero);

            _game.EnterGate.TrySetResult();
            Complete(change);
            _machine.Update(1f);

            Assert.That(_game.Updates, Is.EqualTo(1));
            Assert.That(_machine.IsTransitioning, Is.False);
        }

        [Test]
        public void RequestsDuringTransition_RunAfterItInOrder()
        {
            _menu.EnterGate = new UniTaskCompletionSource();
            var toMenu = _machine.ChangeStateAsync("Menu");
            var toGame = _machine.ChangeStateAsync("Game");
            var toBoot = _machine.ChangeStateAsync("Boot");

            CollectionAssert.AreEqual(new[] { "enter Menu" }, _log);

            _menu.EnterGate.TrySetResult();
            Complete(toMenu);
            Complete(toGame);
            Complete(toBoot);

            CollectionAssert.AreEqual(new[]
            {
                "enter Menu", "entered Menu", "exit Menu", "enter Game", "entered Game", "exit Game", "enter Boot", "entered Boot"
            }, _log);
            Assert.That(_machine.CurrentStateId, Is.EqualTo("Boot"));
        }

        [Test]
        public void TransitionRules_RejectIllegalTransitionsBeforeAnythingRuns()
        {
            _machine.Allow("Boot", "Menu").Allow("Menu", "Game", "Boot");
            Complete(_machine.ChangeStateAsync("Boot"));
            _log.Clear();

            Assert.That(_machine.CanTransition("Game"), Is.False);
            var illegal = _machine.ChangeStateAsync("Game");
            Assert.Throws<InvalidOperationException>(() => illegal.GetAwaiter().GetResult());
            Assert.That(_log, Is.Empty);
            Assert.That(_machine.CurrentStateId, Is.EqualTo("Boot"));

            Complete(_machine.ChangeStateAsync("Menu"));
            Complete(_machine.ChangeStateAsync("Game"));
            Assert.That(_machine.CurrentStateId, Is.EqualTo("Game"));
        }

        [Test]
        public void EnterFailure_LeavesNoCurrentState_MachineStaysUsable()
        {
            Complete(_machine.ChangeStateAsync("Menu"));
            _game.EnterFailure = new InvalidOperationException("server down");

            var failed = _machine.ChangeStateAsync("Game");

            Assert.Throws<InvalidOperationException>(() => failed.GetAwaiter().GetResult());
            Assert.That(_machine.HasCurrentState, Is.False);
            Assert.That(_machine.IsTransitioning, Is.False);

            Complete(_machine.ChangeStateAsync("Menu"));
            Assert.That(_machine.CurrentStateId, Is.EqualTo("Menu"));
        }

        [Test]
        public void ExitFailure_IsLogged_TransitionContinues()
        {
            Complete(_machine.ChangeStateAsync("Menu"));
            _menu.ExitFailure = new InvalidOperationException("save failed");

            Complete(_machine.ChangeStateAsync("Game"));

            Assert.That(_machine.CurrentStateId, Is.EqualTo("Game"));
        }

        [Test]
        public void StateChanged_RaisedAfterEnter()
        {
            var changes = new List<string>();
            _machine.StateChanged += t => changes.Add(t.ToString());

            Complete(_machine.ChangeStateAsync("Boot"));
            Complete(_machine.ChangeStateAsync("Menu"));

            CollectionAssert.AreEqual(new[] { "-> Boot", "Boot -> Menu" }, changes);
        }

        [Test]
        public void CancelledQueuedRequest_IsSkipped()
        {
            _menu.EnterGate = new UniTaskCompletionSource();
            var cts = new CancellationTokenSource();
            var toMenu = _machine.ChangeStateAsync("Menu");
            var cancelled = _machine.ChangeStateAsync("Game", cancellationToken: cts.Token);
            cts.Cancel();
            _menu.EnterGate.TrySetResult();

            Complete(toMenu);
            Assert.That(cancelled.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_machine.CurrentStateId, Is.EqualTo("Menu"));
            Assert.That(_machine.IsTransitioning, Is.False);
        }

        [Test]
        public void UnknownOrDuplicateState_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _machine.ChangeStateAsync("Nope").GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => _machine.Register(new RecordingState("Boot", _log)));
        }
    }
}
