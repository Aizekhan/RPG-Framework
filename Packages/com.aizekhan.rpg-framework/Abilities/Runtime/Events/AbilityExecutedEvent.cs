using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when an ability is executed.
    /// </summary>
    public struct AbilityExecutedEvent : IEvent
    {
        /// <summary>Ability ID that was executed</summary>
        public string AbilityId;

        /// <summary>Entity that cast the ability</summary>
        public int CasterEntityId;

        /// <summary>Target entity (-1 for no target)</summary>
        public int TargetEntityId;

        /// <summary>Timestamp of execution</summary>
        public float Timestamp;

        /// <summary>Whether execution succeeded</summary>
        public bool Success;

        /// <summary>The use (several hits share it); 0 for a failed attempt</summary>
        public int UseId;

        /// <summary>When its first hit lands: <see cref="Timestamp"/> for one that hits at once</summary>
        public float ImpactAt;

        /// <summary>Hits it makes (1 for most)</summary>
        public int Impacts;
    }
}
