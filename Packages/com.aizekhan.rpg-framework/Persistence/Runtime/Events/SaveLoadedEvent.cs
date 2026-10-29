using RPGFramework.Events;
namespace RPGFramework.Persistence
{
    /// <summary>
    /// Event raised when a save file is loaded.
    /// </summary>
    public class SaveLoadedEvent : IEvent
    {
        public string SlotId { get; }
        public ISaveMetadata Metadata { get; }

        public SaveLoadedEvent(string slotId, ISaveMetadata metadata)
        {
            SlotId = slotId;
            Metadata = metadata;
        }
    }
}
