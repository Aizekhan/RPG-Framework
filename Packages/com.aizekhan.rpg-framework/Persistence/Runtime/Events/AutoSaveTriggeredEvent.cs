namespace RPGFramework.Persistence
{
    /// <summary>
    /// Event raised when an auto-save is triggered.
    /// </summary>
    public class AutoSaveTriggeredEvent
    {
        public string SlotId { get; }
        public float TimeSinceLastSave { get; }

        public AutoSaveTriggeredEvent(string slotId, float timeSinceLastSave)
        {
            SlotId = slotId;
            TimeSinceLastSave = timeSinceLastSave;
        }
    }
}
