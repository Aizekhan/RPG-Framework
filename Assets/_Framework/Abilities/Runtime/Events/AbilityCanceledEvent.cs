using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when an ability cast is canceled.
    /// </summary>
    public struct AbilityCanceledEvent : IEvent
    {
        /// <summary>Ability ID that was canceled</summary>
        public string AbilityId;

        /// <summary>Entity that was casting</summary>
        public int CasterEntityId;

        /// <summary>Timestamp of cancellation</summary>
        public float Timestamp;

        /// <summary>Reason for cancellation</summary>
        public string Reason;
    }
}
