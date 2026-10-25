using System;
using RPGFramework.Logging;

namespace RPGFramework.DI
{
    /// <summary>
    /// Extension methods for ILogger to provide convenience methods used by DI system.
    /// </summary>
    internal static class LoggerExtensions
    {
        public static void LogDebug(this ILogger logger, string message, string category = null)
        {
            logger?.Log(LogSeverity.Debug, message, category);
        }

        public static void LogInfo(this ILogger logger, string message, string category = null)
        {
            logger?.Log(LogSeverity.Information, message, category);
        }

        public static void LogWarning(this ILogger logger, string message, string category = null)
        {
            logger?.Log(LogSeverity.Warning, message, category);
        }

        public static void LogError(this ILogger logger, string message, string category = null, Exception exception = null)
        {
            logger?.Log(LogSeverity.Error, message, category, exception);
        }
    }
}
