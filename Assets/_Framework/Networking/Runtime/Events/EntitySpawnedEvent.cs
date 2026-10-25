namespace RPGFramework.Networking
{
    /// <summary>
    /// Event raised when a network entity is spawned.
    /// </summary>
    public struct EntitySpawnedEvent
    {
        public int NetworkId;
        public int OwnerId;
        public string PrefabId;
    }
}
