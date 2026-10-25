using RPGFramework.ECS;

namespace RPGFramework.Serialization
{
    /// <summary>
    /// Registry for component serializers.
    /// Maps component types to their serializers.
    /// Framework contract - implementation is game-specific.
    /// </summary>
    public interface IComponentSerializerRegistry
    {
        /// <summary>
        /// Registers a serializer for a component type.
        /// </summary>
        void RegisterSerializer<T, TSerializer>(TSerializer serializer)
            where T : struct, IComponent
            where TSerializer : IComponentSerializer<T>;

        /// <summary>
        /// Gets a serializer for a component type.
        /// </summary>
        IComponentSerializer<T> GetSerializer<T>() where T : struct, IComponent;

        /// <summary>
        /// Serializes a component using its registered serializer.
        /// </summary>
        byte[] Serialize<T>(T component) where T : struct, IComponent;

        /// <summary>
        /// Deserializes a component using its registered serializer.
        /// </summary>
        T Deserialize<T>(byte[] data) where T : struct, IComponent;

        /// <summary>
        /// Checks if a serializer is registered for a component type.
        /// </summary>
        bool HasSerializer<T>() where T : struct, IComponent;
    }
}
