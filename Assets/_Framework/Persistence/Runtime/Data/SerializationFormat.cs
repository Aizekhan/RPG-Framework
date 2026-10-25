namespace RPGFramework.Persistence
{
    /// <summary>
    /// Defines the serialization format for save files.
    /// </summary>
    public enum SerializationFormat
    {
        /// <summary>
        /// JSON text format (human-readable, larger file size).
        /// </summary>
        Json,

        /// <summary>
        /// Binary format (compact, faster).
        /// </summary>
        Binary
    }
}
