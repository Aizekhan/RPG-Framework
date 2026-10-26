using System;
using System.Collections.Generic;
using System.Threading;
#nullable enable

namespace RPGFramework.Logging
{
    /// <summary>
    /// Adds properties to every entry at log time (e.g. frame number, player id).
    /// </summary>
    public delegate void LogEnricher(IDictionary<string, object?> properties);

    /// <summary>
    /// Default <see cref="ILogger"/>: filters by minimum and per-category severity, attaches context and
    /// enricher properties, and fans entries out to sinks. A failing sink or enricher never breaks the caller.
    /// Thread-safe.
    /// </summary>
    public sealed class Logger : ILogger, IDisposable
    {
        public const string DefaultCategory = "General";

        private readonly object _gate = new object();
        private readonly Dictionary<string, LogSeverity> _categoryLevels = new Dictionary<string, LogSeverity>(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _context = new Dictionary<string, object?>(StringComparer.Ordinal);
        private ILogSink[] _sinks = Array.Empty<ILogSink>();
        private LogEnricher[] _enrichers = Array.Empty<LogEnricher>();
        private Func<DateTime> _clock = () => DateTime.UtcNow;
        private int _suppressedErrors;

        public Logger(LogSeverity minimumSeverity = LogSeverity.Information, params ILogSink[] sinks)
        {
            MinimumSeverity = minimumSeverity;
            foreach (var sink in sinks)
                AddSink(sink);
        }

        /// <summary>
        /// Entries below this severity are dropped unless a category override allows them.
        /// </summary>
        public LogSeverity MinimumSeverity { get; set; }

        /// <summary>
        /// Number of exceptions thrown by sinks or enrichers and swallowed so far.
        /// </summary>
        public int SuppressedErrorCount => Volatile.Read(ref _suppressedErrors);

        public void AddSink(ILogSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            lock (_gate)
            {
                if (Array.IndexOf(_sinks, sink) >= 0)
                    return;
                var copy = new ILogSink[_sinks.Length + 1];
                _sinks.CopyTo(copy, 0);
                copy[_sinks.Length] = sink;
                _sinks = copy;
            }
        }

        public bool RemoveSink(ILogSink sink)
        {
            lock (_gate)
            {
                int index = Array.IndexOf(_sinks, sink);
                if (index < 0)
                    return false;
                var copy = new List<ILogSink>(_sinks);
                copy.RemoveAt(index);
                _sinks = copy.ToArray();
                return true;
            }
        }

        public void AddEnricher(LogEnricher enricher)
        {
            if (enricher == null)
                throw new ArgumentNullException(nameof(enricher));

            lock (_gate)
            {
                var copy = new LogEnricher[_enrichers.Length + 1];
                _enrichers.CopyTo(copy, 0);
                copy[_enrichers.Length] = enricher;
                _enrichers = copy;
            }
        }

        /// <summary>
        /// Overrides <see cref="MinimumSeverity"/> for one category (stricter or looser).
        /// </summary>
        public void SetCategoryLevel(string category, LogSeverity minimumSeverity)
        {
            lock (_gate)
                _categoryLevels[category] = minimumSeverity;
        }

        public void ClearCategoryLevel(string category)
        {
            lock (_gate)
                _categoryLevels.Remove(category);
        }

        /// <summary>
        /// Adds or replaces a property attached to every following entry.
        /// </summary>
        public void SetContext(string key, object? value)
        {
            lock (_gate)
                _context[key] = value;
        }

        public void RemoveContext(string key)
        {
            lock (_gate)
                _context.Remove(key);
        }

        public void ClearContext()
        {
            lock (_gate)
                _context.Clear();
        }

        /// <summary>
        /// Replaces the timestamp source (tests, deterministic replays).
        /// </summary>
        public void SetClock(Func<DateTime> utcClock)
        {
            _clock = utcClock ?? throw new ArgumentNullException(nameof(utcClock));
        }

        public bool IsEnabled(LogSeverity severity, string? category = null)
        {
            lock (_gate)
            {
                if (category != null && _categoryLevels.TryGetValue(category, out var categoryLevel))
                    return severity >= categoryLevel;
                return severity >= MinimumSeverity;
            }
        }

        public void Log(LogSeverity severity, string message, string? category = null, Exception? exception = null)
        {
            category = string.IsNullOrEmpty(category) ? DefaultCategory : category!;
            if (!IsEnabled(severity, category))
                return;

            ILogSink[] sinks;
            LogEnricher[] enrichers;
            Dictionary<string, object?>? properties = null;

            lock (_gate)
            {
                sinks = _sinks;
                enrichers = _enrichers;
                if (_context.Count > 0)
                    properties = new Dictionary<string, object?>(_context, StringComparer.Ordinal);
            }

            if (sinks.Length == 0)
                return;

            foreach (var enricher in enrichers)
            {
                properties ??= new Dictionary<string, object?>(StringComparer.Ordinal);
                try
                {
                    enricher(properties);
                }
                catch
                {
                    Interlocked.Increment(ref _suppressedErrors);
                }
            }

            var entry = new LogEntry(_clock(), severity, category, message ?? string.Empty, exception, properties);

            foreach (var sink in sinks)
            {
                try
                {
                    sink.Write(in entry);
                }
                catch
                {
                    Interlocked.Increment(ref _suppressedErrors);
                }
            }
        }

        public void Flush()
        {
            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Flush();
                }
                catch
                {
                    Interlocked.Increment(ref _suppressedErrors);
                }
            }
        }

        /// <summary>
        /// Flushes and disposes sinks that implement <see cref="IDisposable"/>.
        /// </summary>
        public void Dispose()
        {
            Flush();
            ILogSink[] sinks;
            lock (_gate)
            {
                sinks = _sinks;
                _sinks = Array.Empty<ILogSink>();
            }

            foreach (var sink in sinks)
            {
                if (sink is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        Interlocked.Increment(ref _suppressedErrors);
                    }
                }
            }
        }
    }
}
