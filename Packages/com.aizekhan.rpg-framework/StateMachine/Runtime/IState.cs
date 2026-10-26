using System;

namespace RPGFramework.StateMachine
{
    /// <summary>
    /// Core state interface for generic state machine.
    /// Framework contract with no engine or game dependencies.
    /// </summary>
    /// <typeparam name="TStateId">Enum type representing state identifiers</typeparam>
    public interface IState<TStateId> where TStateId : Enum
    {
        /// <summary>
        /// Unique identifier for this state.
        /// </summary>
        TStateId StateId { get; }

        /// <summary>
        /// Called when entering this state.
        /// </summary>
        /// <param name="previousState">The state being transitioned from</param>
        void Enter(TStateId previousState);

        /// <summary>
        /// Called every update tick while this state is active.
        /// </summary>
        void Update();

        /// <summary>
        /// Called when exiting this state.
        /// </summary>
        void Exit();
    }
}
