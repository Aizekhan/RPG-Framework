namespace RPGFramework.Serialization
{
    /// <summary>
    /// Generic interface for objects that can be serialized to/from byte arrays.
    /// Framework contract - no Unity or game dependencies.
    /// </summary>
    public interface ISerializable
    {
        /// <summary>
        /// Serializes the object to a byte array.
        /// </summary>
        byte[] Serialize();

        /// <summary>
        /// Deserializes the object from a byte array.
        /// </summary>
        void Deserialize(byte[] data);
    }
}
