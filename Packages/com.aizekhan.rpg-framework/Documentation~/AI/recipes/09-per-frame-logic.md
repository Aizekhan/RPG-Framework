# Recipe: logic that runs every frame

Waves, regeneration, cooldowns, AI decisions:

1. Write a class implementing `IUpdateSystem` (`Initialize`, `Update(float deltaTime)`, `Dispose`) in `<Game>.Core`.
   It gets the rules it drives through its constructor.
2. Register it in the module's `OnConfigure` with `AddSystem<MySystem>()` (optional priority, see `SystemPriorities`).
   The host ticks it from Unity; tests tick it with `host.Update(dt)`.
3. For one-off or repeating delays, inject `ITimerService` (`Schedule(seconds, action)`, `Repeat(...)`) instead of
   counting time by hand.
4. Example: `WaveSystem` in the package (`ActionRpg/Runtime`).
