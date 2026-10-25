using System;
using System.IO;

namespace RPGFramework.Networking
{
    /// <summary>
    /// Network-synchronized transform component.
    /// Synchronizes position, rotation, and velocity.
    /// </summary>
    public class NetworkTransform : NetworkEntity
    {
        public Vector3Network Position;
        public QuaternionNetwork Rotation;
        public Vector3Network Velocity;

        private Vector3Network _lastPosition;
        private QuaternionNetwork _lastRotation;
        private Vector3Network _lastVelocity;

        public float PositionThreshold = 0.01f;
        public float RotationThreshold = 0.01f;
        public float VelocityThreshold = 0.01f;

        public NetworkTransform(int networkId, int ownerId)
            : base(networkId, ownerId, ReplicationMode.Delta)
        {
            Position = Vector3Network.Zero;
            Rotation = QuaternionNetwork.Identity;
            Velocity = Vector3Network.Zero;
        }

        public override byte[] SerializeState()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Position
                writer.Write(Position.X);
                writer.Write(Position.Y);
                writer.Write(Position.Z);

                // Rotation
                writer.Write(Rotation.X);
                writer.Write(Rotation.Y);
                writer.Write(Rotation.Z);
                writer.Write(Rotation.W);

                // Velocity
                writer.Write(Velocity.X);
                writer.Write(Velocity.Y);
                writer.Write(Velocity.Z);

                return ms.ToArray();
            }
        }

        public override void DeserializeState(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Position
                Position = new Vector3Network(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle());

                // Rotation
                Rotation = new QuaternionNetwork(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle());

                // Velocity
                Velocity = new Vector3Network(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle());
            }
        }

        public override byte[] SerializeDelta()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                byte flags = 0;

                // Check what changed
                bool positionChanged = !PositionEquals(Position, _lastPosition);
                bool rotationChanged = !RotationEquals(Rotation, _lastRotation);
                bool velocityChanged = !VelocityEquals(Velocity, _lastVelocity);

                if (positionChanged) flags |= 1;
                if (rotationChanged) flags |= 2;
                if (velocityChanged) flags |= 4;

                if (flags == 0)
                    return null; // No changes

                writer.Write(flags);

                if (positionChanged)
                {
                    writer.Write(Position.X);
                    writer.Write(Position.Y);
                    writer.Write(Position.Z);
                    _lastPosition = Position;
                }

                if (rotationChanged)
                {
                    writer.Write(Rotation.X);
                    writer.Write(Rotation.Y);
                    writer.Write(Rotation.Z);
                    writer.Write(Rotation.W);
                    _lastRotation = Rotation;
                }

                if (velocityChanged)
                {
                    writer.Write(Velocity.X);
                    writer.Write(Velocity.Y);
                    writer.Write(Velocity.Z);
                    _lastVelocity = Velocity;
                }

                return ms.ToArray();
            }
        }

        public override void ApplyDelta(byte[] deltaData)
        {
            using (var ms = new MemoryStream(deltaData))
            using (var reader = new BinaryReader(ms))
            {
                byte flags = reader.ReadByte();

                if ((flags & 1) != 0)
                {
                    Position = new Vector3Network(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                if ((flags & 2) != 0)
                {
                    Rotation = new QuaternionNetwork(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                if ((flags & 4) != 0)
                {
                    Velocity = new Vector3Network(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }
            }
        }

        private bool PositionEquals(Vector3Network a, Vector3Network b)
        {
            return Math.Abs(a.X - b.X) < PositionThreshold &&
                   Math.Abs(a.Y - b.Y) < PositionThreshold &&
                   Math.Abs(a.Z - b.Z) < PositionThreshold;
        }

        private bool RotationEquals(QuaternionNetwork a, QuaternionNetwork b)
        {
            return Math.Abs(a.X - b.X) < RotationThreshold &&
                   Math.Abs(a.Y - b.Y) < RotationThreshold &&
                   Math.Abs(a.Z - b.Z) < RotationThreshold &&
                   Math.Abs(a.W - b.W) < RotationThreshold;
        }

        private bool VelocityEquals(Vector3Network a, Vector3Network b)
        {
            return Math.Abs(a.X - b.X) < VelocityThreshold &&
                   Math.Abs(a.Y - b.Y) < VelocityThreshold &&
                   Math.Abs(a.Z - b.Z) < VelocityThreshold;
        }
    }
}
