using RPGFramework.Events;

namespace RPGFramework.Abilities
{
    /// <summary>
    /// Event raised when a toggle ability is switched on or off.
    /// </summary>
    public struct AbilityToggledEvent : IEvent
    {
        /// <summary>Toggle ability that changed</summary>
        public string AbilityId;

        /// <summary>Entity that owns the toggle</summary>
        public int CasterEntityId;

        /// <summary>True when switched on, false when switched off</summary>
        public bool IsOn;

        /// <summary>Timestamp of the switch</summary>
        public float Timestamp;

        /// <summary>Why it switched off by itself (the upkeep could not be paid, e.g. "Insufficient MP"); null: the caster's choice or on</summary>
        public string Reason;
    }
}
