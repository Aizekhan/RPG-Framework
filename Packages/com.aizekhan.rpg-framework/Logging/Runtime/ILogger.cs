using System;
#nullable enable

namespace RPGFramework.Logging
{
    /// <summary>
    /// Minimal platform-neutral logging contract.
    /// Provider-specific formatting, storage, Unity console output and file rotation belong to adapters.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes one log message with optional category and exception context.
        /// </summary>
        void Log(LogSeverity severity, string message, string? category = null, Exception? exception = null);
    }
}