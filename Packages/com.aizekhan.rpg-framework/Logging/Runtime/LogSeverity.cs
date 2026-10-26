namespace RPGFramework.Logging
{
    /// <summary>
    /// Platform-neutral severity for framework log messages.
    /// Values are ordered from least to most severe.
    /// </summary>
    public enum LogSeverity
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }
}