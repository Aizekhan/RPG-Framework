using System;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// A timed phase of an attack.
    /// </summary>
    public enum AttackPhase
    {
        WindUp = 0,
        Active = 1,
        Recovery = 2
    }

    /// <summary>
    /// How an attack moves its attacker during a phase, in the attacker's own space.
    /// </summary>
    public enum AttackMotionKind
    {
        /// <summary>
        /// Forward, towards what it faces (a lunge, a step into the blow).
        /// </summary>
        Approach = 0,

        /// <summary>
        /// Backward (a step out after the blow).
        /// </summary>
        Retreat = 1,

        StrafeLeft = 2,
        StrafeRight = 3,

        /// <summary>
        /// Forward in an arc <see cref="AttackMotion.Height"/> high (a leap attack). Landing on another floor is the
        /// game's physics.
        /// </summary>
        Leap = 4
    }

    /// <summary>
    /// One motion of an attack: during part of a phase the attacker moves <see cref="Distance"/> metres
    /// (<see cref="AttackDefinition.Motions"/>). <see cref="CombatRules.MotionOf"/> gives the speed now; the enemy
    /// director moves the body by it, a player controller may too. The speed follows the phase's length, so a faster
    /// attack (<see cref="CombatRules.SetAttackSpeed"/>) covers the same distance sooner.
    /// </summary>
    [Serializable]
    public struct AttackMotion
    {
        public AttackPhase Phase;
        public AttackMotionKind Kind;

        /// <summary>
        /// Metres covered over the motion's part of the phase.
        /// </summary>
        public float Distance;

        /// <summary>
        /// The top of a <see cref="AttackMotionKind.Leap"/>'s arc, metres.
        /// </summary>
        public float Height;

        /// <summary>
        /// Where in the phase it starts (0..1 of the phase).
        /// </summary>
        public float From;

        /// <summary>
        /// Where in the phase it ends (0..1); at or below <see cref="From"/>: the end of the phase.
        /// </summary>
        public float Until;

        public AttackMotion(AttackPhase phase, AttackMotionKind kind, float distance, float from = 0f, float until = 1f, float height = 0f)
        {
            Phase = phase;
            Kind = kind;
            Distance = distance;
            Height = height;
            From = from;
            Until = until;
        }

        internal float End => Until > From ? Math.Min(1f, Until) : 1f;

        /// <summary>
        /// What is wrong with it, or null.
        /// </summary>
        public string Problem()
        {
            if (!Finite(Distance) || Distance < 0f)
                return "Distance is a finite number, 0 or more";
            if (!Finite(Height) || Height < 0f)
                return "Height is a finite number, 0 or more";
            if (!Finite(From) || From < 0f || From >= 1f)
                return "From is a share of the phase, 0 up to 1";
            if (!Finite(Until))
                return "Until is a share of the phase";
            return null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Where an attack's motion takes its attacker right now, in the attacker's own space (<see cref="CombatRules.MotionOf"/>).
    /// </summary>
    public readonly struct AttackMotionSample
    {
        public AttackMotionSample(float forward, float side, float height)
        {
            Forward = forward;
            Side = side;
            Height = height;
        }

        /// <summary>
        /// Metres per second forward (below 0: backward).
        /// </summary>
        public float Forward { get; }

        /// <summary>
        /// Metres per second to the right (below 0: to the left).
        /// </summary>
        public float Side { get; }

        /// <summary>
        /// Metres above the ground on a leap's arc now.
        /// </summary>
        public float Height { get; }

        public bool IsMoving => Forward != 0f || Side != 0f || Height != 0f;

        /// <summary>
        /// The motion in the world for an attacker facing <paramref name="forward"/> (on the ground), metres per second.
        /// </summary>
        public CombatVector InWorld(CombatVector forward)
        {
            var flat = forward.Flat;
            float length = flat.Length;
            if (length < 1e-5f)
                return CombatVector.Zero;
            float fx = flat.X / length, fz = flat.Z / length;
            // The right of a forward on the ground (x right, z forward)
            return new CombatVector(fx * Forward + fz * Side, 0f, fz * Forward - fx * Side);
        }
    }

    /// <summary>
    /// A part of the volume an attack's weapon sweeps, in the attacker's space and in time: during
    /// [<see cref="From"/>, <see cref="Until"/>) of the active window, a slice of a ring around the attacker
    /// (<see cref="ArcFrom"/>..<see cref="ArcTo"/> degrees from its forward, positive to the right;
    /// <see cref="InnerRadius"/>..<see cref="Radius"/> metres; <see cref="HeightMin"/>..<see cref="HeightMax"/> above
    /// its feet). A wide swing is a few segments moving across; a thrust is one narrow one. Tested by
    /// <see cref="CombatRules.Sweep"/>, without physics.
    /// </summary>
    [Serializable]
    public struct SweepSegment
    {
        /// <summary>
        /// Where in the active window it starts (0..1 of the window).
        /// </summary>
        public float From;

        /// <summary>
        /// Where it ends (0..1); at or below <see cref="From"/>: the end of the window.
        /// </summary>
        public float Until;

        /// <summary>
        /// Degrees from the attacker's forward, positive to the right; <see cref="ArcFrom"/> up to <see cref="ArcTo"/>.
        /// </summary>
        public float ArcFrom;

        public float ArcTo;

        public float InnerRadius;
        public float Radius;

        /// <summary>
        /// Metres above the attacker's feet; both 0 = any height.
        /// </summary>
        public float HeightMin;

        public float HeightMax;

        public SweepSegment(float from, float until, float arcFrom, float arcTo, float radius, float innerRadius = 0f,
            float heightMin = 0f, float heightMax = 0f)
        {
            From = from;
            Until = until;
            ArcFrom = arcFrom;
            ArcTo = arcTo;
            InnerRadius = innerRadius;
            Radius = radius;
            HeightMin = heightMin;
            HeightMax = heightMax;
        }

        internal float End => Until > From ? Math.Min(1f, Until) : 1f;

        /// <summary>
        /// It is the one active at this share of the window.
        /// </summary>
        public bool ActiveAt(float share) => share >= From && share < End;

        /// <summary>
        /// A body at <paramref name="point"/> (a ball of <paramref name="radius"/>) is inside the segment of an
        /// attacker standing at <paramref name="attacker"/> and facing <paramref name="forward"/>.
        /// </summary>
        public bool Contains(CombatVector attacker, CombatVector forward, CombatVector point, float radius = 0f)
        {
            radius = Math.Max(0f, radius);
            var offset = (point - attacker).Flat;
            float distance = offset.Length;
            if (distance - radius > Radius || distance + radius < InnerRadius)
                return false;
            if (HeightMax > HeightMin)
            {
                float height = point.Y - attacker.Y;
                if (height + radius < HeightMin || height - radius > HeightMax)
                    return false;
            }
            float from = Math.Min(ArcFrom, ArcTo), to = Math.Max(ArcFrom, ArcTo);
            if (to - from >= 360f || distance < 1e-4f)
                return true;
            var facing = forward.Flat;
            if (facing.Length < 1e-5f)
                return true;
            // Signed angle to the point, positive to the right; widened by the body's size seen from there
            double angle = Math.Atan2(offset.X * facing.Z - offset.Z * facing.X, offset.X * facing.X + offset.Z * facing.Z) * 180.0 / Math.PI;
            double widen = radius >= distance ? 180.0 : Math.Asin(radius / distance) * 180.0 / Math.PI;
            return angle >= from - widen && angle <= to + widen;
        }

        /// <summary>
        /// What is wrong with it, or null.
        /// </summary>
        public string Problem()
        {
            if (!Finite(Radius) || Radius <= 0f)
                return "Radius is above 0";
            if (!Finite(InnerRadius) || InnerRadius < 0f || InnerRadius > Radius)
                return "Inner Radius is 0 up to Radius";
            if (!Finite(From) || From < 0f || From >= 1f)
                return "From is a share of the active window, 0 up to 1";
            if (!Finite(Until) || !Finite(ArcFrom) || !Finite(ArcTo) || !Finite(HeightMin) || !Finite(HeightMax))
                return "Every number is finite";
            return null;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
