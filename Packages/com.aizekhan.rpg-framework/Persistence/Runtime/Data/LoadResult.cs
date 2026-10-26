namespace RPGFramework.Persistence
{
    /// <summary>
    /// Result of a load operation.
    /// </summary>
    public class LoadResult
    {
        /// <summary>
        /// Gets whether the load operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the slot ID from which the save was loaded.
        /// </summary>
        public string SlotId { get; set; }

        /// <summary>
        /// Gets the loaded game state data.
        /// </summary>
        public object GameState { get; set; }

        /// <summary>
        /// Gets the metadata for the loaded save.
        /// </summary>
        public ISaveMetadata Metadata { get; set; }

        /// <summary>
        /// Gets the error type if the operation failed.
        /// </summary>
        public SaveError Error { get; set; }

        /// <summary>
        /// Gets a human-readable error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        public LoadResult(bool success, string slotId, SaveError error = SaveError.None, string errorMessage = "")
        {
            Success = success;
            SlotId = slotId;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public static LoadResult CreateSuccess(string slotId, object gameState, ISaveMetadata metadata)
        {
            return new LoadResult(true, slotId)
            {
                GameState = gameState,
                Metadata = metadata
            };
        }

        public static LoadResult CreateFailure(string slotId, SaveError error, string errorMessage)
        {
            return new LoadResult(false, slotId, error, errorMessage);
        }
    }
}
