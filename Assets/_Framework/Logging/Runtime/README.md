# RPGFramework Logging Contract

This assembly defines only the framework-facing logging abstraction and severity values.

## Public API
- `ILogger.Log(LogSeverity, string, string, Exception)`
- `LogSeverity`: Trace, Debug, Information, Warning, Error, Critical

## Boundary
- No Unity, MythHunter, DI container, file system, or provider dependencies.
- Concrete sinks/formatting and MythHunter-specific features remain in the Game Layer/adapters.
- Existing `IMythLogger` remains a compatibility facade; it can extend `ILogger` while preserving its established methods.
