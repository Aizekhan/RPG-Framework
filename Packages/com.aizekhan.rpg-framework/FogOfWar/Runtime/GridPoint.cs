using System;

namespace RPGFramework.FogOfWar
{
    /// <summary>
    /// A cell of a square grid: column <see cref="X"/>, row <see cref="Y"/>.
    /// </summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(GridPoint left, GridPoint right) => left.Equals(right);

        public static bool operator !=(GridPoint left, GridPoint right) => !left.Equals(right);

        public override string ToString() => $"({X}, {Y})";
    }
}
