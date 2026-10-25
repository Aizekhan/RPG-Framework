using System.IO;
using RPGFramework.ECS;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Base class for component serializers.
    /// Provides BinaryWriter/Reader infrastructure.
    /// </summary>
    public abstract class ComponentSerializerBase<T> : IComponentSerializer<T>
        where T : struct, IComponent
    {
        public byte[] Serialize(T component)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                SerializeInternal(writer, component);
                return stream.ToArray();
            }
        }

        public T Deserialize(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                return DeserializeInternal(reader);
            }
        }

        /// <summary>
        /// Override to implement component-specific serialization.
        /// </summary>
        protected abstract void SerializeInternal(BinaryWriter writer, T component);

        /// <summary>
        /// Override to implement component-specific deserialization.
        /// </summary>
        protected abstract T DeserializeInternal(BinaryReader reader);
    }
}
