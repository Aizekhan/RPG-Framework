using RPGFramework.ECS;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// ECS component that tracks auto-save state and timing.
    /// </summary>
    public struct SaveSystemComponent : IComponent
    {
        public bool AutoSaveEnabled;
        public float AutoSaveIntervalSeconds;
        public float TimeSinceLastAutoSave;
        public string LastSavedSlotId;
        public long LastSaveTimestamp;
    }
}
