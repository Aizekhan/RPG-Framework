using System;
using System.IO;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Serializer with versioning and type identification support.
    /// Uses ITypeRegistry for stable type IDs instead of Type.FullName.
    /// </summary>
    public class VersionedSerializer
    {
        private readonly ITypeRegistry _typeRegistry;
        private readonly IMigrationRegistry _migrationRegistry;

        public VersionedSerializer(ITypeRegistry typeRegistry, IMigrationRegistry migrationRegistry = null)
        {
            _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
            _migrationRegistry = migrationRegistry;
        }

        /// <summary>
        /// Serializes an object with version and type metadata.
        /// Wire format: [TypeId (string)] [Version (ushort)] [PayloadLength (int32)] [Payload (byte[])]
        /// </summary>
        public byte[] Serialize<T>(T data) where T : ISerializable
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var type = typeof(T);
            var typeId = _typeRegistry.GetTypeId(type);
            var version = _typeRegistry.GetVersion(type);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write type metadata
                writer.Write(typeId);
                writer.Write(version);

                // Serialize payload
                var payload = data.Serialize();
                writer.Write(payload.Length);
                writer.Write(payload);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes an object, performing migration if necessary.
        /// </summary>
        public T Deserialize<T>(byte[] data) where T : ISerializable
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // Read type metadata
                var typeId = reader.ReadString();
                var version = reader.ReadUInt16();

                // Read payload
                var payloadLength = reader.ReadInt32();
                var payload = reader.ReadBytes(payloadLength);

                // Get expected type info
                var expectedType = typeof(T);
                var expectedTypeId = _typeRegistry.GetTypeId(expectedType);
                var expectedVersion = _typeRegistry.GetVersion(expectedType);

                // Type ID mismatch check
                if (typeId != expectedTypeId)
                {
                    throw new InvalidOperationException(
                        $"Type mismatch: expected '{expectedTypeId}' but got '{typeId}'");
                }

                // Check if migration is needed
                if (version != expectedVersion)
                {
                    if (version > expectedVersion)
                    {
                        throw new InvalidOperationException(
                            $"Cannot deserialize newer version {version} (current version: {expectedVersion}). Update your application.");
                    }

                    // Perform migration
                    payload = MigrateData(typeId, payload, version, expectedVersion);
                }

                // Deserialize
                var result = Activator.CreateInstance<T>();
                result.Deserialize(payload);
                return result;
            }
        }

        /// <summary>
        /// Deserializes without knowing the exact type at compile time.
        /// </summary>
        public object DeserializePolymorphic(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // Read type metadata
                var typeId = reader.ReadString();
                var version = reader.ReadUInt16();

                // Read payload
                var payloadLength = reader.ReadInt32();
                var payload = reader.ReadBytes(payloadLength);

                // Get registered type
                var type = _typeRegistry.GetType(typeId, version);

                if (!typeof(ISerializable).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"Type {type.FullName} does not implement ISerializable");
                }

                // Deserialize
                var result = (ISerializable)Activator.CreateInstance(type);
                result.Deserialize(payload);
                return result;
            }
        }

        private byte[] MigrateData(string typeId, byte[] data, ushort fromVersion, ushort toVersion)
        {
            if (_migrationRegistry == null)
            {
                throw new InvalidOperationException(
                    $"Cannot migrate {typeId} from version {fromVersion} to {toVersion}: no migration registry provided");
            }

            var currentData = data;
            for (ushort v = fromVersion; v < toVersion; v++)
            {
                var nextVersion = (ushort)(v + 1);
                currentData = _migrationRegistry.Migrate(typeId, currentData, v, nextVersion);
            }

            return currentData;
        }
    }
}
