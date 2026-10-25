using System;
using System.Collections.Generic;

namespace RPGFramework.Pathfinding
{
    /// <summary>
    /// Simple grid-based navigation graph.
    /// Useful for tile-based games, tactical RPGs, grid-based movement.
    /// </summary>
    public class GridNavigationGraph : INavigationGraph
    {
        private readonly int _width;
        private readonly int _height;
        private readonly bool[,] _walkable;
        private readonly bool _allowDiagonal;
        private readonly float _diagonalCost;

        /// <summary>
        /// Create a grid navigation graph.
        /// </summary>
        /// <param name="width">Grid width</param>
        /// <param name="height">Grid height</param>
        /// <param name="allowDiagonal">Allow diagonal movement</param>
        /// <param name="diagonalCost">Cost for diagonal movement (typically sqrt(2) ≈ 1.414)</param>
        public GridNavigationGraph(int width, int height, bool allowDiagonal = false, float diagonalCost = 1.414f)
        {
            if (width <= 0) throw new ArgumentException("Width must be positive", nameof(width));
            if (height <= 0) throw new ArgumentException("Height must be positive", nameof(height));

            _width = width;
            _height = height;
            _allowDiagonal = allowDiagonal;
            _diagonalCost = diagonalCost;
            _walkable = new bool[width, height];

            // Initialize all cells as walkable
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _walkable[x, y] = true;
                }
            }
        }

        /// <summary>
        /// Set whether a cell is walkable.
        /// </summary>
        public void SetWalkable(int x, int y, bool walkable)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                throw new ArgumentOutOfRangeException($"Position ({x}, {y}) is outside grid bounds");
            }

            _walkable[x, y] = walkable;
        }

        public bool IsWalkable(Position2D position)
        {
            int x = (int)Math.Round(position.X);
            int y = (int)Math.Round(position.Y);

            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return false;
            }

            return _walkable[x, y];
        }

        public IReadOnlyList<Position2D> GetNeighbors(Position2D position)
        {
            int x = (int)Math.Round(position.X);
            int y = (int)Math.Round(position.Y);

            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return Array.Empty<Position2D>();
            }

            var neighbors = new List<Position2D>();

            // Cardinal directions (up, down, left, right)
            AddNeighborIfValid(neighbors, x, y - 1); // Up
            AddNeighborIfValid(neighbors, x, y + 1); // Down
            AddNeighborIfValid(neighbors, x - 1, y); // Left
            AddNeighborIfValid(neighbors, x + 1, y); // Right

            if (_allowDiagonal)
            {
                // Diagonal directions
                AddNeighborIfValid(neighbors, x - 1, y - 1); // Up-Left
                AddNeighborIfValid(neighbors, x + 1, y - 1); // Up-Right
                AddNeighborIfValid(neighbors, x - 1, y + 1); // Down-Left
                AddNeighborIfValid(neighbors, x + 1, y + 1); // Down-Right
            }

            return neighbors;
        }

        public float GetCost(Position2D from, Position2D to)
        {
            // Check if diagonal movement
            float dx = Math.Abs(to.X - from.X);
            float dy = Math.Abs(to.Y - from.Y);

            if (dx > 0.1f && dy > 0.1f)
            {
                return _diagonalCost;
            }

            return 1.0f;
        }

        private void AddNeighborIfValid(List<Position2D> neighbors, int x, int y)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height && _walkable[x, y])
            {
                neighbors.Add(new Position2D(x, y));
            }
        }
    }
}
