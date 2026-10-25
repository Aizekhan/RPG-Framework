using System;
using System.Collections.Generic;

namespace RPGFramework.Pathfinding
{
    /// <summary>
    /// A* pathfinding algorithm implementation.
    /// Framework runtime with no engine or game dependencies.
    /// </summary>
    public class AStarPathfinder : IPathfinder
    {
        private readonly INavigationGraph _graph;
        private readonly Func<Position2D, Position2D, float> _heuristic;

        /// <summary>
        /// Create A* pathfinder with navigation graph and heuristic function.
        /// </summary>
        /// <param name="graph">Navigation graph representing walkable space</param>
        /// <param name="heuristic">Heuristic function for distance estimation (null = Euclidean distance)</param>
        public AStarPathfinder(INavigationGraph graph, Func<Position2D, Position2D, float> heuristic = null)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _heuristic = heuristic ?? ((a, b) => a.EuclideanDistance(b));
        }

        public IReadOnlyList<Position2D> FindPath(Position2D start, Position2D goal)
        {
            if (!_graph.IsWalkable(start) || !_graph.IsWalkable(goal))
            {
                return Array.Empty<Position2D>();
            }

            if (start.Equals(goal))
            {
                return new[] { start };
            }

            var openSet = new PriorityQueue<Position2D, float>();
            var cameFrom = new Dictionary<Position2D, Position2D>();
            var gScore = new Dictionary<Position2D, float>();
            var fScore = new Dictionary<Position2D, float>();

            gScore[start] = 0f;
            fScore[start] = _heuristic(start, goal);
            openSet.Enqueue(start, fScore[start]);

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();

                if (current.Equals(goal))
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var neighbor in _graph.GetNeighbors(current))
                {
                    if (!_graph.IsWalkable(neighbor))
                    {
                        continue;
                    }

                    float tentativeGScore = gScore[current] + _graph.GetCost(current, neighbor);

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + _heuristic(neighbor, goal);

                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }

            return Array.Empty<Position2D>();
        }

        public bool HasPath(Position2D start, Position2D goal)
        {
            var path = FindPath(start, goal);
            return path.Count > 0;
        }

        private List<Position2D> ReconstructPath(Dictionary<Position2D, Position2D> cameFrom, Position2D current)
        {
            var path = new List<Position2D> { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }

            return path;
        }

        /// <summary>
        /// Simple priority queue implementation for A*.
        /// Uses sorted list for simplicity (not optimal for large graphs).
        /// </summary>
        private class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
        {
            private readonly SortedList<TPriority, Queue<TElement>> _items = new SortedList<TPriority, Queue<TElement>>();
            private int _count;

            public int Count => _count;

            public void Enqueue(TElement element, TPriority priority)
            {
                if (!_items.ContainsKey(priority))
                {
                    _items[priority] = new Queue<TElement>();
                }

                _items[priority].Enqueue(element);
                _count++;
            }

            public TElement Dequeue()
            {
                if (_count == 0)
                {
                    throw new InvalidOperationException("Priority queue is empty");
                }

                var firstKey = _items.Keys[0];
                var queue = _items[firstKey];
                var item = queue.Dequeue();

                if (queue.Count == 0)
                {
                    _items.Remove(firstKey);
                }

                _count--;
                return item;
            }
        }
    }
}
