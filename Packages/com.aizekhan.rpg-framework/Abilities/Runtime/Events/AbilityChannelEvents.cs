using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// A channel with a tick interval ticked: the ability's effect was called again.
    /// </summary>
    public struct AbilityChannelTickEvent : IEvent
    {
        public string AbilityId;
        public int CasterEntityId;
        public int TargetEntityId;

        /// <summary>1 for the first tick after the channel started.</summary>
        public int Tick;

        public float Timestamp;
    }

    /// <summary>
    /// A channel ended: it ran its time (<see cref="Completed"/>), or was canceled or interrupted (with the reason).
    /// </summary>
    public struct AbilityChannelEndedEvent : IEvent
    {
        public string AbilityId;
        public int CasterEntityId;
        public int TargetEntityId;
        public bool Completed;

        /// <summary>Null when it ran its time.</summary>
        public string Reason;

        public int Ticks;
        public float Timestamp;
    }
}
