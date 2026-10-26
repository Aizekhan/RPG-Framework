using System;
using System.Collections.Generic;

namespace RPGFramework.Systems
{
    public enum SystemStage
    {
        Update,
        FixedUpdate,
        LateUpdate
    }

    /// <summary>
    /// Receives the duration of every system call made by a <see cref="SystemGroup"/> whose
    /// <see cref="SystemGroup.Profiler"/> is set. Nested groups report under their own name too.
    /// </summary>
    public interface ISystemProfiler
    {
        void Record(string systemName, SystemStage stage, double milliseconds);
    }

    public readonly struct SystemTiming
    {
        public SystemTiming(string systemName, SystemStage stage, double lastMs, double averageMs, double maxMs, int samples)
        {
            SystemName = systemName;
            Stage = stage;
            LastMs = lastMs;
            AverageMs = averageMs;
            MaxMs = maxMs;
            Samples = samples;
        }

        public string SystemName { get; }
        public SystemStage Stage { get; }
        public double LastMs { get; }
        public double AverageMs { get; }

        /// <summary>
        /// Maximum within the sample window.
        /// </summary>
        public double MaxMs { get; }

        public int Samples { get; }

        public override string ToString() =>
            $"{SystemName}.{Stage}: last {LastMs:F3} ms, avg {AverageMs:F3} ms, max {MaxMs:F3} ms ({Samples} samples)";
    }

    /// <summary>
    /// Rolling per-system timings (last N calls per system and stage). Replaces MythHunter's manual
    /// Begin/End profiler: the group measures every call, so systems need no profiling code.
    /// </summary>
    public sealed class SystemProfiler : ISystemProfiler
    {
        private readonly Dictionary<(string, SystemStage), Window> _windows = new Dictionary<(string, SystemStage), Window>();
        private readonly int _windowSize;

        public SystemProfiler(int windowSize = 120)
        {
            if (windowSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize));
            _windowSize = windowSize;
        }

        /// <summary>
        /// Calls slower than this count as slow in <see cref="GetSlowSystems"/>. Default ≈ one 60 FPS frame.
        /// </summary>
        public double SlowThresholdMs { get; set; } = 16.6;

        public bool IsEnabled { get; set; } = true;

        public void Record(string systemName, SystemStage stage, double milliseconds)
        {
            if (!IsEnabled || systemName == null)
                return;
            var key = (systemName, stage);
            if (!_windows.TryGetValue(key, out var window))
            {
                window = new Window(_windowSize);
                _windows.Add(key, window);
            }
            window.Add(milliseconds);
        }

        public bool TryGetTiming(string systemName, SystemStage stage, out SystemTiming timing)
        {
            if (systemName != null && _windows.TryGetValue((systemName, stage), out var window))
            {
                timing = window.ToTiming(systemName, stage);
                return true;
            }
            timing = default;
            return false;
        }

        /// <summary>
        /// All timings, slowest average first.
        /// </summary>
        public IReadOnlyList<SystemTiming> GetTimings()
        {
            var result = new List<SystemTiming>(_windows.Count);
            foreach (var pair in _windows)
                result.Add(pair.Value.ToTiming(pair.Key.Item1, pair.Key.Item2));
            result.Sort((a, b) => b.AverageMs.CompareTo(a.AverageMs));
            return result;
        }

        /// <summary>
        /// Timings whose last call exceeded <see cref="SlowThresholdMs"/>.
        /// </summary>
        public IReadOnlyList<SystemTiming> GetSlowSystems()
        {
            var result = new List<SystemTiming>();
            foreach (var timing in GetTimings())
            {
                if (timing.LastMs > SlowThresholdMs)
                    result.Add(timing);
            }
            return result;
        }

        public void Reset() => _windows.Clear();

        private sealed class Window
        {
            private readonly double[] _samples;
            private int _next;
            private int _count;
            private double _sum;
            private double _last;

            public Window(int size)
            {
                _samples = new double[size];
            }

            public void Add(double value)
            {
                if (_count == _samples.Length)
                    _sum -= _samples[_next];
                else
                    _count++;
                _samples[_next] = value;
                _sum += value;
                _next = (_next + 1) % _samples.Length;
                _last = value;
            }

            public SystemTiming ToTiming(string name, SystemStage stage)
            {
                double max = 0;
                for (int i = 0; i < _count; i++)
                    max = Math.Max(max, _samples[i]);
                return new SystemTiming(name, stage, _last, _count > 0 ? _sum / _count : 0, max, _count);
            }
        }
    }
}
