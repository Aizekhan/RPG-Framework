namespace RPGFramework.Combat
{
    /// <summary>
    /// Generic status effect types for RPG games.
    /// Games can extend with custom types starting from 1000.
    /// </summary>
    public enum StatusEffectType
    {
        // Positive effects (0-99)

        /// <summary>Generic positive stat boost</summary>
        Buff = 0,

        /// <summary>Heal over time (HOT)</summary>
        HealOverTime = 1,

        /// <summary>Absorb damage shield</summary>
        Shield = 2,

        /// <summary>Increase movement speed</summary>
        Haste = 3,

        /// <summary>Temporary invulnerability</summary>
        Invulnerable = 4,

        // Negative effects (100-199)

        /// <summary>Generic negative stat reduction</summary>
        Debuff = 100,

        /// <summary>Damage over time (DOT)</summary>
        DamageOverTime = 101,

        /// <summary>Prevent all actions</summary>
        Stun = 102,

        /// <summary>Reduce movement/attack speed</summary>
        Slow = 103,

        /// <summary>Reduce armor/resistances</summary>
        Vulnerable = 104,

        /// <summary>Prevent healing</summary>
        HealingReduction = 105,

        /// <summary>Silence (prevent ability usage)</summary>
        Silence = 106,

        /// <summary>Blind (reduce accuracy/vision)</summary>
        Blind = 107,

        /// <summary>Fear (loss of control)</summary>
        Fear = 108,

        /// <summary>Root (prevent movement)</summary>
        Root = 109,

        // Neutral/Special effects (200-299)

        /// <summary>Transform/polymorph effect</summary>
        Transform = 200,

        /// <summary>Channeling/casting effect</summary>
        Channeling = 201,

        /// <summary>Stealth/invisibility</summary>
        Stealth = 202,

        /// <summary>Mark/tracking effect</summary>
        Mark = 203,

        // Custom game-specific range (1000+)

        /// <summary>Starting point for custom effect types</summary>
        Custom = 1000
    }
}
