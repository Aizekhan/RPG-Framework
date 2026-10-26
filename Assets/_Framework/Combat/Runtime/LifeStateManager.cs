using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Default implementation of ILifeStateManager.
    /// </summary>
    /// <remarks>
    /// Manages life states for entities using a dictionary-based approach.
    /// This is a simple in-memory implementation suitable for most RPG scenarios.
    /// </remarks>
    public class LifeStateManager : ILifeStateManager
    {
        private readonly Dictionary<int, LifeStateComponent> _lifeStates;
        private readonly Dictionary<int, ILifeStateHandler> _handlers;
        private readonly IEventPublisher _events;

        /// <summary>
        /// Event raised when an entity dies.
        /// </summary>
        public event Action<DeathEvent> OnDeath;

        /// <summary>
        /// Event raised when an entity is resurrected.
        /// </summary>
        public event Action<ResurrectionEvent> OnResurrection;

        /// <summary>
        /// Initializes a new instance of the LifeStateManager class.
        /// </summary>
        /// <param name="events">Optional bus that also receives <see cref="DeathEvent"/> and <see cref="ResurrectionEvent"/>.</param>
        public LifeStateManager(IEventPublisher events = null)
        {
            _events = events;
            _lifeStates = new Dictionary<int, LifeStateComponent>();
            _handlers = new Dictionary<int, ILifeStateHandler>();
        }

        /// <summary>
        /// Registers an entity with the life state manager.
        /// </summary>
        /// <param name="entityId">ID of the entity to register.</param>
        /// <param name="handler">Life state handler for the entity.</param>
        /// <param name="initialState">Initial life state component. If null, creates a default alive state.</param>
        public void RegisterEntity(int entityId, ILifeStateHandler handler, LifeStateComponent? initialState = null)
        {
            _handlers[entityId] = handler;
            _lifeStates[entityId] = initialState ?? LifeStateComponent.CreateAlive();
        }

        /// <summary>
        /// Unregisters an entity from the life state manager.
        /// </summary>
        /// <param name="entityId">ID of the entity to unregister.</param>
        public void UnregisterEntity(int entityId)
        {
            _handlers.Remove(entityId);
            _lifeStates.Remove(entityId);
        }

        /// <summary>
        /// Kills the specified entity.
        /// </summary>
        public bool Kill(int entityId, int killerId, string reason)
        {
            if (!_lifeStates.TryGetValue(entityId, out var lifeState))
                return false;

            // Cannot kill if already dead
            if (lifeState.CurrentState == LifeState.Dead)
                return false;

            // Cannot kill if entity cannot die
            if (!lifeState.CanDie)
                return false;

            // Cannot kill if invulnerable
            if (lifeState.CurrentState == LifeState.Invulnerable)
                return false;

            // Transition to dead state
            var oldState = lifeState.CurrentState;
            lifeState.CurrentState = LifeState.Dead;
            lifeState.DeathTime = GetCurrentTime();
            lifeState.LastKillerId = killerId;
            lifeState.LastDeathReason = reason;
            _lifeStates[entityId] = lifeState;

            // Notify handler
            if (_handlers.TryGetValue(entityId, out var handler))
            {
                handler.OnDeath(killerId, reason);
                handler.OnLifeStateChanged(oldState, LifeState.Dead);
            }

            // Raise death event
            var death = new DeathEvent
            {
                EntityId = entityId,
                KillerEntityId = killerId,
                DeathReason = reason,
                TimeOfDeath = lifeState.DeathTime
            };
            OnDeath?.Invoke(death);
            _events?.Publish(death);

            return true;
        }

        /// <summary>
        /// Resurrects the specified entity.
        /// </summary>
        public bool Resurrect(int entityId, int resurrectorId, string resurrectType)
        {
            if (!_lifeStates.TryGetValue(entityId, out var lifeState))
                return false;

            // Cannot resurrect if not dead
            if (lifeState.CurrentState != LifeState.Dead)
                return false;

            // Cannot resurrect if resurrection is disabled
            if (!lifeState.CanResurrect)
                return false;

            // Transition to alive state
            var oldState = lifeState.CurrentState;
            lifeState.CurrentState = LifeState.Alive;
            lifeState.DeathTime = -1f;
            _lifeStates[entityId] = lifeState;

            // Notify handler
            if (_handlers.TryGetValue(entityId, out var handler))
            {
                handler.OnResurrect(resurrectorId, resurrectType);
                handler.OnLifeStateChanged(oldState, LifeState.Alive);
            }

            // Raise resurrection event
            var resurrection = new ResurrectionEvent
            {
                EntityId = entityId,
                ResurrectorEntityId = resurrectorId,
                ResurrectionType = resurrectType,
                TimeOfResurrection = GetCurrentTime()
            };
            OnResurrection?.Invoke(resurrection);
            _events?.Publish(resurrection);

            return true;
        }

        /// <summary>
        /// Sets the entity as invulnerable for a specified duration.
        /// </summary>
        public void SetInvulnerable(int entityId, float duration)
        {
            if (!_lifeStates.TryGetValue(entityId, out var lifeState))
                return;

            var oldState = lifeState.CurrentState;
            lifeState.CurrentState = LifeState.Invulnerable;

            if (duration < 0)
            {
                // Permanent invulnerability
                lifeState.InvulnerabilityEndTime = float.MaxValue;
            }
            else
            {
                lifeState.InvulnerabilityEndTime = GetCurrentTime() + duration;
            }

            _lifeStates[entityId] = lifeState;

            // Notify handler if state changed
            if (oldState != LifeState.Invulnerable && _handlers.TryGetValue(entityId, out var handler))
            {
                handler.OnLifeStateChanged(oldState, LifeState.Invulnerable);
            }
        }

        /// <summary>
        /// Removes invulnerability from the entity.
        /// </summary>
        public void RemoveInvulnerability(int entityId)
        {
            if (!_lifeStates.TryGetValue(entityId, out var lifeState))
                return;

            if (lifeState.CurrentState != LifeState.Invulnerable)
                return;

            var oldState = lifeState.CurrentState;
            lifeState.CurrentState = LifeState.Alive;
            lifeState.InvulnerabilityEndTime = -1f;
            _lifeStates[entityId] = lifeState;

            // Notify handler
            if (_handlers.TryGetValue(entityId, out var handler))
            {
                handler.OnLifeStateChanged(oldState, LifeState.Alive);
            }
        }

        /// <summary>
        /// Checks whether the entity should die based on a death condition.
        /// </summary>
        public bool ShouldDie(int entityId, IDeathCondition condition)
        {
            if (!_handlers.TryGetValue(entityId, out var handler))
                return false;

            return condition.ShouldDie(handler);
        }

        /// <summary>
        /// Gets the current life state of the specified entity.
        /// </summary>
        public LifeState GetLifeState(int entityId)
        {
            if (_lifeStates.TryGetValue(entityId, out var lifeState))
                return lifeState.CurrentState;

            return LifeState.Dead; // Default to dead if not registered
        }

        /// <summary>
        /// Checks whether the entity is alive.
        /// </summary>
        public bool IsAlive(int entityId)
        {
            var state = GetLifeState(entityId);
            return state == LifeState.Alive || state == LifeState.Invulnerable;
        }

        /// <summary>
        /// Checks whether the entity is dead.
        /// </summary>
        public bool IsDead(int entityId)
        {
            return GetLifeState(entityId) == LifeState.Dead;
        }

        /// <summary>
        /// Checks whether the entity is invulnerable.
        /// </summary>
        public bool IsInvulnerable(int entityId)
        {
            return GetLifeState(entityId) == LifeState.Invulnerable;
        }

        /// <summary>
        /// Updates invulnerability timers.
        /// </summary>
        /// <param name="currentTime">Current game time.</param>
        /// <remarks>
        /// Call this method regularly (e.g., in game update loop) to expire temporary invulnerability.
        /// </remarks>
        public void UpdateInvulnerabilityTimers(float currentTime)
        {
            var expiredEntities = new List<int>();

            foreach (var kvp in _lifeStates)
            {
                var entityId = kvp.Key;
                var lifeState = kvp.Value;

                if (lifeState.CurrentState == LifeState.Invulnerable &&
                    lifeState.InvulnerabilityEndTime > 0 &&
                    lifeState.InvulnerabilityEndTime < float.MaxValue &&
                    currentTime >= lifeState.InvulnerabilityEndTime)
                {
                    expiredEntities.Add(entityId);
                }
            }

            foreach (var entityId in expiredEntities)
            {
                RemoveInvulnerability(entityId);
            }
        }

        /// <summary>
        /// Gets the current time.
        /// </summary>
        /// <remarks>
        /// Override this in derived classes to use game-specific time (e.g., Unity Time.time).
        /// Default implementation returns 0 (suitable for unit tests).
        /// </remarks>
        protected virtual float GetCurrentTime()
        {
            return 0f; // Framework doesn't have time dependency; game layer provides time
        }
    }
}
