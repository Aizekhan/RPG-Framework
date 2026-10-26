# Recipe: start a game

1. **RPG Framework > Game Kit > New Game or Feature...**, type the name (PascalCase, e.g. `DragonPeak`), press
   **New Game**. Batch mode:
   `Unity -batchmode -quit -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine -game DragonPeak`
2. You get:

   | Path | What |
   |---|---|
   | `Scripts/Core/DragonPeak.Core.asmdef` | rules assembly, `noEngineReferences: true` |
   | `Scripts/Core/DragonPeakComposition.cs` | the module list (`// rpg-framework:modules` marks where features go) |
   | `Scripts/Core/DragonPeakModule.cs`, `DragonPeakSession.cs`, `DragonPeakEvents.cs` | the game's own module, rules entry point, first event |
   | `Scripts/DragonPeakHost.cs` | scene entry point (`GameHostBehaviour`) |
   | `Scenes/DragonPeak.unity` | camera, light, host |
   | `Tests/` | architecture tests, a boot test, a scene test |

   It also installs `CLAUDE.md` and `Docs/RPGFramework` at the project root.
3. Run the tests (`unity test . --mode EditMode`). All pass on a fresh game.
4. Open the scene and press Play: the game boots and the Console shows the framework start log.
5. Next: add features (recipe 02), framework modules (recipe 07), the first visible entities (recipe 04).
