using RPGFramework.Events;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when an entity's state is replicated.
    /// Published by <see cref="ReplicationSystem.ApplyReplication"/> (when given an event publisher) after a registered entity took a state or delta.
    /// </summary>
    public struct StateReplicatedEvent : IEvent
    {
        public int NetworkId;
        public bool IsDelta;
        public int DataSize;
    }
}
