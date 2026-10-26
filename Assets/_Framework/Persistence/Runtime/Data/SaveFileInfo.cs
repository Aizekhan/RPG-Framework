namespace RPGFramework.Persistence
{
    /// <summary>
    /// Sidecar record written next to each save (<c>slot_0.info</c> beside <c>slot_0.sav</c>): the metadata shown in
    /// load menus without reading the save itself, and the checksum of the save data. Always stored as JSON.
    /// </summary>
    public class SaveFileInfo
    {
        public SaveMetadata Metadata { get; set; }
        public string Checksum { get; set; }
        public long DataLength { get; set; }
    }
}
