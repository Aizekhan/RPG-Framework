using RPGFramework.Events;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Event raised when the season changes.
    /// Raised through <see cref="SeasonSystem.OnSeasonChanged"/>; forward it to an event bus if your game needs one.
    /// </summary>
    public class SeasonChangedEvent : IEvent
    {
        /// <summary>
        /// The previous season.
        /// </summary>
        public Season PreviousSeason { get; }

        /// <summary>
        /// The new season.
        /// </summary>
        public Season NewSeason { get; }

        /// <summary>
        /// The day number when the season changed.
        /// </summary>
        public int Day { get; }

        /// <summary>
        /// Creates a new season changed event.
        /// </summary>
        public SeasonChangedEvent(Season previousSeason, Season newSeason, int day)
        {
            PreviousSeason = previousSeason;
            NewSeason = newSeason;
            Day = day;
        }
    }
}
