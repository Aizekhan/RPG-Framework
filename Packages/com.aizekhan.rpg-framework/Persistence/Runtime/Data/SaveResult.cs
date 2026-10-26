namespace RPGFramework.Persistence
{
    /// <summary>
    /// Result of a save operation.
    /// </summary>
    public class SaveResult
    {
        /// <summary>
        /// Gets whether the save operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the slot ID where the save was written.
        /// </summary>
        public string SlotId { get; set; }

        /// <summary>
        /// Gets the error type if the operation failed.
        /// </summary>
        public SaveError Error { get; set; }

        /// <summary>
        /// Gets a human-readable error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets the metadata for the saved file.
        /// </summary>
        public ISaveMetadata Metadata { get; set; }

        public SaveResult(bool success, string slotId, SaveError error = SaveError.None, string errorMessage = "")
        {
            Success = success;
            SlotId = slotId;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public static SaveResult CreateSuccess(string slotId, ISaveMetadata metadata)
        {
            return new SaveResult(true, slotId)
            {
                Metadata = metadata
            };
        }

        public static SaveResult CreateFailure(string slotId, SaveError error, string errorMessage)
        {
            return new SaveResult(false, slotId, error, errorMessage);
        }
    }
}
