using RPGFramework.Events;
namespace RPGFramework.Persistence
{
    /// <summary>
    /// Event raised when a save/load operation fails.
    /// </summary>
    public class SaveErrorEvent : IEvent
    {
        public string SlotId { get; }
        public SaveError Error { get; }
        public string ErrorMessage { get; }

        public SaveErrorEvent(string slotId, SaveError error, string errorMessage)
        {
            SlotId = slotId;
            Error = error;
            ErrorMessage = errorMessage;
        }
    }
}
