namespace RPGFramework.Persistence
{
    /// <summary>
    /// Event raised when a save file is created.
    /// </summary>
    public class SaveCreatedEvent
    {
        public string SlotId { get; }
        public ISaveMetadata Metadata { get; }

        public SaveCreatedEvent(string slotId, ISaveMetadata metadata)
        {
            SlotId = slotId;
            Metadata = metadata;
        }
    }
}
