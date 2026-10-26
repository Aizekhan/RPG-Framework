using RPGFramework.ECS;

namespace RPGFramework.Persistence
{
    /// <summary>
    /// ECS component that tracks the currently active save slot.
    /// </summary>
    public struct SaveSlotComponent : IComponent
    {
        public string ActiveSlotId;
        public bool IsQuickSave;
        public bool IsAutoSave;
    }
}
