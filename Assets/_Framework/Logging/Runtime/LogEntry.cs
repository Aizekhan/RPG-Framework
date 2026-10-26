using System;
using System.Collections.Generic;
#nullable enable

namespace RPGFramework.Logging
{
    /// <summary>
    /// One formatted-independent log record delivered to every <see cref="ILogSink"/>.
    /// </summary>
    public readonly struct LogEntry
    {
        public LogEntry(
            DateTime timestampUtc,
            LogSeverity severity,
            string category,
            string message,
            Exception? exception,
            IReadOnlyDictionary<string, object?>? properties)
        {
            TimestampUtc = timestampUtc;
            Severity = severity;
            Category = category;
            Message = message;
            Exception = exception;
            Properties = properties;
        }

        public DateTime TimestampUtc { get; }
        public LogSeverity Severity { get; }
        public string Category { get; }
        public string Message { get; }
        public Exception? Exception { get; }

        /// <summary>
        /// Context and enricher properties captured at log time, or null when there are none.
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Properties { get; }

        /// <summary>
        /// Plain-text single-record representation used by text sinks.
        /// </summary>
        public override string ToString()
        {
            var text = $"{TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}Z [{Severity}] [{Category}] {Message}";

            if (Properties != null && Properties.Count > 0)
            {
                var parts = new List<string>(Properties.Count);
                foreach (var pair in Properties)
                    parts.Add($"{pair.Key}={pair.Value ?? "null"}");
                text += " {" + string.Join(", ", parts) + "}";
            }

            if (Exception != null)
                text += Environment.NewLine + Exception;

            return text;
        }
    }
}
