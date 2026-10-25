// Assets/_Framework/Combat/Runtime/DamageType.cs

namespace RPGFramework.Combat
{
    /// <summary>
    /// Generic damage type enumeration
    /// Games can extend this by casting to int and adding custom types (e.g., Fire = 100, Ice = 101)
    /// </summary>
    public enum DamageType
    {
        /// <summary>
        /// Physical damage (melee, ranged attacks)
        /// </summary>
        Physical = 0,

        /// <summary>
        /// Magical damage (spells, magical abilities)
        /// </summary>
        Magical = 1,

        /// <summary>
        /// Pure damage (true damage, ignores resistances)
        /// </summary>
        Pure = 2

        // Games can extend with custom types by casting to int:
        // Fire = 100,
        // Ice = 101,
        // Lightning = 102,
        // Poison = 103,
        // etc.
    }
}
