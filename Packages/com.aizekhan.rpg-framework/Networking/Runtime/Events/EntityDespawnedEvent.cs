using RPGFramework.Events;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network entity is despawned.
    /// Published by <see cref="ReplicationSystem.DespawnEntity"/> (when given an event publisher) after the entity is unregistered.
    /// </summary>
    public struct EntityDespawnedEvent : IEvent
    {
        public int NetworkId;
    }
}
