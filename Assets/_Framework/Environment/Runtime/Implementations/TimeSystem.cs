using System;

namespace RPGFramework.Environment.Runtime
{
    /// <summary>
    /// Manages time progression and time of day.
    /// </summary>
    public class TimeSystem : ITimeSystem
    {
        private TimeData _timeData;
        private TimeOfDay _previousTimeOfDay;

        /// <summary>
        /// Event raised when the time of day changes.
        /// </summary>
        public event Action<TimeChangedEvent> OnTimeChanged;

        public TimeOfDay CurrentTimeOfDay => _timeData.TimeOfDay;
        public int CurrentHour => _timeData.Hour;
        public int CurrentMinute => _timeData.Minute;
        public int CurrentDay => _timeData.Day;
        public float TimeScale => _timeData.TimeScale;
        public float TotalGameTime => _timeData.TotalSeconds;

        /// <summary>
        /// Creates a new time system with the specified initial time.
        /// </summary>
        public TimeSystem(int startHour = 12, int startMinute = 0, float timeScale = 1f)
        {
            _timeData = new TimeData
            {
                Hour = startHour,
                Minute = startMinute,
                Day = 1,
                TimeScale = timeScale,
                TotalSeconds = (startHour * 60 + startMinute) * 60f,
                TimeOfDay = CalculateTimeOfDay(startHour)
            };
            _previousTimeOfDay = _timeData.TimeOfDay;
        }

        public void Update(float deltaTime)
        {
            _timeData.TotalSeconds += deltaTime * _timeData.TimeScale;

            int totalMinutes = (int)(_timeData.TotalSeconds / 60f);
            int totalHours = totalMinutes / 60;
            int days = totalHours / 24;

            _timeData.Day = days + 1;
            _timeData.Hour = (totalHours % 24);
            _timeData.Minute = (totalMinutes % 60);

            var newTimeOfDay = CalculateTimeOfDay(_timeData.Hour);
            if (newTimeOfDay != _previousTimeOfDay)
            {
                OnTimeChanged?.Invoke(new TimeChangedEvent(_previousTimeOfDay, newTimeOfDay, _timeData.Hour, _timeData.Minute, _timeData.Day));
                _previousTimeOfDay = newTimeOfDay;
            }

            _timeData.TimeOfDay = newTimeOfDay;
        }

        public void SetTimeScale(float scale)
        {
            _timeData.TimeScale = Math.Max(0f, scale);
        }

        public void SetTime(int hour, int minute)
        {
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);

            int currentDayMinutes = _timeData.Hour * 60 + _timeData.Minute;
            int newDayMinutes = hour * 60 + minute;
            int minuteDifference = newDayMinutes - currentDayMinutes;

            _timeData.Hour = hour;
            _timeData.Minute = minute;
            _timeData.TotalSeconds += minuteDifference * 60f;

            var newTimeOfDay = CalculateTimeOfDay(hour);
            if (newTimeOfDay != _previousTimeOfDay)
            {
                OnTimeChanged?.Invoke(new TimeChangedEvent(_previousTimeOfDay, newTimeOfDay, hour, minute, _timeData.Day));
                _previousTimeOfDay = newTimeOfDay;
            }

            _timeData.TimeOfDay = newTimeOfDay;
        }

        public void AdvanceTime(float hours)
        {
            float seconds = hours * 3600f;
            _timeData.TotalSeconds += seconds;

            int totalMinutes = (int)(_timeData.TotalSeconds / 60f);
            int totalHours = totalMinutes / 60;
            int days = totalHours / 24;

            _timeData.Day = days + 1;
            _timeData.Hour = (totalHours % 24);
            _timeData.Minute = (totalMinutes % 60);

            var newTimeOfDay = CalculateTimeOfDay(_timeData.Hour);
            if (newTimeOfDay != _previousTimeOfDay)
            {
                OnTimeChanged?.Invoke(new TimeChangedEvent(_previousTimeOfDay, newTimeOfDay, _timeData.Hour, _timeData.Minute, _timeData.Day));
                _previousTimeOfDay = newTimeOfDay;
            }

            _timeData.TimeOfDay = newTimeOfDay;
        }

        private TimeOfDay CalculateTimeOfDay(int hour)
        {
            return hour switch
            {
                >= 4 and < 6 => TimeOfDay.Dawn,
                >= 6 and < 9 => TimeOfDay.Morning,
                >= 9 and < 13 => TimeOfDay.Noon,
                >= 13 and < 17 => TimeOfDay.Afternoon,
                >= 17 and < 20 => TimeOfDay.Evening,
                >= 20 and < 22 => TimeOfDay.Dusk,
                >= 22 or < 2 => TimeOfDay.Night,
                _ => TimeOfDay.Midnight
            };
        }
    }
}
