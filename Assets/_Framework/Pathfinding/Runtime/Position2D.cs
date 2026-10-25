using System;

namespace RPGFramework.Pathfinding
{
    /// <summary>
    /// Framework-neutral 2D position representation.
    /// Engine-agnostic alternative to UnityEngine.Vector2 or similar.
    /// </summary>
    public struct Position2D : IEquatable<Position2D>
    {
        public float X { get; }
        public float Y { get; }

        public Position2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Calculate Manhattan distance between two positions.
        /// Commonly used for grid-based pathfinding heuristics.
        /// </summary>
        public float ManhattanDistance(Position2D other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        /// <summary>
        /// Calculate Euclidean distance between two positions.
        /// Accurate straight-line distance.
        /// </summary>
        public float EuclideanDistance(Position2D other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculate squared Euclidean distance (avoids sqrt for performance).
        /// Useful for distance comparisons where exact value isn't needed.
        /// </summary>
        public float SquaredDistance(Position2D other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public bool Equals(Position2D other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is Position2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(Position2D left, Position2D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Position2D left, Position2D right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:F2}, {1:F2})", X, Y);
        }
    }
}
