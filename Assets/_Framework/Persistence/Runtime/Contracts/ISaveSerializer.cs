namespace RPGFramework.Persistence
{
    /// <summary>
    /// Handles serialization and deserialization of save data.
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// Serializes an object to byte array.
        /// </summary>
        byte[] Serialize<T>(T data);

        /// <summary>
        /// Deserializes a byte array to an object.
        /// </summary>
        T Deserialize<T>(byte[] data);

        /// <summary>
        /// Gets the serialization format used by this serializer.
        /// </summary>
        SerializationFormat Format { get; }
    }
}
