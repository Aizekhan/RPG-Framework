# Recipe: start a game

1. **RPG Framework > Game Kit > New Game or Feature...**, type the name (PascalCase, e.g. `DragonPeak`), press
   **New Game**. Batch mode:
   `Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine -game DragonPeak`
   (add `-aiGuidance` to also install `CLAUDE.md` and `Docs/RPGFramework`).
2. You get a playable action RPG:

   | Path | What |
   |---|---|
   | `Content/DragonPeakSetup.asset` | **Game Setup**: player, enemies, waves, items, loot, starting quests |
   | `Content/Characters/Hero.asset`, `Goblin.asset` | **Character**: prefab, stats, growth, speed, reach, experience, loot |
   | `Content/Items`, `Loot`, `Quests`, `Materials` | starter items, the goblin's loot table, the first quest, ground |
   | `Scripts/Core/DragonPeak.Core.asmdef` | rules assembly, `noEngineReferences: true` |
   | `Scripts/Core/DragonPeakComposition.cs` | the game's own modules (`// rpg-framework:modules` marks where features go) |
   | `Scripts/Core/DragonPeakModule.cs`, `DragonPeakSession.cs`, `DragonPeakEvents.cs` | the game's own module, rules entry point, first event |
   | `Scripts/DragonPeakHost.cs` | scene entry point (`ActionRpgHost`; its Setup field holds the Game Setup) |
   | `Scenes/DragonPeak.unity` | camera, light, ground, host |
   | `Tests/` | architecture tests, boot and wave tests on the real Game Setup, a scene test |
3. Run the tests (`unity test . --mode EditMode`). All pass on a fresh game.
4. Open the scene and press Play: the hero fights waves of goblins; the HUD shows health, quests and loot.
5. Next: content in the inspector (recipe 10), your own rules (recipes 02, 03), framework modules (recipe 07).
