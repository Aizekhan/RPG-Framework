using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// What a team knows of a cell.
    /// </summary>
    public enum FogState
    {
        /// <summary>Never seen: drawn black, nothing on it is shown.</summary>
        Unexplored,

        /// <summary>Seen before, not now: the ground is remembered (drawn dim), what moves on it is not shown.</summary>
        Explored,

        /// <summary>In sight of the team's eyes now.</summary>
        Visible
    }

    /// <summary>
    /// How an eye sees: radius in cells, shape, and whether walls stop it.
    /// </summary>
    public readonly struct FogSight
    {
        public FogSight(int radius, SightShape shape = SightShape.Circle, bool throughWalls = false,
            ISightBlockers blockers = null)
        {
            Radius = radius;
            Shape = shape;
            ThroughWalls = throughWalls;
            Blockers = blockers;
        }

        public int Radius { get; }
        public SightShape Shape { get; }

        /// <summary>Walls do not stop it (a scan, a flare, a scrying spell, a flying scout).</summary>
        public bool ThroughWalls { get; }

        /// <summary>
        /// This eye's own blockers instead of the map's (null: the map's): an eye on high ground over a map where only
        /// higher cliffs block, a hero with true sight through illusory walls.
        /// </summary>
        public ISightBlockers Blockers { get; }
    }

    /// <summary>
    /// A team explored cells it never saw before (by sight or by <see cref="FogOfWarMap.Explore"/>).
    /// </summary>
    public readonly struct FogExploredEvent : IEvent
    {
        public FogExploredEvent(int team, int newCells, int exploredCells, int totalCells)
        {
            Team = team;
            NewCells = newCells;
            ExploredCells = exploredCells;
            TotalCells = totalCells;
        }

        public int Team { get; }

        /// <summary>Cells explored by this change.</summary>
        public int NewCells { get; }

        /// <summary>Cells the team has explored in all (its own, not what allies share).</summary>
        public int ExploredCells { get; }

        public int TotalCells { get; }
    }

    /// <summary>
    /// What a save keeps of a fog: the cells each team explored. Eyes are not kept: the game adds its units again on
    /// load, and what they see becomes visible. A co-op host can send it to a guest in a game message.
    /// </summary>
    public sealed class FogOfWarRecord
    {
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>One entry per team: its explored cells as a bit per cell (row by row), in base 64.</summary>
        public List<string> Explored { get; set; } = new List<string>();
    }

    /// <summary>
    /// Fog of war on a square grid, as strategy games, tactics games and roguelikes have it: per team, each cell is
    /// <see cref="FogState.Unexplored"/>, <see cref="FogState.Explored"/> or <see cref="FogState.Visible"/>.
    /// <list type="bullet">
    /// <item>Eyes (units, torches, towers, a scan) are added per team with a sight (<see cref="FogSight"/>) and moved
    /// when they change cell; a cell stays visible while any eye of the team sees it, so a move costs only that eye's
    /// cells, not the map.</item>
    /// <item>Walls come from <see cref="ISightBlockers"/> (symmetric shadowcasting, <see cref="FieldOfView"/>); a door
    /// that opens is <see cref="BlockersChanged(GridPoint)"/>.</item>
    /// <item>Allies share sight with <see cref="ShareVision"/>; a map scroll is <see cref="Explore"/>.</item>
    /// <item>A view draws it from <see cref="WriteStates"/> when <see cref="ViewVersion"/> changed (in Unity:
    /// <c>FogOfWarTexture</c>); the game hides what stands on a cell its team does not see (<see cref="IsVisible"/>).</item>
    /// <item>Saved by <see cref="FogOfWarSave"/> (<see cref="Capture"/> / <see cref="Restore"/>).</item>
    /// </list>
    /// A 3D world maps its ground onto the grid with <see cref="FogGridSpace"/>. Plain C#, makes no garbage when eyes
    /// move once their lists have grown. Not thread-safe.
    /// </summary>
    public sealed class FogOfWarMap
    {
        public const int MaxTeams = 32;

        private readonly ISightBlockers _blockers;
        private readonly IEventPublisher _events;
        private readonly TeamFog[] _teams;
        private readonly Dictionary<int, Eye> _eyes = new Dictionary<int, Eye>();
        private readonly List<Eye> _eyeList = new List<Eye>();
        private readonly FieldOfView _fieldOfView;
        private int _nextId = 1;
        private long _sharingVersion;

        /// <param name="blockers">What stops sight; null: nothing does (open ground).</param>
        /// <param name="teams">Teams with a fog of their own (1..32): players, factions, the monsters' side.</param>
        public FogOfWarMap(int width, int height, ISightBlockers blockers = null, int teams = 1,
            IEventPublisher events = null)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
            if (teams < 1 || teams > MaxTeams)
                throw new ArgumentOutOfRangeException(nameof(teams), $"Teams must be 1..{MaxTeams}");
            Width = width;
            Height = height;
            _blockers = blockers ?? SightBlockers.None;
            _events = events;
            _fieldOfView = new FieldOfView(width, height);
            _teams = new TeamFog[teams];
            for (int i = 0; i < teams; i++)
                _teams[i] = new TeamFog(width * height);
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public int TeamCount => _teams.Length;
        public ISightBlockers Blockers => _blockers;
        public int EyeCount => _eyes.Count;

        /// <summary>
        /// Raised with the bus event, after the change.
        /// </summary>
        public event Action<FogExploredEvent> Explored;

        public bool Contains(GridPoint cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

        /// <summary>
        /// Adds an eye of a team; what it sees becomes visible at once.
        /// </summary>
        /// <returns>Its id, for <see cref="MoveEye"/>, <see cref="SetSight"/> and <see cref="RemoveEye"/>.</returns>
        public int AddEye(int team, GridPoint at, FogSight sight)
        {
            CheckTeam(team);
            var eye = new Eye(_nextId++, team, at, sight);
            _eyes.Add(eye.Id, eye);
            _eyeList.Add(eye);
            Look(eye);
            Announce();
            return eye.Id;
        }

        public int AddEye(int team, GridPoint at, int radius, SightShape shape = SightShape.Circle) =>
            AddEye(team, at, new FogSight(radius, shape));

        /// <summary>
        /// Moves an eye; nothing is done when it stays in its cell, so a game may call it every frame.
        /// </summary>
        /// <returns>False for an unknown id.</returns>
        public bool MoveEye(int id, GridPoint to)
        {
            if (!_eyes.TryGetValue(id, out var eye))
                return false;
            if (eye.At == to)
                return true;
            Forget(eye);
            eye.At = to;
            Look(eye);
            Announce();
            return true;
        }

        /// <summary>
        /// Changes how an eye sees (a torch burns down, a unit levels up, a blinding spell).
        /// </summary>
        /// <returns>False for an unknown id.</returns>
        public bool SetSight(int id, FogSight sight)
        {
            if (!_eyes.TryGetValue(id, out var eye))
                return false;
            Forget(eye);
            eye.Sight = sight;
            Look(eye);
            Announce();
            return true;
        }

        /// <summary>
        /// Removes an eye (the unit died, the flare went out); cells only it saw become explored.
        /// </summary>
        /// <returns>False for an unknown id.</returns>
        public bool RemoveEye(int id)
        {
            if (!_eyes.TryGetValue(id, out var eye))
                return false;
            Forget(eye);
            _eyes.Remove(id);
            _eyeList.Remove(eye);
            return true;
        }

        /// <summary>
        /// Removes every eye (a new level with the same map); what was explored stays.
        /// </summary>
        public void RemoveAllEyes()
        {
            foreach (var eye in _eyeList)
                Forget(eye);
            _eyes.Clear();
            _eyeList.Clear();
        }

        public bool HasEye(int id) => _eyes.ContainsKey(id);

        public GridPoint PositionOf(int id) => Get(id).At;

        public int TeamOf(int id) => Get(id).Team;

        public FogSight SightOf(int id) => Get(id).Sight;

        /// <summary>
        /// The cells one eye sees now: whom a sentry spotted, what a tower covers.
        /// </summary>
        public IReadOnlyList<GridPoint> SeenBy(int id) => Get(id).Cells;

        /// <summary>
        /// A cell began or stopped blocking sight (a door, a destroyed wall, smoke): the eyes in reach look again.
        /// Change the blockers first.
        /// </summary>
        public void BlockersChanged(GridPoint cell)
        {
            foreach (var eye in _eyeList)
            {
                if (eye.Sight.ThroughWalls)
                    continue;
                int reach = Math.Max(0, eye.Sight.Radius);
                if (Math.Abs(cell.X - eye.At.X) <= reach && Math.Abs(cell.Y - eye.At.Y) <= reach)
                {
                    Forget(eye);
                    Look(eye);
                }
            }
            Announce();
        }

        /// <summary>
        /// Many cells changed (a level's doors reset): every eye looks again.
        /// </summary>
        public void BlockersChanged()
        {
            foreach (var eye in _eyeList)
            {
                Forget(eye);
                Look(eye);
            }
            Announce();
        }

        /// <summary>
        /// Marks an area explored without seeing it now (a map, a scroll of magic mapping, a scout's report); walls do
        /// not stop it.
        /// </summary>
        /// <returns>The cells explored by it.</returns>
        public int Explore(int team, GridPoint center, int radius, SightShape shape = SightShape.Circle)
        {
            CheckTeam(team);
            var fog = _teams[team];
            int before = fog.NewCells;
            for (int y = Math.Max(0, center.Y - radius); y <= Math.Min(Height - 1, center.Y + radius); y++)
                for (int x = Math.Max(0, center.X - radius); x <= Math.Min(Width - 1, center.X + radius); x++)
                    if (GridSight.InShape(x - center.X, y - center.Y, radius, shape))
                        MarkExplored(fog, y * Width + x);
            int explored = fog.NewCells - before;
            if (explored > 0)
                fog.Version++;
            Announce();
            return explored;
        }

        /// <summary>
        /// The whole map is explored for a team (a cheat, the end of a level, a spectator).
        /// </summary>
        public int ExploreAll(int team)
        {
            CheckTeam(team);
            var fog = _teams[team];
            int before = fog.NewCells;
            for (int i = 0; i < CellCount; i++)
                MarkExplored(fog, i);
            int explored = fog.NewCells - before;
            if (explored > 0)
                fog.Version++;
            Announce();
            return explored;
        }

        /// <summary>
        /// The team forgets what it explored but does not see now (an amnesia curse, a shifting maze).
        /// </summary>
        public void ForgetExplored(int team)
        {
            CheckTeam(team);
            var fog = _teams[team];
            fog.ExploredCount = 0;
            for (int i = 0; i < CellCount; i++)
            {
                fog.Explored[i] = fog.SeenBy[i] > 0;
                if (fog.Explored[i])
                    fog.ExploredCount++;
            }
            fog.Version++;
        }

        /// <summary>
        /// Team <paramref name="to"/> sees what team <paramref name="from"/> sees and has explored (allies, a shared
        /// map). One way: share both ways for a full alliance. <see cref="ExploredCount"/> stays each team's own.
        /// </summary>
        public void ShareVision(int from, int to, bool share = true)
        {
            CheckTeam(from);
            CheckTeam(to);
            if (from == to)
                return;
            uint bit = 1u << from;
            uint before = _teams[to].SharedFrom;
            _teams[to].SharedFrom = share ? before | bit : before & ~bit;
            if (_teams[to].SharedFrom != before)
                _sharingVersion++;
        }

        public bool SharesVision(int from, int to)
        {
            CheckTeam(from);
            CheckTeam(to);
            return from == to || (_teams[to].SharedFrom & (1u << from)) != 0;
        }

        /// <summary>
        /// What a team knows of a cell, with the sight its allies share; outside the grid: unexplored.
        /// </summary>
        public FogState StateOf(int team, GridPoint cell)
        {
            CheckTeam(team);
            if (!Contains(cell))
                return FogState.Unexplored;
            return StateAt(team, cell.Y * Width + cell.X);
        }

        /// <summary>
        /// Whether a team sees a cell now: show the enemy standing there, let the team target it.
        /// </summary>
        public bool IsVisible(int team, GridPoint cell) => StateOf(team, cell) == FogState.Visible;

        /// <summary>
        /// Whether a team saw a cell at some time (it may see it now).
        /// </summary>
        public bool IsExplored(int team, GridPoint cell) => StateOf(team, cell) != FogState.Unexplored;

        /// <summary>
        /// Cells the team explored itself (achievements, "explored 80 %").
        /// </summary>
        public int ExploredCount(int team)
        {
            CheckTeam(team);
            return _teams[team].ExploredCount;
        }

        public float ExploredFraction(int team) => (float)ExploredCount(team) / CellCount;

        /// <summary>
        /// Grows whenever anything a team's view shows changed (its own fog, an ally's it shares, a share given or
        /// taken): redraw when it differs from the last drawn.
        /// </summary>
        public long ViewVersion(int team)
        {
            CheckTeam(team);
            long version = _sharingVersion + _teams[team].Version;
            uint shared = _teams[team].SharedFrom;
            for (int t = 0; shared != 0; t++, shared >>= 1)
                if ((shared & 1) != 0)
                    version += _teams[t].Version;
            return version;
        }

        /// <summary>
        /// One byte per cell, row by row (index y * Width + x), from a team's view: for a fog texture or a minimap.
        /// </summary>
        public void WriteStates(int team, byte[] into, byte unexplored = 255, byte explored = 128, byte visible = 0)
        {
            CheckTeam(team);
            if (into == null)
                throw new ArgumentNullException(nameof(into));
            if (into.Length < CellCount)
                throw new ArgumentException($"Needs {CellCount} bytes", nameof(into));
            for (int i = 0; i < CellCount; i++)
            {
                switch (StateAt(team, i))
                {
                    case FogState.Visible: into[i] = visible; break;
                    case FogState.Explored: into[i] = explored; break;
                    default: into[i] = unexplored; break;
                }
            }
        }

        public FogOfWarRecord Capture()
        {
            var record = new FogOfWarRecord { Width = Width, Height = Height };
            var bytes = new byte[(CellCount + 7) / 8];
            foreach (var fog in _teams)
            {
                Array.Clear(bytes, 0, bytes.Length);
                for (int i = 0; i < CellCount; i++)
                    if (fog.Explored[i])
                        bytes[i >> 3] |= (byte)(1 << (i & 7));
                record.Explored.Add(Convert.ToBase64String(bytes));
            }
            return record;
        }

        /// <summary>
        /// Puts back what each team explored; what the eyes see now stays visible (and explored). Teams the record has
        /// no entry for keep theirs.
        /// </summary>
        /// <returns>False, changing nothing, when the record is of another map size or unreadable.</returns>
        public bool Restore(FogOfWarRecord record)
        {
            if (record == null || record.Width != Width || record.Height != Height || record.Explored == null)
                return false;
            int teams = Math.Min(record.Explored.Count, TeamCount);
            var decoded = new byte[teams][];
            for (int t = 0; t < teams; t++)
            {
                try
                {
                    decoded[t] = Convert.FromBase64String(record.Explored[t] ?? "");
                }
                catch (FormatException)
                {
                    return false;
                }
                if (decoded[t].Length != (CellCount + 7) / 8)
                    return false;
            }
            for (int t = 0; t < teams; t++)
            {
                var fog = _teams[t];
                fog.ExploredCount = 0;
                for (int i = 0; i < CellCount; i++)
                {
                    fog.Explored[i] = (decoded[t][i >> 3] & (1 << (i & 7))) != 0 || fog.SeenBy[i] > 0;
                    if (fog.Explored[i])
                        fog.ExploredCount++;
                }
                fog.Version++;
            }
            return true;
        }

        private FogState StateAt(int team, int index)
        {
            uint teams = _teams[team].SharedFrom | (1u << team);
            bool explored = false;
            for (int t = 0; teams != 0; t++, teams >>= 1)
            {
                if ((teams & 1) == 0)
                    continue;
                if (_teams[t].SeenBy[index] > 0)
                    return FogState.Visible;
                explored |= _teams[t].Explored[index];
            }
            return explored ? FogState.Explored : FogState.Unexplored;
        }

        private void Look(Eye eye)
        {
            var sight = eye.Sight;
            _fieldOfView.Compute(sight.Blockers ?? _blockers, eye.At, sight.Radius, sight.Shape, eye.Cells,
                sight.ThroughWalls);
            var fog = _teams[eye.Team];
            foreach (var cell in eye.Cells)
            {
                int index = cell.Y * Width + cell.X;
                fog.SeenBy[index]++;
                MarkExplored(fog, index);
            }
            fog.Version++;
        }

        private void Forget(Eye eye)
        {
            var fog = _teams[eye.Team];
            foreach (var cell in eye.Cells)
                fog.SeenBy[cell.Y * Width + cell.X]--;
            eye.Cells.Clear();
            fog.Version++;
        }

        private static void MarkExplored(TeamFog fog, int index)
        {
            if (fog.Explored[index])
                return;
            fog.Explored[index] = true;
            fog.ExploredCount++;
            fog.NewCells++;
        }

        // Publishes what each team explored since the last call
        private void Announce()
        {
            for (int t = 0; t < _teams.Length; t++)
            {
                var fog = _teams[t];
                if (fog.NewCells == 0)
                    continue;
                var explored = new FogExploredEvent(t, fog.NewCells, fog.ExploredCount, CellCount);
                fog.NewCells = 0;
                _events?.Publish(explored);
                Explored?.Invoke(explored);
            }
        }

        private Eye Get(int id) =>
            _eyes.TryGetValue(id, out var eye) ? eye : throw new KeyNotFoundException($"No eye {id} on the fog");

        private void CheckTeam(int team)
        {
            if (team < 0 || team >= _teams.Length)
                throw new ArgumentOutOfRangeException(nameof(team), $"Team {team} is not 0..{_teams.Length - 1}");
        }

        private sealed class TeamFog
        {
            public readonly int[] SeenBy;
            public readonly bool[] Explored;
            public int ExploredCount;
            public int NewCells;
            public long Version;
            public uint SharedFrom;

            public TeamFog(int cells)
            {
                SeenBy = new int[cells];
                Explored = new bool[cells];
            }
        }

        private sealed class Eye
        {
            public readonly int Id;
            public readonly int Team;
            public readonly List<GridPoint> Cells = new List<GridPoint>();
            public GridPoint At;
            public FogSight Sight;

            public Eye(int id, int team, GridPoint at, FogSight sight)
            {
                Id = id;
                Team = team;
                At = at;
                Sight = sight;
            }
        }
    }
}
