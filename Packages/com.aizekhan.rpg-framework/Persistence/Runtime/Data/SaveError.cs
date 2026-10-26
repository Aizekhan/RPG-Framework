namespace RPGFramework.Persistence
{
    /// <summary>
    /// Defines possible save/load error types.
    /// </summary>
    public enum SaveError
    {
        None,
        FileNotFound,
        CorruptedData,
        IncompatibleVersion,
        SerializationFailed,
        DeserializationFailed,
        InvalidChecksum,
        SlotNotFound,
        SlotOccupied,
        MaxSlotsReached,
        IOError,
        PermissionDenied,
        InsufficientSpace
    }
}
