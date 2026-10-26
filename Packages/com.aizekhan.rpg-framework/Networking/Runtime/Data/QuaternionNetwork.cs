using System;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Simple quaternion for network synchronization.
    /// Pure .NET struct without UnityEngine dependencies.
    /// </summary>
    public struct QuaternionNetwork : IEquatable<QuaternionNetwork>
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public QuaternionNetwork(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static QuaternionNetwork Identity => new QuaternionNetwork(0, 0, 0, 1);

        public bool Equals(QuaternionNetwork other)
        {
            return Math.Abs(X - other.X) < 0.0001f &&
                   Math.Abs(Y - other.Y) < 0.0001f &&
                   Math.Abs(Z - other.Z) < 0.0001f &&
                   Math.Abs(W - other.W) < 0.0001f;
        }

        public override bool Equals(object obj)
        {
            return obj is QuaternionNetwork other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                hash = hash * 31 + W.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(QuaternionNetwork a, QuaternionNetwork b) => a.Equals(b);
        public static bool operator !=(QuaternionNetwork a, QuaternionNetwork b) => !a.Equals(b);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2}, {W:F2})";
    }
}
