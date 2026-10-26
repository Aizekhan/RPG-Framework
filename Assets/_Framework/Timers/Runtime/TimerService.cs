using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RPGFramework.Logging;
using RPGFramework.Systems;

namespace RPGFramework.Timers
{
    /// <summary>
    /// Identifies a scheduled timer. Ids are never reused, so handles of finished or cancelled timers are
    /// stale and never affect a newer timer.
    /// </summary>
    public readonly struct TimerHandle : IEquatable<TimerHandle>
    {
        internal TimerHandle(int id)
        {
            Id = id;
        }

        internal int Id { get; }

        public bool IsValid => Id != 0;

        public static readonly TimerHandle None = default;

        public bool Equals(TimerHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is TimerHandle other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => IsValid ? $"Timer#{Id}" : "Timer#none";
    }

    public readonly struct TimerInfo
    {
        public TimerInfo(TimerHandle handle, string category, float interval, float remaining, int remainingRepeats, bool isPaused)
        {
            Handle = handle;
            Category = category;
            Interval = interval;
            Remaining = remaining;
            RemainingRepeats = remainingRepeats;
            IsPaused = isPaused;
        }

        public TimerHandle Handle { get; }
        public string Category { get; }
        public float Interval { get; }
        public float Remaining { get; }

        /// <summary>
        /// -1 for infinite repetition.
        /// </summary>
        public int RemainingRepeats { get; }

        public bool IsPaused { get; }
    }

    public interface ITimerService
    {
        /// <summary>
        /// Multiplier applied to every delta (0 freezes all timers). Default 1.
        /// </summary>
        float TimeScale { get; set; }

        int ActiveCount { get; }

        /// <summary>
        /// Runs <paramref name="onComplete"/> once after <paramref name="delaySeconds"/> of game time.
        /// </summary>
        TimerHandle Schedule(float delaySeconds, Action onComplete, string category = null);

        /// <summary>
        /// Runs <paramref name="onTick"/> every <paramref name="intervalSeconds"/>; <paramref name="repeatCount"/> &lt; 0 repeats until cancelled.
        /// A long frame fires every interval it crossed (no drift).
        /// </summary>
        TimerHandle Repeat(float intervalSeconds, Action onTick, int repeatCount = -1, string category = null);

        bool Cancel(TimerHandle handle);
        bool Pause(TimerHandle handle);
        bool Resume(TimerHandle handle);
        bool IsActive(TimerHandle handle);
        bool IsPaused(TimerHandle handle);

        /// <summary>
        /// Seconds until the next firing, or 0 for stale handles.
        /// </summary>
        float GetRemaining(TimerHandle handle);

        int PauseCategory(string category);
        int ResumeCategory(string category);
        int CancelCategory(string category);
        void CancelAll();

        /// <summary>
        /// Completes after <paramref name="delaySeconds"/> of game time (respects TimeScale and category pause).
        /// </summary>
        UniTask DelayAsync(float delaySeconds, CancellationToken cancellationToken = default, string category = null);

        IReadOnlyList<TimerInfo> GetTimers();
    }

    /// <summary>
    /// Game-time timers driven by <see cref="Update"/> (register it as a system). Timers may be scheduled or
    /// cancelled from inside callbacks; new timers start counting from the next update. Callback exceptions are
    /// logged and do not affect other timers.
    /// </summary>
    public sealed class TimerService : ITimerService, IUpdateSystem
    {
        private const string Category = "Timers";

        private readonly ILogger _logger;
        private readonly Dictionary<int, Timer> _timers = new Dictionary<int, Timer>();
        private readonly List<Timer> _order = new List<Timer>();
        private readonly List<Timer> _added = new List<Timer>();
        private int _nextId = 1;
        private bool _updating;
        private float _timeScale = 1f;

        public TimerService(ILogger logger = null)
        {
            _logger = logger;
        }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = value >= 0f ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public int ActiveCount => _timers.Count;

        public TimerHandle Schedule(float delaySeconds, Action onComplete, string category = null)
        {
            if (onComplete == null)
                throw new ArgumentNullException(nameof(onComplete));
            if (delaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            return Add(delaySeconds, onComplete, 1, category);
        }

        public TimerHandle Repeat(float intervalSeconds, Action onTick, int repeatCount = -1, string category = null)
        {
            if (onTick == null)
                throw new ArgumentNullException(nameof(onTick));
            if (intervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Repeat interval must be positive");
            if (repeatCount == 0)
                throw new ArgumentOutOfRangeException(nameof(repeatCount), "Use a positive count or -1 for infinite");
            return Add(intervalSeconds, onTick, repeatCount, category);
        }

        public bool Cancel(TimerHandle handle)
        {
            if (!TryGet(handle, out var timer))
                return false;
            Remove(timer);
            return true;
        }

        public bool Pause(TimerHandle handle) => SetPaused(handle, true);

        public bool Resume(TimerHandle handle) => SetPaused(handle, false);

        public bool IsActive(TimerHandle handle) => TryGet(handle, out _);

        public bool IsPaused(TimerHandle handle) => TryGet(handle, out var timer) && timer.Paused;

        public float GetRemaining(TimerHandle handle) => TryGet(handle, out var timer) ? Math.Max(0f, timer.Remaining) : 0f;

        public int PauseCategory(string category) => ForCategory(category, t => t.Paused = true);

        public int ResumeCategory(string category) => ForCategory(category, t => t.Paused = false);

        public int CancelCategory(string category) => ForCategory(category, Remove);

        public void CancelAll()
        {
            foreach (var timer in new List<Timer>(_timers.Values))
                Remove(timer);
        }

        public async UniTask DelayAsync(float delaySeconds, CancellationToken cancellationToken = default, string category = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new UniTaskCompletionSource();
            var handle = Schedule(delaySeconds, () => completion.TrySetResult(), category);
            using (cancellationToken.Register(() =>
                   {
                       Cancel(handle);
                       completion.TrySetCanceled(cancellationToken);
                   }))
            {
                await completion.Task;
            }
        }

        public IReadOnlyList<TimerInfo> GetTimers()
        {
            var result = new List<TimerInfo>(_timers.Count);
            foreach (var timer in _order)
            {
                if (!timer.Removed)
                    result.Add(timer.ToInfo());
            }
            foreach (var timer in _added)
            {
                if (!timer.Removed)
                    result.Add(timer.ToInfo());
            }
            return result;
        }

        public void Initialize()
        {
        }

        public void Update(float deltaTime)
        {
            if (_updating)
                return;

            float scaled = deltaTime * _timeScale;
            _updating = true;
            try
            {
                if (_added.Count > 0)
                {
                    _order.AddRange(_added);
                    _added.Clear();
                }

                if (scaled > 0f)
                {
                    // _order may not change during this loop: additions go to _added, removals only flag timers
                    for (int i = 0; i < _order.Count; i++)
                    {
                        var timer = _order[i];
                        if (timer.Removed || timer.Paused)
                            continue;
                        Advance(timer, scaled);
                    }
                }

                _order.RemoveAll(t => t.Removed);
            }
            finally
            {
                _updating = false;
            }
        }

        public void Dispose() => CancelAll();

        private void Advance(Timer timer, float deltaTime)
        {
            timer.Remaining -= deltaTime;
            while (timer.Remaining <= 0f && !timer.Removed && !timer.Paused)
            {
                if (timer.RepeatsLeft > 0)
                    timer.RepeatsLeft--;

                bool last = timer.RepeatsLeft == 0;
                if (last)
                    Remove(timer);
                else
                    timer.Remaining += timer.Interval;

                try
                {
                    timer.Callback();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Timer callback failed ({timer.Category ?? "uncategorised"})", Category, ex);
                }

                if (last)
                    break;
            }
        }

        private TimerHandle Add(float interval, Action callback, int repeats, string category)
        {
            int id = _nextId++;
            var timer = new Timer(new TimerHandle(id), interval, callback, repeats, category);
            _timers.Add(id, timer);
            if (_updating)
                _added.Add(timer);
            else
                _order.Add(timer);
            return timer.Handle;
        }

        private void Remove(Timer timer)
        {
            if (timer.Removed)
                return;
            timer.Removed = true;
            _timers.Remove(timer.Handle.Id);
            if (!_updating)
            {
                _order.Remove(timer);
                _added.Remove(timer);
            }
        }

        private bool TryGet(TimerHandle handle, out Timer timer) =>
            _timers.TryGetValue(handle.Id, out timer) && !timer.Removed;

        private bool SetPaused(TimerHandle handle, bool paused)
        {
            if (!TryGet(handle, out var timer))
                return false;
            timer.Paused = paused;
            return true;
        }

        private int ForCategory(string category, Action<Timer> action)
        {
            var matches = new List<Timer>();
            foreach (var timer in _timers.Values)
            {
                if (string.Equals(timer.Category, category, StringComparison.Ordinal))
                    matches.Add(timer);
            }
            foreach (var timer in matches)
                action(timer);
            return matches.Count;
        }

        private sealed class Timer
        {
            public Timer(TimerHandle handle, float interval, Action callback, int repeats, string category)
            {
                Handle = handle;
                Interval = interval;
                Remaining = interval;
                Callback = callback;
                RepeatsLeft = repeats;
                Category = category;
            }

            public TimerHandle Handle { get; }
            public float Interval { get; }
            public Action Callback { get; }
            public string Category { get; }
            public float Remaining;
            public int RepeatsLeft;
            public bool Paused;
            public bool Removed;

            public TimerInfo ToInfo() => new TimerInfo(Handle, Category, Interval, Math.Max(0f, Remaining), RepeatsLeft, Paused);
        }
    }
}
