using System;
using System.Collections.Generic;

namespace RPGFramework.StateMachine
{
    /// <summary>
    /// Generic state machine implementation.
    /// Framework runtime with no engine or game dependencies.
    /// </summary>
    /// <typeparam name="TStateId">Enum type representing state identifiers</typeparam>
    public class StateMachine<TStateId> : IStateMachine<TStateId> where TStateId : Enum
    {
        private readonly Dictionary<TStateId, IState<TStateId>> _states;
        private IState<TStateId> _currentState;
        private bool _isInitialized;

        public TStateId CurrentState => _currentState != null ? _currentState.StateId : default;

        public StateMachine()
        {
            _states = new Dictionary<TStateId, IState<TStateId>>();
            _isInitialized = false;
        }

        public virtual void Initialize()
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("StateMachine is already initialized");
            }

            _isInitialized = true;
        }

        public void RegisterState(IState<TStateId> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(state.StateId))
            {
                throw new InvalidOperationException($"State {state.StateId} is already registered");
            }

            _states[state.StateId] = state;
        }

        public bool HasState(TStateId stateId)
        {
            return _states.ContainsKey(stateId);
        }

        public void Update()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("StateMachine must be initialized before Update");
            }

            _currentState?.Update();
        }

        public void ChangeState(TStateId newState)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("StateMachine must be initialized before ChangeState");
            }

            // Allow re-entering the same state (some games need this)
            // If you want to prevent it, uncomment:
            // if (_currentState != null && _currentState.StateId.Equals(newState))
            //     return;

            if (!_states.TryGetValue(newState, out var nextState))
            {
                throw new InvalidOperationException($"State {newState} is not registered");
            }

            TStateId previousStateId = _currentState != null ? _currentState.StateId : default;

            // Exit current state
            _currentState?.Exit();

            // Transition to new state
            _currentState = nextState;

            // Enter new state
            _currentState.Enter(previousStateId);
        }
    }
}
