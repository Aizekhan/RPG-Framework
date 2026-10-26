namespace RPGFramework.Serialization
{
    /// <summary>
    /// Type-safe serializer interface.
    /// Alternative to ISerializable for external serialization.
    /// </summary>
    public interface ISerializer<T>
    {
        /// <summary>
        /// Serializes a value to byte array.
        /// </summary>
        byte[] Serialize(T value);

        /// <summary>
        /// Deserializes a value from byte array.
        /// </summary>
        T Deserialize(byte[] data);
    }
}
