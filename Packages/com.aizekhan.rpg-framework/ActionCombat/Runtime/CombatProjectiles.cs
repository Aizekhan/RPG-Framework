using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.ActionCombat
{
    /// <summary>
    /// How a projectile finds whom it strikes.
    /// </summary>
    public enum ProjectileHitMode
    {
        /// <summary>
        /// It comes down where its flight ends (an arrow loosed in an arc, a thrown bomb) and strikes everyone within
        /// its radius there; nothing on the way.
        /// </summary>
        Landing = 0,

        /// <summary>
        /// It strikes the first one its path passes within its radius (a bolt, a thrown knife) and stops, or flies on
        /// through up to <see cref="ProjectileLaunch.Pierce"/> more; it falls when its time runs out.
        /// </summary>
        Contact = 1
    }

    /// <summary>
    /// What to launch (<see cref="CombatProjectiles.Launch"/>).
    /// </summary>
    public sealed class ProjectileLaunch
    {
        public int ShooterId { get; set; } = -1;

        /// <summary>
        /// Whom it was aimed at (-1: none); it may strike someone else.
        /// </summary>
        public int TargetId { get; set; } = -1;

        public CombatVector From { get; set; }

        /// <summary>
        /// Metres per second (<see cref="CombatProjectiles.Lead"/> aims it where the target will be).
        /// </summary>
        public CombatVector Velocity { get; set; }

        /// <summary>
        /// How close to a body it strikes, on the ground plane.
        /// </summary>
        public float Radius { get; set; } = 0.5f;

        /// <summary>
        /// Seconds it flies: where it comes down (<see cref="ProjectileHitMode.Landing"/>) or the longest it flies.
        /// </summary>
        public float Seconds { get; set; } = 1f;

        public ProjectileHitMode Mode { get; set; } = ProjectileHitMode.Landing;

        /// <summary>
        /// With <see cref="ProjectileHitMode.Contact"/>: how many more it flies through after the first.
        /// </summary>
        public int Pierce { get; set; }

        /// <summary>
        /// It strikes no one when its shooter is dead by then (an arrow whose archer fell drops harmless).
        /// </summary>
        public bool VoidIfShooterDies { get; set; }

        /// <summary>
        /// The shooter's swing in <see cref="CombatRules"/> (an attack with <see cref="AttackDefinition.Projectile"/>):
        /// each strike goes through <see cref="CombatRules.ReportContact"/>, so dodges, blocks and damage apply, and
        /// the swing ends when it lands. 0: the strikes are only reported (<see cref="ProjectileLandedEvent"/>).
        /// </summary>
        public int SwingId { get; set; }

        /// <summary>
        /// It only flies, for a view: a co-op friend's copy of the host's projectile, whose strikes the host decides.
        /// </summary>
        public bool Mirror { get; set; }

        /// <summary>
        /// Its id, for a mirror of another machine's projectile; 0: a new one.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The game's own kind (an arrow, a bolt) for views.
        /// </summary>
        public string Kind { get; set; }
    }

    /// <summary>
    /// A projectile in flight (<see cref="CombatProjectiles.InFlight"/>), for views and co-op.
    /// </summary>
    public sealed class FlyingProjectile
    {
        internal FlyingProjectile(int id, ProjectileLaunch launch, float time)
        {
            Id = id;
            ShooterId = launch.ShooterId;
            TargetId = launch.TargetId;
            Position = launch.From;
            Velocity = launch.Velocity;
            Radius = Math.Max(0f, launch.Radius);
            TimeLeft = Math.Max(0f, launch.Seconds);
            LaunchedAt = time;
            Mode = launch.Mode;
            PierceLeft = Math.Max(0, launch.Pierce);
            VoidIfShooterDies = launch.VoidIfShooterDies;
            SwingId = launch.SwingId;
            Mirror = launch.Mirror;
            Kind = launch.Kind;
        }

        public int Id { get; }
        public int ShooterId { get; }
        public int TargetId { get; }
        public CombatVector Position { get; internal set; }
        public CombatVector Velocity { get; }
        public float Radius { get; }
        public float TimeLeft { get; internal set; }
        public float LaunchedAt { get; }
        public ProjectileHitMode Mode { get; }
        public bool VoidIfShooterDies { get; }
        public int SwingId { get; }
        public bool Mirror { get; }
        public string Kind { get; }

        /// <summary>
        /// Where it comes down (or falls) if nothing stops it.
        /// </summary>
        public CombatVector EndsAt => new CombatVector(Position.X + Velocity.X * TimeLeft, Position.Y + Velocity.Y * TimeLeft,
            Position.Z + Velocity.Z * TimeLeft);

        internal int PierceLeft;
        internal float EndedAt;
        internal readonly List<int> Struck = new List<int>();
    }

    /// <summary>
    /// Projectiles in the rules, without physics (a game with no colliders: a runner's road, a server, a simulation):
    /// each flies straight at its velocity, ticked with the fight, and strikes the bodies of
    /// <see cref="IEnemyWorld.TargetsOf"/> its shooter on the ground plane, where it comes down or along its path
    /// (<see cref="ProjectileHitMode"/>). <see cref="Lead"/> aims at where a moving target will be. A strike is
    /// reported (<see cref="ProjectileStruckEvent"/>, then <see cref="ProjectileLandedEvent"/> with everyone it
    /// struck) and, with a swing, goes through <see cref="CombatRules.ReportContact"/>. The Unity
    /// <c>CombatProjectile</c> is the physics one.
    /// </summary>
    public sealed class CombatProjectiles
    {
        private readonly IEnemyWorld _world;
        private readonly IEventPublisher _events;
        private readonly CombatRules _rules;
        private readonly List<FlyingProjectile> _flying = new List<FlyingProjectile>();
        private readonly List<FlyingProjectile> _ended = new List<FlyingProjectile>();
        private readonly List<FlyingProjectile> _ticking = new List<FlyingProjectile>();
        private readonly List<int> _targets = new List<int>();
        private int _lastId;

        /// <param name="world">Who a shooter may strike and where everyone is.</param>
        /// <param name="events">Launches, strikes and landings are published here; null: none.</param>
        /// <param name="rules">For projectiles with a swing: their strikes go through the rules' contacts.</param>
        public CombatProjectiles(IEnemyWorld world, IEventPublisher events = null, CombatRules rules = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _events = events;
            _rules = rules;
        }

        /// <summary>
        /// Seconds of flight so far (the sum of the ticks).
        /// </summary>
        public float Time { get; private set; }

        public IReadOnlyList<FlyingProjectile> InFlight => _flying;

        public FlyingProjectile Find(int id)
        {
            foreach (var projectile in _flying)
            {
                if (projectile.Id == id)
                    return projectile;
            }
            return null;
        }

        /// <summary>
        /// Launches a projectile.
        /// </summary>
        /// <returns>Its id.</returns>
        public int Launch(ProjectileLaunch launch)
        {
            if (launch == null)
                throw new ArgumentNullException(nameof(launch));
            if (float.IsNaN(launch.Seconds) || launch.Seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(launch), launch.Seconds, "A flight lasts 0 seconds or more");
            int id = launch.Id > 0 ? launch.Id : ++_lastId;
            if (launch.Id > 0)
            {
                Remove(launch.Id);
                _lastId = Math.Max(_lastId, launch.Id);
            }
            var projectile = new FlyingProjectile(id, launch, Time);
            _flying.Add(projectile);
            _events?.Publish(new ProjectileLaunchedEvent(id, projectile.ShooterId, projectile.TargetId, projectile.Position,
                projectile.Velocity, projectile.TimeLeft, projectile.Mirror, Time));
            if (projectile.TimeLeft <= 0f)
                Tick(0f);
            return id;
        }

        /// <summary>
        /// Takes a projectile out without landing it (a mirror the host ended, a projectile caught by a shield).
        /// </summary>
        public bool Remove(int id)
        {
            var projectile = Find(id);
            if (projectile == null)
                return false;
            _flying.Remove(projectile);
            return true;
        }

        /// <summary>
        /// Takes out every projectile (a new run, a cleared arena).
        /// </summary>
        public void Clear() => _flying.Clear();

        /// <summary>
        /// Flies everything on by <paramref name="deltaSeconds"/> (after the bodies moved, with the rules' tick).
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Time moves forward");
            Time += deltaSeconds;
            if (_flying.Count == 0)
                return;

            // A snapshot: a strike's events may launch or remove projectiles
            _ended.Clear();
            _ticking.Clear();
            _ticking.AddRange(_flying);
            foreach (var projectile in _ticking)
            {
                if (!_flying.Contains(projectile))
                    continue;
                float step = Math.Min(deltaSeconds, projectile.TimeLeft);
                var from = projectile.Position;
                projectile.Position = Along(from, projectile.Velocity, step);
                projectile.TimeLeft -= step;
                projectile.EndedAt = Time - deltaSeconds + step;
                if (projectile.Mode == ProjectileHitMode.Contact && !projectile.Mirror && StrikeOnTheWay(projectile, from))
                    _ended.Add(projectile);
                else if (projectile.TimeLeft <= 0f)
                    _ended.Add(projectile);
            }
            _ticking.Clear();
            foreach (var projectile in _ended)
            {
                if (_flying.Remove(projectile))
                    Land(projectile);
            }
            _ended.Clear();
        }

        /// <summary>
        /// The velocity that meets a target moving at a steady velocity, at <paramref name="speed"/> metres per
        /// second (straight, the target's pace kept): aim it where the target will be.
        /// </summary>
        /// <returns>False when it cannot catch the target (then <paramref name="velocity"/> aims at where it is now).</returns>
        public static bool Lead(CombatVector from, CombatVector target, CombatVector targetVelocity, float speed,
            out CombatVector velocity, out float seconds)
        {
            var offset = target - from;
            speed = Math.Max(1e-4f, speed);
            // |offset + v t| = speed t  ->  (v.v - s^2) t^2 + 2 (offset.v) t + offset.offset = 0
            double a = CombatVector.Dot(targetVelocity, targetVelocity) - (double)speed * speed;
            double b = 2.0 * CombatVector.Dot(offset, targetVelocity);
            double c = CombatVector.Dot(offset, offset);
            double t = -1.0;
            if (Math.Abs(a) < 1e-9)
            {
                if (Math.Abs(b) > 1e-9)
                    t = -c / b;
            }
            else
            {
                double disc = b * b - 4.0 * a * c;
                if (disc >= 0.0)
                {
                    double root = Math.Sqrt(disc);
                    double t1 = (-b - root) / (2.0 * a);
                    double t2 = (-b + root) / (2.0 * a);
                    t = Smallest(t1, t2);
                }
            }

            if (t < 0.0 || double.IsNaN(t))
            {
                float distance = offset.Length;
                seconds = distance / speed;
                velocity = distance > 1e-6f ? Scale(offset, speed / distance) : CombatVector.Zero;
                return false;
            }
            seconds = (float)t;
            var meet = Along(target, targetVelocity, seconds);
            var way = meet - from;
            velocity = seconds > 1e-6f ? Scale(way, 1f / seconds) : CombatVector.Zero;
            return true;
        }

        private static double Smallest(double t1, double t2)
        {
            if (t1 >= 0.0 && t2 >= 0.0)
                return Math.Min(t1, t2);
            return t1 >= 0.0 ? t1 : t2;
        }

        // A contact projectile's path this tick passes a body: true when it stops
        private bool StrikeOnTheWay(FlyingProjectile projectile, CombatVector from)
        {
            if (projectile.VoidIfShooterDies && !_world.IsAlive(projectile.ShooterId))
                return false;
            _targets.Clear();
            _targets.AddRange(_world.TargetsOf(projectile.ShooterId));
            foreach (int target in _targets)
            {
                if (projectile.Struck.Contains(target) || !_world.IsAlive(target) ||
                    !_world.TryGetPose(target, out var where, out _))
                    continue;
                if (FlatDistanceToSegment(where, from, projectile.Position) > projectile.Radius)
                    continue;
                Strike(projectile, target, where);
                if (projectile.PierceLeft-- <= 0)
                    return true;
            }
            return false;
        }

        // Its flight is over: a landing one strikes everyone within its radius where it comes down
        private void Land(FlyingProjectile projectile)
        {
            bool voided = projectile.VoidIfShooterDies && !_world.IsAlive(projectile.ShooterId);
            if (projectile.Mode == ProjectileHitMode.Landing && !projectile.Mirror && !voided)
            {
                _targets.Clear();
                _targets.AddRange(_world.TargetsOf(projectile.ShooterId));
                foreach (int target in _targets)
                {
                    if (!_world.IsAlive(target) || !_world.TryGetPose(target, out var where, out _))
                        continue;
                    if ((where - projectile.Position).Flat.Length <= projectile.Radius)
                        Strike(projectile, target, where);
                }
            }
            if (projectile.SwingId != 0)
                _rules?.EndSwing(projectile.SwingId);
            var struck = projectile.Struck.Count == 0 ? Array.Empty<int>() : projectile.Struck.ToArray();
            _events?.Publish(new ProjectileLandedEvent(projectile.Id, projectile.ShooterId, projectile.TargetId,
                projectile.Position, struck, voided, projectile.Mirror, projectile.EndedAt));
        }

        private void Strike(FlyingProjectile projectile, int target, CombatVector where)
        {
            projectile.Struck.Add(target);
            var result = ContactResult.Hit;
            if (projectile.SwingId != 0 && _rules != null)
                result = _rules.ReportContact(projectile.ShooterId, target, projectile.SwingId, projectile.Position);
            _events?.Publish(new ProjectileStruckEvent(projectile.Id, projectile.ShooterId, target, projectile.Position,
                result, projectile.EndedAt));
        }

        private static float FlatDistanceToSegment(CombatVector point, CombatVector a, CombatVector b)
        {
            var ab = (b - a).Flat;
            var ap = (point - a).Flat;
            float lengthSquared = CombatVector.Dot(ab, ab);
            float t = lengthSquared < 1e-9f ? 0f : Math.Max(0f, Math.Min(1f, CombatVector.Dot(ap, ab) / lengthSquared));
            var closest = new CombatVector(ab.X * t, 0f, ab.Z * t);
            return (ap - closest).Length;
        }

        private static CombatVector Along(CombatVector from, CombatVector velocity, float seconds) =>
            new CombatVector(from.X + velocity.X * seconds, from.Y + velocity.Y * seconds, from.Z + velocity.Z * seconds);

        private static CombatVector Scale(CombatVector vector, float by) => new CombatVector(vector.X * by, vector.Y * by, vector.Z * by);
    }

    public readonly struct ProjectileLaunchedEvent : IEvent
    {
        public ProjectileLaunchedEvent(int projectileId, int shooterId, int targetId, CombatVector from, CombatVector velocity,
            float seconds, bool mirror, float time)
        {
            ProjectileId = projectileId;
            ShooterId = shooterId;
            TargetId = targetId;
            From = from;
            Velocity = velocity;
            Seconds = seconds;
            Mirror = mirror;
            Time = time;
        }

        public int ProjectileId { get; }
        public int ShooterId { get; }
        public int TargetId { get; }
        public CombatVector From { get; }
        public CombatVector Velocity { get; }

        /// <summary>
        /// Seconds it flies (where a landing one comes down).
        /// </summary>
        public float Seconds { get; }

        public bool Mirror { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A projectile struck a body (<see cref="Result"/>: through the rules with a swing; a hit without).
    /// </summary>
    public readonly struct ProjectileStruckEvent : IEvent
    {
        public ProjectileStruckEvent(int projectileId, int shooterId, int targetId, CombatVector point, ContactResult result, float time)
        {
            ProjectileId = projectileId;
            ShooterId = shooterId;
            TargetId = targetId;
            Point = point;
            Result = result;
            Time = time;
        }

        public int ProjectileId { get; }
        public int ShooterId { get; }
        public int TargetId { get; }
        public CombatVector Point { get; }
        public ContactResult Result { get; }
        public float Time { get; }
    }

    /// <summary>
    /// A projectile's flight is over: it came down, stopped in a body, or fell. <see cref="Struck"/> are the bodies
    /// it struck (empty: it missed; <see cref="Voided"/>: its shooter was dead and it struck no one).
    /// </summary>
    public readonly struct ProjectileLandedEvent : IEvent
    {
        public ProjectileLandedEvent(int projectileId, int shooterId, int targetId, CombatVector position,
            IReadOnlyList<int> struck, bool voided, bool mirror, float time)
        {
            ProjectileId = projectileId;
            ShooterId = shooterId;
            TargetId = targetId;
            Position = position;
            Struck = struck;
            Voided = voided;
            Mirror = mirror;
            Time = time;
        }

        public int ProjectileId { get; }
        public int ShooterId { get; }

        /// <summary>
        /// Whom it was aimed at (-1: none).
        /// </summary>
        public int TargetId { get; }

        public CombatVector Position { get; }
        public IReadOnlyList<int> Struck { get; }
        public bool Voided { get; }
        public bool Mirror { get; }
        public float Time { get; }

        public bool Landed => Struck != null && Struck.Count > 0;

        /// <summary>
        /// It missed the one it was aimed at (struck others or no one).
        /// </summary>
        public bool MissedItsTarget => TargetId >= 0 && (Struck == null || !Contains(TargetId));

        private bool Contains(int id)
        {
            foreach (int struck in Struck)
            {
                if (struck == id)
                    return true;
            }
            return false;
        }
    }
}
