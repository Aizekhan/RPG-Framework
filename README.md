# RPG Framework

Reusable building blocks for RPGs in Unity 6, shipped as one UPM package: composition and events, combat, stats,
progression, quests, items, inventory, loot, saving, UI navigation, an action RPG made from inspector assets, and co-op
for 2-4 players. The framework owns the rules and infrastructure; each game owns its content, presentation and the
rules that make it unique.

## Start a new game

1. Create an empty **Unity 6** project (6000.0 or later).
2. Add two lines to its `Packages/manifest.json`:

   ```json
   "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
   "com.aizekhan.rpg-framework": "https://github.com/Aizekhan/RPG-Framework.git?path=/Packages/com.aizekhan.rpg-framework#v0.2.0"
   ```

3. In Unity: **RPG Framework > Game Kit > New Game or Feature...**, type a name, **New Game**, then **Play**.
   A hero fights waves of goblins, with loot, quests, a HUD and saved progress. **F2** hosts or joins co-op.

Everything after that (new enemies, items, quests, your own rules, tests) is in the package README:
[`Packages/com.aizekhan.rpg-framework/README.md`](Packages/com.aizekhan.rpg-framework/README.md).

## What is in this repository

| Path | What it is |
|---|---|
| `Packages/com.aizekhan.rpg-framework` | **The framework** (the package games install). Its README, CHANGELOG and `Documentation~` are the reference |
| `Assets/ReferenceGame` | Goblin Valley, a sample game built only on the package, the way a user's game is |
| `Assets/_Tests` | Framework-wide architecture tests |
| `STATUS.md` | What is verified, at which commit, and what is open |

Each release is a git tag (`v0.2.0`); pin one in the manifest so updates are deliberate.

## License

MIT, see `LICENSE`.
