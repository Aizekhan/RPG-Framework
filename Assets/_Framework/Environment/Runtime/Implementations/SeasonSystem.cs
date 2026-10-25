using System;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Manages seasonal changes.
    /// </summary>
    public class SeasonSystem : ISeasonSystem
    {
        private Season _currentSeason;
        private int _daysPerSeason;
        private int _previousDay;

        /// <summary>
        /// Event raised when the season changes.
        /// </summary>
        public event Action<SeasonChangedEvent> OnSeasonChanged;

        public Season CurrentSeason => _currentSeason;
        public int DayInSeason { get; private set; }
        public int DaysPerSeason => _daysPerSeason;
        public float SeasonProgress => (float)DayInSeason / _daysPerSeason;

        /// <summary>
        /// Creates a new season system with the specified initial season and days per season.
        /// </summary>
        public SeasonSystem(Season initialSeason = Season.Spring, int daysPerSeason = 30)
        {
            _currentSeason = initialSeason;
            _daysPerSeason = Math.Max(1, daysPerSeason);
            DayInSeason = 0;
            _previousDay = 0;
        }

        public void Update(int currentDay)
        {
            if (currentDay == _previousDay)
            {
                return;
            }

            _previousDay = currentDay;

            int totalSeasonDays = currentDay - 1;
            int seasonIndex = (totalSeasonDays / _daysPerSeason) % 4;
            DayInSeason = totalSeasonDays % _daysPerSeason;

            var newSeason = (Season)seasonIndex;
            if (newSeason != _currentSeason)
            {
                var previousSeason = _currentSeason;
                _currentSeason = newSeason;
                OnSeasonChanged?.Invoke(new SeasonChangedEvent(previousSeason, newSeason, currentDay));
            }
        }

        public void SetSeason(Season season)
        {
            if (season != _currentSeason)
            {
                var previousSeason = _currentSeason;
                _currentSeason = season;
                DayInSeason = 0;
                OnSeasonChanged?.Invoke(new SeasonChangedEvent(previousSeason, season, _previousDay));
            }
        }

        public void SetDaysPerSeason(int days)
        {
            _daysPerSeason = Math.Max(1, days);
        }

        public float GetTemperatureModifier()
        {
            return _currentSeason switch
            {
                Season.Spring => 0f,
                Season.Summer => 0.5f,
                Season.Autumn => -0.2f,
                Season.Winter => -1f,
                _ => 0f
            };
        }
    }
}
