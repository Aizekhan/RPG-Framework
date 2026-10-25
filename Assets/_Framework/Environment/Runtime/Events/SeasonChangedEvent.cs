namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Event raised when the season changes.
    /// </summary>
    public class SeasonChangedEvent
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
