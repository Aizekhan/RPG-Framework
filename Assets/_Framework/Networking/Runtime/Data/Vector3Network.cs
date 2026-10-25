using System;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Simple 3D vector for network synchronization.
    /// Pure .NET struct without UnityEngine dependencies.
    /// </summary>
    public struct Vector3Network : IEquatable<Vector3Network>
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3Network(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3Network Zero => new Vector3Network(0, 0, 0);
        public static Vector3Network One => new Vector3Network(1, 1, 1);

        public bool Equals(Vector3Network other)
        {
            return Math.Abs(X - other.X) < 0.0001f &&
                   Math.Abs(Y - other.Y) < 0.0001f &&
                   Math.Abs(Z - other.Z) < 0.0001f;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3Network other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Vector3Network a, Vector3Network b) => a.Equals(b);
        public static bool operator !=(Vector3Network a, Vector3Network b) => !a.Equals(b);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
