using RPGFramework.Events;

namespace __GAME__
{
    /// <summary>
    /// The __FEATURE__ value changed.
    /// </summary>
    public readonly struct __FEATURE__ChangedEvent : IEvent
    {
        public __FEATURE__ChangedEvent(int oldValue, int newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public int OldValue { get; }
        public int NewValue { get; }
    }
}
