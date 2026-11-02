using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// A delayed hit of an ability came due (<see cref="IAbilityImpacts"/>): it landed (the effect was called) or
    /// missed, and why. <see cref="Timestamp"/> is the hit's own time, though the update came later.
    /// </summary>
    public struct AbilityImpactEvent : IEvent
    {
        public string AbilityId;
        public int CasterEntityId;
        public int TargetEntityId;

        /// <summary>
        /// The use it belongs to (<see cref="AbilityExecutedEvent.UseId"/>).
        /// </summary>
        public int UseId;

        /// <summary>
        /// Which hit, from 1, of <see cref="Hits"/>.
        /// </summary>
        public int Hit;

        public int Hits;
        public bool Landed;

        /// <summary>
        /// Why it missed (null when it landed).
        /// </summary>
        public string Reason;

        public float Timestamp;
    }
}
