# Recipe: the framework from the command line (batch mode, CI, agents)

Every framework tool that an assistant or a CI job needs runs without opening the editor window. Run them from the
project folder; `Unity` is the editor executable (Unity Hub: `Editor/Unity.exe` of the project's version), or use the
Unity CLI (`unity`) where shown.

## Tests

```
unity test . --mode EditMode --output Results/editmode.xml
unity test . --mode PlayMode --output Results/playmode.xml
unity test . --mode EditMode --filter "MyGame" --output Results/mygame.xml
```

The NUnit XML holds every test with its result and message: read the failures there, not in the console. Without the
CLI: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Results/editmode.xml`
(`-testFilter` to narrow; do not pass `-quit` with `-runTests`). Exit code 0 when all pass, 2 when a test failed.

## Entry points (`-executeMethod`)

Run as `Unity -batchmode -quit -projectPath . -executeMethod <method> [arguments]`.

| Method | What it does | Arguments | Exit code |
|---|---|---|---|
| `RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine` | New game from the action RPG template (recipe 01) | `-game <Name>`, `-company <Name>`, `-aiGuidance` | 0; errors in the log |
| `RPGFramework.GameKit.Editor.GameScaffold.NewFeatureFromCommandLine` | New feature module with rules, events and tests (recipe 02) | `-game <Name> -feature <Name>` | 0; errors in the log |
| `RPGFramework.GameKit.Editor.AIGuidance.InstallFromCommandLine` | Installs or refreshes CLAUDE.md, these recipes and the process files | none | 0 |
| `RPGFramework.Editor.Utilities.FrameworkValidation.ValidateFromCommandLine` | Checks quest, dialogue, faction, recipe, item and loot data | none | 0 clean, 1 errors, 2 the check failed |
| `RPGFramework.Editor.Utilities.FrameworkValidation.ValidateAllData` | The same check, results in the log only (`=== RPG Framework Validation Results ===`, `Found N error(s):`) | none | always 0 |
| `RPGFramework.Tools.ProjectIndex.ProjectIndexCommand.ExportFromCommandLine` | Writes the project index (below) | `-indexOutput <file>`, `-includePackages`, `-indexExclude <a;b>` | 0 written, 1 failed |
| `RPGFramework.ContentImport.Editor.ContentAssetImport.ImportFromCommandLine` | Makes or updates Quest and Dialogue assets from a content JSON file (recipe 20) | `-contentFile <file>`, `-contentFolder <folder>` (default `Assets/Content`) | 0 imported, 1 the file has errors (nothing written), 2 failed |

The game's architecture rules run as tests (`MyGameArchitectureTests`), so `unity test` covers them.

## Project index

A read-only map of the project as JSON (`rpgframework.project-index/1`), for an assistant or a tool that needs to
know what the project holds: packages, render pipeline, build scenes, the git branch and commit; every asset with its
GUID, type (`ScriptableObject:<Class>` for data assets) and the GUIDs it references; scenes and prefabs with their
root objects and components; scripts with assembly, namespace, `using`s, types (kind, base types, XML summary, public
members); assemblies and their references; the framework version and which of its modules each game assembly uses;
`problems` (missing scripts, references to missing assets, build scenes that are gone, binary-serialized assets) and
`coverage` per kind.

It reads files only (`.meta`, the YAML of scenes, prefabs and assets, `.cs`, `.asmdef`, `Packages/manifest.json`,
`ProjectSettings`): no AssetDatabase, no import, no file written except the output, so it also works on a project that
does not compile. Library, Temp, Logs, obj, UserSettings, hidden folders, `~` folders and key files (.pem, .key,
.keystore, .jks, .p12, .pfx) are skipped. Assets must use Force Text serialization (the Unity default) for their
references to be read.

- Menu: **RPG Framework > Tools > Export Project Index** (writes `Library/RPGFramework/ProjectIndex.json`).
- Code: `ProjectIndexer.Scan(projectRoot, options)` returns the `ProjectIndex`; `ProjectIndexer.Export(root, file)`.
- Paths in it are project-relative; there are no absolute paths and no user names.

Refresh it after a big change, not every session; it is a snapshot.
