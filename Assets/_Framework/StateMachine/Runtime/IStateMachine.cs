using System;

namespace RPGFramework.StateMachine
{
    /// <summary>
    /// Generic state machine interface.
    /// Framework contract with no engine or game dependencies.
    /// </summary>
    /// <typeparam name="TStateId">Enum type representing state identifiers</typeparam>
    public interface IStateMachine<TStateId> where TStateId : Enum
    {
        /// <summary>
        /// Current active state identifier.
        /// </summary>
        TStateId CurrentState { get; }

        /// <summary>
        /// Initialize the state machine and register all states.
        /// Must be called before ChangeState or Update.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Update the current state.
        /// Should be called every frame/tick.
        /// </summary>
        void Update();

        /// <summary>
        /// Transition to a new state.
        /// </summary>
        /// <param name="newState">Target state identifier</param>
        void ChangeState(TStateId newState);

        /// <summary>
        /// Register a state with the state machine.
        /// </summary>
        /// <param name="state">State instance to register</param>
        void RegisterState(IState<TStateId> state);

        /// <summary>
        /// Check if a state is registered.
        /// </summary>
        /// <param name="stateId">State identifier to check</param>
        /// <returns>True if state is registered</returns>
        bool HasState(TStateId stateId);
    }
}
