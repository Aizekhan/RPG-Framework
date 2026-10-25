namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Manages seasonal changes in the game world.
    /// </summary>
    public interface ISeasonSystem
    {
        /// <summary>
        /// Gets the current season.
        /// </summary>
        Season CurrentSeason { get; }

        /// <summary>
        /// Gets the current day within the season (0-based).
        /// </summary>
        int DayInSeason { get; }

        /// <summary>
        /// Gets the number of days in each season.
        /// </summary>
        int DaysPerSeason { get; }

        /// <summary>
        /// Gets the progression through the current season (0.0 - 1.0).
        /// </summary>
        float SeasonProgress { get; }

        /// <summary>
        /// Updates the season system based on the current day.
        /// </summary>
        /// <param name="currentDay">The current day number.</param>
        void Update(int currentDay);

        /// <summary>
        /// Sets the current season directly.
        /// </summary>
        void SetSeason(Season season);

        /// <summary>
        /// Sets the number of days per season.
        /// </summary>
        void SetDaysPerSeason(int days);

        /// <summary>
        /// Gets the temperature modifier for the current season (-1.0 to 1.0).
        /// </summary>
        float GetTemperatureModifier();
    }
}
