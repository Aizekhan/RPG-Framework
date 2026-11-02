# RPGFramework Logging

The framework's logger, without UnityEngine.

## Public API
- `ILogger.Log(LogSeverity, string, string, Exception)` and `LoggerExtensions` (`LogError`, ...; needs `using RPGFramework.Logging;`)
- `LogSeverity`: Trace, Debug, Information, Warning, Error, Critical
- `Logger` (per-category levels, context, enrichers, sinks isolated from each other), `CategoryLogger`, `NullLogger`, `LogEntry`
- Sinks: `ILogSink`, `MemoryLogSink` (tests), `FileLogSink` (rotation)

## Boundary
- No Unity, DI container or provider dependencies.
- The Unity console sink, `UnityConsoleLogSink`, is in `RPGFramework.Logging.Unity`.
