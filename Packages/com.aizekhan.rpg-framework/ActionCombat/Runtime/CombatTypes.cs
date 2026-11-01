using System;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// What a combatant is doing. An attack goes Idle → (Charging) → WindUp → Active → Recovery → Idle; a dodge,
    /// a held block and a hit reaction are states of their own.
    /// </summary>
    public enum CombatState
    {
        Idle = 0,

        /// <summary>
        /// Holding a chargeable attack (a heavy swing, a drawn bow) until <see cref="CombatRules.Release"/>.
        /// </summary>
        Charging = 1,

        /// <summary>
        /// The attack is coming; it cannot hit yet.
        /// </summary>
        WindUp = 2,

        /// <summary>
        /// The attack's hit window: only now does a contact land (for a projectile, the window opens here and lasts
        /// its flight).
        /// </summary>
        Active = 3,

        /// <summary>
        /// The attack is over and the combatant is recovering; a dodge cancels it.
        /// </summary>
        Recovery = 4,

        /// <summary>
        /// Dodging; invulnerable for the dodge's invulnerability window.
        /// </summary>
        Dodging = 5,

        /// <summary>
        /// The block is held (and nothing else is going on).
        /// </summary>
        Blocking = 6,

        /// <summary>
        /// Staggered by a hit.
        /// </summary>
        HitReact = 7
    }

    /// <summary>
    /// How a contact between a hitbox and a body ended.
    /// </summary>
    public enum ContactResult
    {
        /// <summary>
        /// Nothing happened (<see cref="ContactIgnored"/> says why).
        /// </summary>
        Ignored = 0,

        Hit = 1,
        Blocked = 2,
        Dodged = 3
    }

    /// <summary>
    /// Why a contact was ignored.
    /// </summary>
    public enum ContactIgnored
    {
        None = 0,

        /// <summary>
        /// The swing is unknown, ended, or is not this attacker's.
        /// </summary>
        UnknownSwing = 1,

        /// <summary>
        /// The swing exists but its active window is not open (too early or too late).
        /// </summary>
        OutsideActiveWindow = 2,

        /// <summary>
        /// This swing already met this target (a swing lands once per target).
        /// </summary>
        AlreadyHit = 3,

        /// <summary>
        /// The attacker touched itself.
        /// </summary>
        Self = 4,

        /// <summary>
        /// The damage rules say the attacker may not hit the target (a friend, someone dead).
        /// </summary>
        NotHostile = 5
    }

    /// <summary>
    /// A point or direction in the world, without the engine's types (x, y, z; y is up).
    /// </summary>
    public readonly struct CombatVector : IEquatable<CombatVector>
    {
        public CombatVector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static CombatVector Zero => default;

        /// <summary>
        /// The same vector on the ground (y = 0).
        /// </summary>
        public CombatVector Flat => new CombatVector(X, 0f, Z);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static CombatVector operator -(CombatVector a, CombatVector b) => new CombatVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static CombatVector operator +(CombatVector a, CombatVector b) => new CombatVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static float Dot(CombatVector a, CombatVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>
        /// The angle between two directions in degrees (0 when either is zero).
        /// </summary>
        public static float Angle(CombatVector a, CombatVector b)
        {
            float lengths = a.Length * b.Length;
            if (lengths < 1e-6f)
                return 0f;
            double cos = Math.Max(-1.0, Math.Min(1.0, Dot(a, b) / lengths));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }

        public bool Equals(CombatVector other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is CombatVector other && Equals(other);

        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();

        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
    }
}
