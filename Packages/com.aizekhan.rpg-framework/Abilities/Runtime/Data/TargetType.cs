namespace RPGFramework.Abilities
{
    /// <summary>
    /// Defines valid targets for abilities.
    /// Used for target validation and ability execution.
    /// </summary>
    public enum TargetType
    {
        /// <summary>No target required (self-cast)</summary>
        None = 0,

        /// <summary>Targets self only</summary>
        Self = 1,

        /// <summary>Targets single enemy</summary>
        Enemy = 2,

        /// <summary>Targets single ally</summary>
        Ally = 3,

        /// <summary>Targets area (AoE)</summary>
        Area = 4,

        /// <summary>Targets ground location</summary>
        Ground = 5,

        /// <summary>Targets any entity</summary>
        Any = 6,

        /// <summary>Hits every enemy of the caster; no target is picked</summary>
        AllEnemies = 7,

        /// <summary>Hits every ally of the caster, the caster included; no target is picked</summary>
        AllAllies = 8
    }
}
