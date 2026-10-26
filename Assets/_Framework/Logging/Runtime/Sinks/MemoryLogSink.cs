using System;
using System.Collections.Generic;

namespace RPGFramework.Logging
{
    /// <summary>
    /// Keeps the most recent entries in a fixed-size ring buffer (debug consoles, crash reports, tests).
    /// Thread-safe.
    /// </summary>
    public sealed class MemoryLogSink : ILogSink
    {
        private readonly object _gate = new object();
        private readonly LogEntry[] _buffer;
        private int _start;
        private int _count;

        public MemoryLogSink(int capacity = 512)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new LogEntry[capacity];
        }

        public int Capacity => _buffer.Length;

        public int Count
        {
            get
            {
                lock (_gate)
                    return _count;
            }
        }

        public event Action<LogEntry> EntryWritten;

        public void Write(in LogEntry entry)
        {
            lock (_gate)
            {
                int index = (_start + _count) % _buffer.Length;
                _buffer[index] = entry;
                if (_count < _buffer.Length)
                    _count++;
                else
                    _start = (_start + 1) % _buffer.Length;
            }

            EntryWritten?.Invoke(entry);
        }

        public void Flush()
        {
        }

        /// <summary>
        /// Entries from oldest to newest.
        /// </summary>
        public IReadOnlyList<LogEntry> Snapshot()
        {
            lock (_gate)
            {
                var result = new LogEntry[_count];
                for (int i = 0; i < _count; i++)
                    result[i] = _buffer[(_start + i) % _buffer.Length];
                return result;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _start = 0;
                _count = 0;
            }
        }
    }
}
