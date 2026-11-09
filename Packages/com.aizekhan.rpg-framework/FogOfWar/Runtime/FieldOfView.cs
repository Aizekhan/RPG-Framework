using System;
using System.Collections.Generic;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// The outline of what an eye can see before walls are counted.
    /// </summary>
    public enum SightShape
    {
        /// <summary>A round area (cells whose centre is within radius + 1/2): torches, units in strategy games.</summary>
        Circle,

        /// <summary>A square (8 steps around count as 1): classic roguelikes, where a diagonal step is one move.</summary>
        Square,

        /// <summary>A diamond (steps along the grid lines only): games that move in 4 directions.</summary>
        Diamond
    }

    /// <summary>
    /// What one eye sees on a grid: symmetric shadowcasting (the roguelike standard; A. Ford, 2020). If a floor cell
    /// sees another, that one sees it back; walls in view are seen (a room's walls light up), what stands behind them is
    /// not; no gaps or blind spots in open rooms or along pillars. It costs the cells in view, not the grid. One
    /// instance serves any number of eyes on one grid and makes no garbage once its list has grown.
    /// </summary>
    public sealed class FieldOfView
    {
        private readonly int[] _stamp;
        private int _pass;
        private ISightBlockers _blockers;
        private List<GridPoint> _into;
        private GridPoint _eye;
        private int _radius;
        private SightShape _shape;

        public FieldOfView(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
            Width = width;
            Height = height;
            _stamp = new int[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with every cell the eye sees within the radius, each once, the
        /// eye's own cell first when it is on the grid. A negative radius sees nothing; 0 sees only the eye's cell.
        /// </summary>
        /// <param name="throughWalls">Count no walls: the whole shape (a scrying spell, a scan, a watchtower's map).</param>
        public void Compute(ISightBlockers blockers, GridPoint eye, int radius, SightShape shape, List<GridPoint> into,
            bool throughWalls = false)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (radius < 0 || !InGrid(eye.X, eye.Y))
                return;
            if (++_pass == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _pass = 1;
            }
            _into = into;
            _eye = eye;
            _radius = radius;
            _shape = shape;
            _blockers = blockers ?? SightBlockers.None;
            Reveal(eye.X, eye.Y);
            if (throughWalls)
            {
                for (int y = Math.Max(0, eye.Y - radius); y <= Math.Min(Height - 1, eye.Y + radius); y++)
                    for (int x = Math.Max(0, eye.X - radius); x <= Math.Min(Width - 1, eye.X + radius); x++)
                        Reveal(x, y);
            }
            else
            {
                for (int quadrant = 0; quadrant < 4; quadrant++)
                    Scan(quadrant, 1, -1, 1, 1, 1);
            }
            _into = null;
            _blockers = null;
        }

        // One row of a quadrant at a depth, between two slopes kept as exact fractions (col / depth)
        private void Scan(int quadrant, int depth, long startNum, long startDen, long endNum, long endDen)
        {
            if (depth > _radius)
                return;
            long minCol = FloorDiv(2 * depth * startNum + startDen, 2 * startDen);
            long maxCol = -FloorDiv(-(2 * depth * endNum - endDen), 2 * endDen);
            bool hasPrevious = false, previousWall = false;
            for (long col = minCol; col <= maxCol; col++)
            {
                Transform(quadrant, depth, (int)col, out int x, out int y);
                bool wall = !InGrid(x, y) || _blockers.BlocksSight(x, y);
                bool symmetric = col * startDen >= depth * startNum && col * endDen <= depth * endNum;
                if (wall || symmetric)
                    Reveal(x, y);
                if (hasPrevious && previousWall && !wall)
                {
                    startNum = 2 * col - 1;
                    startDen = 2 * depth;
                }
                if (hasPrevious && !previousWall && wall)
                    Scan(quadrant, depth + 1, startNum, startDen, 2 * col - 1, 2 * depth);
                hasPrevious = true;
                previousWall = wall;
            }
            if (hasPrevious && !previousWall)
                Scan(quadrant, depth + 1, startNum, startDen, endNum, endDen);
        }

        private void Transform(int quadrant, int depth, int col, out int x, out int y)
        {
            switch (quadrant)
            {
                case 0: x = _eye.X + col; y = _eye.Y - depth; break;
                case 1: x = _eye.X + depth; y = _eye.Y + col; break;
                case 2: x = _eye.X + col; y = _eye.Y + depth; break;
                default: x = _eye.X - depth; y = _eye.Y + col; break;
            }
        }

        private void Reveal(int x, int y)
        {
            if (!InGrid(x, y) || !GridSight.InShape(x - _eye.X, y - _eye.Y, _radius, _shape))
                return;
            int index = y * Width + x;
            if (_stamp[index] == _pass)
                return;
            _stamp[index] = _pass;
            _into.Add(new GridPoint(x, y));
        }

        private bool InGrid(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
