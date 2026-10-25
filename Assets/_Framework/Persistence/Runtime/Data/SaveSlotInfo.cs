namespace RPGFramework.Persistence
{
    /// <summary>
    /// Concrete implementation of save slot information.
    /// </summary>
    public class SaveSlotInfo : ISaveSlot
    {
        public string SlotId { get; set; }
        public bool IsOccupied { get; set; }
        public ISaveMetadata Metadata { get; set; }
        public string FilePath { get; set; }

        public SaveSlotInfo(string slotId, string filePath)
        {
            SlotId = slotId;
            FilePath = filePath;
            IsOccupied = false;
            Metadata = null;
        }
    }
}
