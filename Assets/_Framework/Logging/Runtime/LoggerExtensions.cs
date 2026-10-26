using System;
#nullable enable

namespace RPGFramework.Logging
{
    /// <summary>
    /// Severity shortcuts for any <see cref="ILogger"/>. A null logger is ignored.
    /// </summary>
    public static class LoggerExtensions
    {
        public static void LogTrace(this ILogger? logger, string message, string? category = null) =>
            logger?.Log(LogSeverity.Trace, message, category);

        public static void LogDebug(this ILogger? logger, string message, string? category = null) =>
            logger?.Log(LogSeverity.Debug, message, category);

        public static void LogInfo(this ILogger? logger, string message, string? category = null) =>
            logger?.Log(LogSeverity.Information, message, category);

        public static void LogWarning(this ILogger? logger, string message, string? category = null, Exception? exception = null) =>
            logger?.Log(LogSeverity.Warning, message, category, exception);

        public static void LogError(this ILogger? logger, string message, string? category = null, Exception? exception = null) =>
            logger?.Log(LogSeverity.Error, message, category, exception);

        public static void LogCritical(this ILogger? logger, string message, string? category = null, Exception? exception = null) =>
            logger?.Log(LogSeverity.Critical, message, category, exception);

        /// <summary>
        /// Returns a logger that uses <paramref name="category"/> whenever the caller passes none.
        /// </summary>
        public static ILogger ForCategory(this ILogger logger, string category) =>
            new CategoryLogger(logger, category);
    }

    /// <summary>
    /// Wraps a logger with a default category.
    /// </summary>
    public sealed class CategoryLogger : ILogger
    {
        private readonly ILogger _inner;

        public CategoryLogger(ILogger inner, string category)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Category = string.IsNullOrEmpty(category) ? throw new ArgumentException("Category is required", nameof(category)) : category;
        }

        public string Category { get; }

        public void Log(LogSeverity severity, string message, string? category = null, Exception? exception = null) =>
            _inner.Log(severity, message, category ?? Category, exception);
    }

    /// <summary>
    /// Logger that discards everything; use instead of null checks.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public void Log(LogSeverity severity, string message, string? category = null, Exception? exception = null)
        {
        }
    }
}
