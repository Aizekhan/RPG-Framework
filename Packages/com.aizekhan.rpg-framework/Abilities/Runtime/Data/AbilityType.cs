namespace RPGFramework.Abilities
{
    /// <summary>
    /// Types of abilities in the RPG system.
    /// Defines how and when abilities are activated.
    /// </summary>
    public enum AbilityType
    {
        /// <summary>Active ability requiring manual activation</summary>
        Active = 0,

        /// <summary>Passive ability always active</summary>
        Passive = 1,

        /// <summary>Triggered ability activated by conditions</summary>
        Triggered = 2,

        /// <summary>Toggle ability that can be turned on/off</summary>
        Toggle = 3,

        /// <summary>Channeled ability requiring continuous casting</summary>
        Channeled = 4
    }
}
