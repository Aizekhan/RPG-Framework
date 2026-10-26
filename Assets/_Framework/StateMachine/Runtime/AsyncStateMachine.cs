using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Logging;

namespace RPGFramework.StateMachine
{
    public readonly struct StateTransition<TStateId>
    {
        public StateTransition(bool hasFrom, TStateId from, TStateId to, object payload)
        {
            HasFrom = hasFrom;
            From = from;
            To = to;
            Payload = payload;
        }

        /// <summary>
        /// False for the very first transition (there was no previous state).
        /// </summary>
        public bool HasFrom { get; }
        public TStateId From { get; }
        public TStateId To { get; }

        /// <summary>
        /// Data passed to <c>ChangeStateAsync</c> (e.g. selected heroes for a gameplay state).
        /// </summary>
        public object Payload { get; }

        public bool TryGetPayload<T>(out T payload)
        {
            if (Payload is T typed)
            {
                payload = typed;
                return true;
            }
            payload = default;
            return false;
        }

        public override string ToString() => HasFrom ? $"{From} -> {To}" : $"-> {To}";
    }

    public interface IAsyncState<TStateId>
    {
        TStateId Id { get; }
        UniTask EnterAsync(StateTransition<TStateId> transition, CancellationToken cancellationToken);
        UniTask ExitAsync(StateTransition<TStateId> transition, CancellationToken cancellationToken);
        void Update(float deltaTime);
    }

    /// <summary>
    /// Convenience base: override only what the state needs.
    /// </summary>
    public abstract class AsyncState<TStateId> : IAsyncState<TStateId>
    {
        public abstract TStateId Id { get; }

        public virtual UniTask EnterAsync(StateTransition<TStateId> transition, CancellationToken cancellationToken) => UniTask.CompletedTask;

        public virtual UniTask ExitAsync(StateTransition<TStateId> transition, CancellationToken cancellationToken) => UniTask.CompletedTask;

        public virtual void Update(float deltaTime)
        {
        }
    }

    /// <summary>
    /// State machine whose enter/exit steps may be asynchronous (load a scene, show a menu, connect to a server).
    /// <list type="bullet">
    /// <item>Transitions are serialised: a request made during a transition runs after it, in order.</item>
    /// <item><see cref="Update"/> only reaches a state after its <c>EnterAsync</c> has completed.</item>
    /// <item>Optional transition rules reject illegal transitions before anything runs.</item>
    /// <item>If <c>EnterAsync</c> fails, the machine has no current state and the error is rethrown, so the
    /// caller can move to an error/fallback state.</item>
    /// </list>
    /// State ids can be enums, strings or any type with value equality.
    /// </summary>
    public sealed class AsyncStateMachine<TStateId>
    {
        private const string Category = "StateMachine";

        private readonly Dictionary<TStateId, IAsyncState<TStateId>> _states = new Dictionary<TStateId, IAsyncState<TStateId>>();
        private readonly Dictionary<TStateId, HashSet<TStateId>> _allowed = new Dictionary<TStateId, HashSet<TStateId>>();
        private readonly Queue<UniTaskCompletionSource> _waiting = new Queue<UniTaskCompletionSource>();
        private readonly ILogger _logger;
        private IAsyncState<TStateId> _current;
        private bool _transitioning;

        public AsyncStateMachine(ILogger logger = null)
        {
            _logger = logger;
        }

        public bool HasCurrentState => _current != null;

        public TStateId CurrentStateId => _current != null ? _current.Id : default;

        public bool IsTransitioning => _transitioning;

        /// <summary>
        /// Raised after a transition completes (the new state has been entered).
        /// </summary>
        public event Action<StateTransition<TStateId>> StateChanged;

        public AsyncStateMachine<TStateId> Register(IAsyncState<TStateId> state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (_states.ContainsKey(state.Id))
                throw new InvalidOperationException($"State {state.Id} is already registered");
            _states.Add(state.Id, state);
            return this;
        }

        public bool HasState(TStateId id) => _states.ContainsKey(id);

        /// <summary>
        /// Declares a legal transition. Once any rule exists for a source state, only declared targets are allowed
        /// from it; states without rules may go anywhere.
        /// </summary>
        public AsyncStateMachine<TStateId> Allow(TStateId from, params TStateId[] to)
        {
            if (!_allowed.TryGetValue(from, out var targets))
            {
                targets = new HashSet<TStateId>();
                _allowed.Add(from, targets);
            }
            foreach (var target in to)
                targets.Add(target);
            return this;
        }

        public bool CanTransition(TStateId to)
        {
            if (!_states.ContainsKey(to))
                return false;
            if (_current == null || !_allowed.TryGetValue(_current.Id, out var targets))
                return true;
            return targets.Contains(to);
        }

        /// <summary>
        /// Exits the current state and enters <paramref name="to"/>. Changing to the current state is a no-op unless
        /// <paramref name="reenter"/> is true.
        /// </summary>
        public async UniTask ChangeStateAsync(TStateId to, object payload = null, bool reenter = false,
            CancellationToken cancellationToken = default)
        {
            if (!_states.ContainsKey(to))
                throw new InvalidOperationException($"State {to} is not registered");

            await EnterQueueAsync(cancellationToken);
            try
            {
                await TransitionAsync(to, payload, reenter, cancellationToken);
            }
            finally
            {
                ExitQueue();
            }
        }

        /// <summary>
        /// Ticks the current state; skipped while a transition is in progress. Exceptions are logged.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_transitioning || _current == null)
                return;

            try
            {
                _current.Update(deltaTime);
            }
            catch (Exception ex)
            {
                _logger.LogError($"State {_current.Id} update failed", Category, ex);
            }
        }

        private async UniTask TransitionAsync(TStateId to, object payload, bool reenter, CancellationToken cancellationToken)
        {
            var target = _states[to];
            if (_current != null && ReferenceEquals(_current, target) && !reenter)
                return;
            if (!CanTransition(to))
                throw new InvalidOperationException($"Transition {(_current != null ? _current.Id.ToString() : "<none>")} -> {to} is not allowed");

            var transition = _current != null
                ? new StateTransition<TStateId>(true, _current.Id, to, payload)
                : new StateTransition<TStateId>(false, default, to, payload);

            _logger.LogInfo($"State transition {transition}", Category);

            if (_current != null)
            {
                var leaving = _current;
                try
                {
                    await leaving.ExitAsync(transition, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw; // still in the old state
                }
                catch (Exception ex)
                {
                    // A broken exit must not trap the game in a state; log and continue the transition
                    _logger.LogError($"State {leaving.Id} exit failed", Category, ex);
                }
            }

            _current = null;
            try
            {
                await target.EnterAsync(transition, cancellationToken);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    _logger.LogError($"State {to} enter failed", Category, ex);
                throw;
            }

            _current = target;
            StateChanged?.Invoke(transition);
        }

        private async UniTask EnterQueueAsync(CancellationToken cancellationToken)
        {
            if (!_transitioning)
            {
                _transitioning = true;
                return;
            }

            var turn = new UniTaskCompletionSource();
            _waiting.Enqueue(turn);
            try
            {
                await turn.Task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!turn.TrySetCanceled())
                    ExitQueue();
                throw;
            }
        }

        private void ExitQueue()
        {
            while (_waiting.Count > 0)
            {
                if (_waiting.Dequeue().TrySetResult())
                    return;
            }
            _transitioning = false;
        }
    }
}
