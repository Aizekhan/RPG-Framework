# RPG Framework

Reusable building blocks for RPGs in Unity 6, shipped as one UPM package: composition and events, combat, stats,
progression, quests, items, inventory, loot, saving, UI navigation, an action RPG made from inspector assets, co-op
for 2-8 players and PvP, and the rules of a turn-based party RPG. The framework owns the rules and infrastructure; each game owns its content, presentation and the
rules that make it unique.

## Start a new game with Claude Code (recommended)

1. Make an empty folder for the game.
2. Put one file in it: [`START-NEW-GAME.md`](START-NEW-GAME.md)
   ([download](https://raw.githubusercontent.com/Aizekhan/RPG-Framework/main/START-NEW-GAME.md)).
3. Open Claude Code in that folder and type **"Follow START-NEW-GAME.md"**.

Claude Code checks your tools, creates the Unity project, installs the latest framework release, generates a playable
game, fills `GAME.md` with you and then builds the game feature by feature. What the framework lacks is recorded in
`FRAMEWORK-GAPS.md` instead of being worked around (architecture rules GA11 and GA12 check it), and the framework's
core services (pooling, assets, spawning, scenes, timers, logging) are used instead of hand-made copies (GA13).

## Start a new game by hand

1. Create an empty **Unity 6** project (6000.0 or later).
2. Add two lines to its `Packages/manifest.json`:

   ```json
   "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
   "com.aizekhan.rpg-framework": "https://github.com/Aizekhan/RPG-Framework.git?path=/Packages/com.aizekhan.rpg-framework#v0.12.0"
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
| `START-NEW-GAME.md` | The one file that starts a game with Claude Code |
| `STATUS.md` | What is verified, at which commit, and what is open |

Each release is a git tag (`v0.12.0`); pin one in the manifest so updates are deliberate.

## License

MIT, see `LICENSE`.
