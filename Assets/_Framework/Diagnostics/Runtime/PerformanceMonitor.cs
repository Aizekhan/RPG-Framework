using System;
using RPGFramework.Systems;

namespace RPGFramework.Diagnostics
{
    /// <summary>
    /// Frame-time and managed-memory statistics over a rolling window of frames. Register it as an update system
    /// (it reads the frame delta it is given, so it works with any time source).
    /// </summary>
    public sealed class PerformanceMonitor : IUpdateSystem
    {
        private readonly float[] _frames;
        private int _next;
        private int _count;
        private int _gcAtStart;

        public PerformanceMonitor() : this(120)
        {
        }

        public PerformanceMonitor(int windowFrames)
        {
            if (windowFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowFrames));
            _frames = new float[windowFrames];
        }

        public int SampleCount => _count;

        public float AverageFrameMs { get; private set; }
        public float MaxFrameMs { get; private set; }

        public float AverageFps => AverageFrameMs > 0f ? 1000f / AverageFrameMs : 0f;

        /// <summary>
        /// FPS of the slowest frame in the window.
        /// </summary>
        public float WorstFps => MaxFrameMs > 0f ? 1000f / MaxFrameMs : 0f;

        public long ManagedMemoryBytes { get; private set; }

        /// <summary>
        /// Generation-0 garbage collections since <see cref="Initialize"/> (or <see cref="Reset"/>).
        /// </summary>
        public int GcCollections { get; private set; }

        public void Initialize() => Reset();

        public void Update(float deltaTime)
        {
            if (deltaTime < 0f)
                return;

            _frames[_next] = deltaTime * 1000f;
            _next = (_next + 1) % _frames.Length;
            if (_count < _frames.Length)
                _count++;

            float sum = 0f;
            float max = 0f;
            for (int i = 0; i < _count; i++)
            {
                sum += _frames[i];
                max = Math.Max(max, _frames[i]);
            }
            AverageFrameMs = sum / _count;
            MaxFrameMs = max;
            ManagedMemoryBytes = GC.GetTotalMemory(false);
            GcCollections = GC.CollectionCount(0) - _gcAtStart;
        }

        public void Reset()
        {
            _next = 0;
            _count = 0;
            AverageFrameMs = 0f;
            MaxFrameMs = 0f;
            _gcAtStart = GC.CollectionCount(0);
            GcCollections = 0;
        }

        public void Dispose()
        {
        }
    }
}
