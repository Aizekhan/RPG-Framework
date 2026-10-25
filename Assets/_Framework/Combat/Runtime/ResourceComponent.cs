using System;
using System.IO;
using RPGFramework.ECS;
using RPGFramework.Serialization;

namespace RPGFramework.Combat
{
    /// <summary>
    /// Generic resource pool component for RPG systems (HP, MP, Stamina, etc.).
    /// Provides storage for current/max values and regeneration tracking.
    /// </summary>
    /// <typeparam name="TResourceType">Resource identifier type (enum or struct)</typeparam>
    [SerializableType("RPGFramework.Combat.ResourceComponent")]
    public struct ResourceComponent<TResourceType> : IComponent, ISerializable
        where TResourceType : struct, IConvertible
    {
        /// <summary>Resource type identifier</summary>
        public TResourceType ResourceType;

        /// <summary>Current resource value</summary>
        public float Current;

        /// <summary>Maximum resource capacity</summary>
        public float Max;

        /// <summary>Resource regeneration rate per second (0 = no regeneration)</summary>
        public float RegenRate;

        /// <summary>Last time regeneration occurred (game time in seconds)</summary>
        public float LastRegenTime;

        /// <summary>
        /// Serialize component to byte array using Framework serialization foundation.
        /// </summary>
        /// <returns>Serialized byte array</returns>
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write version for migration support (EPIC 06 pattern)
                writer.Write((ushort)1);

                // Resource type as int (safe for enums and int-sized structs)
                writer.Write(ResourceType.ToInt32(null));

                // Resource values
                writer.Write(Current);
                writer.Write(Max);
                writer.Write(RegenRate);
                writer.Write(LastRegenTime);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserialize component from byte array using Framework serialization foundation.
        /// </summary>
        /// <param name="data">Serialized byte array</param>
        public void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Cannot deserialize ResourceComponent from null or empty data", nameof(data));
            }

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                ushort version = reader.ReadUInt16();

                if (version == 1)
                {
                    int resourceTypeInt = reader.ReadInt32();
                    ResourceType = (TResourceType)Enum.ToObject(typeof(TResourceType), resourceTypeInt);

                    Current = reader.ReadSingle();
                    Max = reader.ReadSingle();
                    RegenRate = reader.ReadSingle();
                    LastRegenTime = reader.ReadSingle();
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported ResourceComponent version: {version}. Expected version 1.");
                }
            }
        }
    }
}
