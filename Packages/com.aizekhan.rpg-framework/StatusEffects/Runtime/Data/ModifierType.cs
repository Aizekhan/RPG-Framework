namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Types of modifiers that effects can apply.
    /// </summary>
    public enum ModifierType
    {
        /// <summary>
        /// Modifies a stat (e.g., "AttackPower", "Defense").
        /// </summary>
        Stat,

        /// <summary>
        /// Deals damage per tick.
        /// </summary>
        Damage,

        /// <summary>
        /// Heals per tick.
        /// </summary>
        Healing,

        /// <summary>
        /// Modifies a resource (e.g., Mana drain, Energy boost).
        /// </summary>
        Resource,

        /// <summary>
        /// Affects movement (e.g., Slow, Root, Speed boost).
        /// </summary>
        Movement,

        /// <summary>
        /// Control effects (Stun, Silence, Disarm).
        /// </summary>
        Control
    }
}
