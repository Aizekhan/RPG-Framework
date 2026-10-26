namespace RPGFramework.Logging
{
    /// <summary>
    /// Output target for <see cref="Logger"/> (console, file, memory, remote...).
    /// Implementations must not throw for normal I/O problems; <see cref="Logger"/> isolates failures anyway.
    /// </summary>
    public interface ILogSink
    {
        void Write(in LogEntry entry);

        /// <summary>
        /// Persists any buffered entries.
        /// </summary>
        void Flush();
    }
}
