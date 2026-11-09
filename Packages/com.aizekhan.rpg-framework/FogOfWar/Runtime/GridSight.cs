using System;
using System.Collections.Generic;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// Sight on a square grid without a fog: a straight line between two cells, whether anything blocks it (an archer's
    /// shot, a spell that needs a clear line), whether a cell is inside a sight shape, and what an eye sees.
    /// </summary>
    public static class GridSight
    {
        /// <summary>
        /// Whether a cell <paramref name="dx"/>, <paramref name="dy"/> away from the eye is inside the shape.
        /// </summary>
        public static bool InShape(int dx, int dy, int radius, SightShape shape)
        {
            if (radius < 0)
                return false;
            dx = Math.Abs(dx);
            dy = Math.Abs(dy);
            switch (shape)
            {
                case SightShape.Square: return dx <= radius && dy <= radius;
                case SightShape.Diamond: return dx + dy <= radius;
                default: return (long)dx * dx + (long)dy * dy <= (long)radius * radius + radius;
            }
        }

        /// <summary>
        /// The cells of the straight line from one cell to another (Bresenham), both ends included, added to
        /// <paramref name="into"/> (cleared first).
        /// </summary>
        public static void Line(GridPoint from, GridPoint to, List<GridPoint> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));
            into.Clear();
            Walk(from, to, null, into);
        }

        /// <summary>
        /// The cells of the straight line from one cell to another, both ends included.
        /// </summary>
        public static List<GridPoint> Line(GridPoint from, GridPoint to)
        {
            var line = new List<GridPoint>();
            Walk(from, to, null, line);
            return line;
        }

        /// <summary>
        /// True when no cell between the two ends blocks sight (the ends themselves may: an archer on a wall sees a
        /// target at a door). The same both ways: a line counts when it is clear from either end. Makes no garbage.
        /// </summary>
        public static bool HasLine(ISightBlockers blockers, GridPoint from, GridPoint to)
        {
            if (blockers == null)
                throw new ArgumentNullException(nameof(blockers));
            return Walk(from, to, blockers, null) || Walk(to, from, blockers, null);
        }

        /// <summary>
        /// True when the target is inside the shape and has a clear line (<see cref="HasLine"/>).
        /// </summary>
        public static bool CanSee(ISightBlockers blockers, GridPoint eye, GridPoint target, int radius,
            SightShape shape = SightShape.Circle) =>
            InShape(target.X - eye.X, target.Y - eye.Y, radius, shape) && HasLine(blockers, eye, target);

        /// <summary>
        /// The cells an eye sees on a grid (<see cref="FieldOfView"/>). For many eyes or every turn keep a
        /// <see cref="FieldOfView"/> and reuse it.
        /// </summary>
        public static List<GridPoint> Visible(ISightBlockers blockers, int width, int height, GridPoint eye, int radius,
            SightShape shape = SightShape.Circle)
        {
            var cells = new List<GridPoint>();
            new FieldOfView(width, height).Compute(blockers, eye, radius, shape, cells);
            return cells;
        }

        // Walks the line; with blockers stops at the first blocking cell between the ends (false)
        private static bool Walk(GridPoint from, GridPoint to, ISightBlockers blockers, List<GridPoint> into)
        {
            int x = from.X, y = from.Y;
            int dx = Math.Abs(to.X - x), dy = -Math.Abs(to.Y - y);
            int sx = x < to.X ? 1 : -1, sy = y < to.Y ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                into?.Add(new GridPoint(x, y));
                bool end = x == to.X && y == to.Y;
                if (blockers != null && !end && (x != from.X || y != from.Y) && blockers.BlocksSight(x, y))
                    return false;
                if (end)
                    return true;
                int twice = 2 * error;
                if (twice >= dy)
                {
                    error += dy;
                    x += sx;
                }
                if (twice <= dx)
                {
                    error += dx;
                    y += sy;
                }
            }
        }
    }
}
