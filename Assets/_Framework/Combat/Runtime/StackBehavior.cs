namespace RPGFramework.Combat
{
    /// <summary>
    /// Defines how multiple instances of the same status effect stack.
    /// </summary>
    public enum StackBehavior
    {
        /// <summary>
        /// Allow multiple independent instances of the same effect.
        /// Common for DOT/HOT effects - each application is tracked separately.
        /// Example: Multiple poison effects can exist simultaneously.
        /// </summary>
        Stack = 0,

        /// <summary>
        /// Replace existing effect with new application.
        /// Common for buffs - newer application overwrites old.
        /// Example: Strength buff gets replaced by new cast.
        /// </summary>
        Replace = 1,

        /// <summary>
        /// Keep existing effect but refresh its duration to maximum.
        /// Common for buffs where duration extends but power stays same.
        /// Example: Shield buff duration resets to full on reapplication.
        /// </summary>
        Refresh = 2,

        /// <summary>
        /// Ignore new application if effect already exists.
        /// Common for unique debuffs or crowd control.
        /// Example: Stun cannot be reapplied while active.
        /// </summary>
        Ignore = 3,

        /// <summary>
        /// Increment stack counter, sharing duration across stacks.
        /// Common for effects that scale with number of applications.
        /// Example: Bleed stacks increase damage per tick.
        /// </summary>
        StackCount = 4
    }
}
