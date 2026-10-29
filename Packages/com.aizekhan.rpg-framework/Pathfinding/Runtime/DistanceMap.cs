using System;
using System.Collections.Generic;

namespace RPGFramework.Pathfinding
{
    /// <summary>
    /// How far every reachable place is from the nearest goal (a Dijkstra map / flow field): one search serves any
    /// number of chasers, each taking <see cref="NextStep"/> towards the goals, or <see cref="StepAway"/> to flee.
    /// Build it again when the goals move (the player's cell changed) or the graph changes. Goals can be several
    /// (every exit, every player); <paramref name="maxCost"/> keeps the search to a neighbourhood.
    /// </summary>
    public sealed class DistanceMap
    {
        private readonly INavigationGraph _graph;
        private readonly Dictionary<Position2D, float> _distance = new Dictionary<Position2D, float>();
        private readonly List<(Position2D Position, float Distance)> _heap = new List<(Position2D, float)>();

        public DistanceMap(INavigationGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// Places reached by the last build.
        /// </summary>
        public int Count => _distance.Count;

        public float MaxCost { get; private set; } = float.PositiveInfinity;

        /// <summary>
        /// Measures from the goals; walkable goals only. Places further than <paramref name="maxCost"/> stay unreached.
        /// </summary>
        public void Build(IEnumerable<Position2D> goals, float maxCost = float.PositiveInfinity)
        {
            if (goals == null)
                throw new ArgumentNullException(nameof(goals));
            _distance.Clear();
            _heap.Clear();
            MaxCost = maxCost;
            foreach (var goal in goals)
            {
                if (_graph.IsWalkable(goal) && !_distance.ContainsKey(goal))
                {
                    _distance[goal] = 0f;
                    Push(goal, 0f);
                }
            }

            while (_heap.Count > 0)
            {
                var (position, distance) = Pop();
                if (distance > _distance[position])
                    continue;
                foreach (var next in _graph.GetNeighbors(position))
                {
                    // Costs are the same both ways on the graphs the framework has; a one-way graph measures from here
                    float through = distance + _graph.GetCost(next, position);
                    if (through > maxCost)
                        continue;
                    if (_distance.TryGetValue(next, out float known) && known <= through)
                        continue;
                    _distance[next] = through;
                    Push(next, through);
                }
            }
        }

        public void Build(Position2D goal, float maxCost = float.PositiveInfinity) => Build(new[] { goal }, maxCost);

        /// <returns>The distance to the nearest goal; infinity when the place was not reached.</returns>
        public float DistanceTo(Position2D position) =>
            _distance.TryGetValue(position, out float distance) ? distance : float.PositiveInfinity;

        public bool IsReached(Position2D position) => _distance.ContainsKey(position);

        /// <summary>
        /// The neighbour that gets closest to a goal.
        /// </summary>
        /// <returns>False on a goal, or where no neighbour is closer (unreached, walled in).</returns>
        public bool NextStep(Position2D from, out Position2D next) => Best(from, towards: true, out next);

        /// <summary>
        /// The neighbour that gets furthest from the goals (fleeing; inside the measured area).
        /// </summary>
        /// <returns>False where no neighbour is further.</returns>
        public bool StepAway(Position2D from, out Position2D next) => Best(from, towards: false, out next);

        /// <summary>
        /// The whole way from a place to the nearest goal (the place itself first); empty when it was not reached.
        /// </summary>
        public List<Position2D> PathFrom(Position2D from)
        {
            var path = new List<Position2D>();
            if (!IsReached(from))
                return path;
            path.Add(from);
            var at = from;
            while (NextStep(at, out var next) && path.Count <= _distance.Count)
            {
                path.Add(next);
                at = next;
            }
            return path;
        }

        private bool Best(Position2D from, bool towards, out Position2D next)
        {
            next = from;
            float here = DistanceTo(from);
            if (towards && float.IsPositiveInfinity(here))
                return false;
            float best = here;
            bool found = false;
            foreach (var neighbour in _graph.GetNeighbors(from))
            {
                if (!_distance.TryGetValue(neighbour, out float distance))
                    continue;
                if (towards ? distance < best : distance > best)
                {
                    best = distance;
                    next = neighbour;
                    found = true;
                }
            }
            return found;
        }

        // A binary heap on the distance
        private void Push(Position2D position, float distance)
        {
            _heap.Add((position, distance));
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heap[parent].Distance <= _heap[i].Distance)
                    break;
                (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                i = parent;
            }
        }

        private (Position2D Position, float Distance) Pop()
        {
            var top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = i * 2 + 1, right = left + 1, smallest = i;
                if (left < _heap.Count && _heap[left].Distance < _heap[smallest].Distance)
                    smallest = left;
                if (right < _heap.Count && _heap[right].Distance < _heap[smallest].Distance)
                    smallest = right;
                if (smallest == i)
                    break;
                (_heap[smallest], _heap[i]) = (_heap[i], _heap[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
