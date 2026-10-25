using RPGFramework.ECS;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Generic component serializer interface.
    /// Serializes ECS components to/from byte arrays.
    /// Framework contract - works with any IComponent.
    /// </summary>
    public interface IComponentSerializer<T> where T : struct, IComponent
    {
        /// <summary>
        /// Serializes a component to byte array.
        /// </summary>
        byte[] Serialize(T component);

        /// <summary>
        /// Deserializes a component from byte array.
        /// </summary>
        T Deserialize(byte[] data);
    }
}
