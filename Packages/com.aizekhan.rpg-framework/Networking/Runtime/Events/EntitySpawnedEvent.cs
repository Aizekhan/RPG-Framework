using RPGFramework.Events;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network entity is spawned.
    /// Published by <see cref="ReplicationSystem.SpawnEntity"/> (when given an event publisher): the game creates the entity and registers it.
    /// </summary>
    public struct EntitySpawnedEvent : IEvent
    {
        public int NetworkId;
        public int OwnerId;
        public string PrefabId;
    }
}
