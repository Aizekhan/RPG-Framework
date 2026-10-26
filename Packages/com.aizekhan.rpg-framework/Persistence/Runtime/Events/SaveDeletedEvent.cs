namespace RPGFramework.Persistence
{
    /// <summary>
    /// Event raised when a save file is deleted.
    /// </summary>
    public class SaveDeletedEvent
    {
        public string SlotId { get; }

        public SaveDeletedEvent(string slotId)
        {
            SlotId = slotId;
        }
    }
}
