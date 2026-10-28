# Start a new game on RPG Framework, with Claude Code

> **For the developer.** Make an empty folder for your game, put this one file in it, open Claude Code in that folder
> and type: **"Follow START-NEW-GAME.md"**. Claude Code will check your computer, create the Unity project, install
> the framework, generate a playable game, help you describe your game, and then build it with you feature by
> feature. You will be asked to do a few things only you can do (install Unity, press Play, decide what the game
> is). You can stop at any time; the next session continues where you left off.
>
> **Для розробника.** Створіть порожню папку для гри, покладіть туди цей файл, відкрийте в ній Claude Code і
> напишіть: **«Follow START-NEW-GAME.md»**. Далі Claude Code веде вас крок за кроком.

---

## For Claude Code: how to follow this file

You are helping a developer start and build a game on **RPG Framework** (`com.aizekhan.rpg-framework`, a Unity 6
package: https://github.com/Aizekhan/RPG-Framework). You have not seen it before; this file and the documents it
installs tell you everything you need. Follow these rules for the whole project:

1. **Speak the developer's language** (the one they write in). Keep the code, commits and project documents in
   English unless they ask otherwise.
2. **One step at a time, in order.** Before a step, say in one or two sentences what it does and why. After it,
   show the proof (command output, test counts, a file) and tick it in `SETUP.md`. Never claim a step is done
   without proof. If a step fails, find the cause; do not skip it and do not weaken a check to pass it.
3. **Keep `SETUP.md` up to date** (created in step 0): the checklist below with each step's state and its proof.
   At the start of every session, read `SETUP.md` (and after step 10 `CLAUDE.md`, `GAME.md`, `DEVLOG.md`) and
   continue from the first unfinished step.
4. **Ask only what only the developer can decide or do**: the game's name and idea, installing Unity or logging in,
   pressing Play and saying what they see, approving plans. Everything you can check yourself, check yourself.
5. **Unity runs in batch mode only when the project is closed in the Unity Editor** (an open Editor locks the
   project). Before a batch command, make sure it is closed, or ask the developer to close it. When the Editor is
   open, tests can run in its Test Runner (Window > General > Test Runner).
6. **Never edit the framework inside `Library/PackageCache`** (it is read-only and replaced on update). What the
   framework lacks is recorded in `FRAMEWORK-GAPS.md` (see the development loop).
7. **Use the framework's core services; never write your own** (see "Core services" below and
   `Docs/RPGFramework/recipes/14-core-services.md` once installed).

Commands below are for Git Bash or PowerShell on Windows; adapt paths on macOS/Linux. `<Unity>` is the full path
of the Unity 6 editor executable found in step 1; `<Game>` is the game's name in PascalCase.

### Core services — always use these

The game resolves them from the host's container (`Host.Container.Resolve<T>()`) or takes them as constructor
parameters in its rules. The architecture test GA13 fails on the hand-made versions.

| Need | Use | Not |
|---|---|---|
| Spawn/despawn many objects | `IPrefabSpawner` (`ConfigurePool`, `SpawnAsync`, `Despawn`) | `Instantiate`, `Destroy` |
| Pool plain C# objects | `ObjectPool<T>` registered in `IPoolManager` | a `Stack<T>` by hand |
| Load assets | `IAssetManager.LoadAsync<T>`, dispose the handle | `Resources.Load` |
| Change scenes | `ISceneDispatcher` | `SceneManager.LoadScene` |
| Delayed or repeating calls | `ITimerService` (`Schedule`, `Repeat`, `DelayAsync`) | `Invoke`, coroutines with `WaitForSeconds` |
| Per-frame logic, game phases | systems (`SystemBase`, `AddSystem<T>()`), `PhaseTracker` | `Update()` full of rules |
| Logs | `ILogger` | `Debug.Log` |
| Messages between features | the event bus (`Publish`, `Subscribe`) | direct calls, singletons |
| Entities, templates, queries | `IEntityManager`, `ArchetypeCatalog`, `GetEntitiesWith<T1, T2>(list)` | dictionaries of data |
| AI, paths | `StateMachine<T>`, `AsyncStateMachine<T>`, `BehaviorTree`, `AStarPathfinder` | hand-written FSM / A* |
| UI screens | `INavigationService`, `PrefabViewFactory` | toggling panels by hand |
| Saves | `SaveParticipant<T>` (recipe 12) | `PlayerPrefs`, own JSON files |

A line that truly needs the Unity call ends with `// framework-ok: <reason>`.

### Checklist (copy into `SETUP.md` in step 0)

```
- [ ] 0  Agree on the plan; game name
- [ ] 1  Tools: git, Unity Hub, Unity 6 editor
- [ ] 2  Unity project in this folder
- [ ] 3  Git repository and first commit
- [ ] 4  Framework and UniTask in Packages/manifest.json
- [ ] 5  Game generated (Game Kit, with AI guidance)
- [ ] 6  Tests pass (EditMode, PlayMode)
- [ ] 7  The developer plays it
- [ ] 8  GAME.md: what this game is
- [ ] 9  First milestone planned
- [ ] 10 Hand-off: CLAUDE.md knows this project
```

---

## Step 0. Agree on the plan

- Explain in a few lines what is about to happen (the checklist) and roughly how long: steps 1-7 take 20-40 minutes,
  most of it Unity importing.
- Ask for the game's working name and turn it into PascalCase letters and digits, starting with a letter
  (`Dragon Peak` → `DragonPeak`). It names the folders, assemblies and scene; it can be renamed later only by hand.
- Create `SETUP.md` with the checklist and the name. Tick step 0.

## Step 1. Tools

Check, and show the developer what you found:

- `git --version` (needed). If missing: ask them to install Git for Windows.
- Unity Hub and a **Unity 6 editor (6000.0.x)**. Editors can be installed anywhere, so ask Unity Hub's own list
  first: `%APPDATA%/UnityHub/editors-v2.json` (macOS: `~/Library/Application Support/UnityHub/editors-v2.json`)
  has each installed version with its `location`. Then the default folders
  `C:/Program Files/Unity/Hub/Editor/*/Editor/Unity.exe` (macOS:
  `/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity`). Pick the newest 6000.0.x whose file exists and
  confirm it with the developer. If there is none, ask them to install it from Unity Hub (Installs > Install Editor
  > Unity 6, LTS) and to sign in to Hub with a Unity account and activate a licence (Personal is free). Wait; then
  check again.
- Optional: `gh --version` (GitHub CLI, for pushing to GitHub later).

Write the editor path into `SETUP.md`. Tick step 1.

## Step 2. Create the Unity project here

The project lives in this folder (next to this file):

```bash
"<Unity>" -batchmode -quit -nographics -createProject "<absolute path of this folder>" -logFile Logs/create.log
```

Proof: `Assets/`, `Packages/manifest.json` and `ProjectSettings/` exist. If it failed, read `Logs/create.log` (a
licence problem means the developer must sign in to Unity Hub). Tick step 2.

## Step 3. Git

```bash
git init
git lfs install
```

(`git lfs install` once per machine; if `git lfs` is missing, ask the developer to install Git LFS from
https://git-lfs.com. Step 5 adds a `.gitattributes` that keeps models, textures and sounds in LFS.)

Create `.gitignore` with Unity's standard entries:

```
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
*.csproj
*.sln
*.suo
*.user
*.pidb
*.booproj
.vs/
.vscode/
.idea/
*.apk
*.aab
*.unitypackage
TestResults*.xml
```

Commit: `git add -A && git commit -m "chore: empty Unity 6 project"`. Ask whether they want a GitHub repository now
(`gh repo create <name> --private --source . --push`) or later. Tick step 3.

## Step 4. Install the framework

Find the newest release tag (the framework's versions are git tags `vX.Y.Z`):

```bash
git ls-remote --tags --refs https://github.com/Aizekhan/RPG-Framework.git
```

Pick the highest version. In `Packages/manifest.json`, add these two entries to `"dependencies"` (keep the rest):

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
"com.aizekhan.rpg-framework": "https://github.com/Aizekhan/RPG-Framework.git?path=/Packages/com.aizekhan.rpg-framework#vX.Y.Z"
```

Pinning a tag means the framework changes only when the developer decides (see "Updating the framework"). Tick
step 4 with the version in `SETUP.md`.

## Step 5. Generate the game

```bash
"<Unity>" -batchmode -quit -nographics -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewGameFromCommandLine -game <Game> -aiGuidance -logFile Logs/newgame.log
```

The first run downloads the packages and imports everything; it can take several minutes. Then check:

- `grep -c "error CS" Logs/newgame.log` is **0** (otherwise show the errors and fix the cause).
- `Assets/<Game>/` exists with `Scripts/Core`, `Content`, `Scenes`, `Tests`.
- `CLAUDE.md`, `Docs/RPGFramework/` (architecture guide and recipes), `GAME.md`, `FRAMEWORK-GAPS.md`, `DEVLOG.md`,
  `.gitattributes` and `.github/workflows/unity-tests.yml` exist.
- `ProjectSettings/ProjectSettings.asset` has `runInBackground: 1` (co-op), `m_ActiveColorSpace: 1` (Linear) and a
  `companyName` that is not `DefaultCompany` (New Game uses the game's name; pass `-company "<Studio>"` to choose).
- The CI workflow **fails** until the repository has the `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets
  (https://game.ci/docs/github/activation). Tell the developer; a red CI is the reminder, not a bug.

Now **read `CLAUDE.md` and `Docs/RPGFramework/architecture.md`** fully: they are the rules for all code from now on.
Skim the recipe titles in `Docs/RPGFramework/recipes/`. Tick step 5.

## Step 6. Run the tests

```bash
"<Unity>" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode-run.log
"<Unity>" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode-run.log
```

(No `-quit` with `-runTests`; Unity exits by itself, with code 2 if a test failed.) Read the NUnit XML: the root
`test-run` element has `total`, `passed`, `failed`. Proof: failed = 0 in both, and the EditMode total is not 0 (a
test assembly that failed to compile shows up as missing tests, not as a failure). A fresh game has about 14
EditMode tests and no PlayMode tests yet; PlayMode tests come with the first scene features. Write the counts into
`SETUP.md`; later runs must never have fewer tests than the last recorded count without a reason. Tick step 6.

## Step 7. The developer plays it

Ask the developer to open the project in Unity Hub (Add > Add project from disk > this folder), open the scene
`Assets/<Game>/Scenes/<Game>.unity` and press **Play**. They should see a hero fighting waves of goblins with a
HUD; WASD or arrows move, the mouse or Space attacks, I/Tab opens the character sheet, F2 opens co-op. Ask what they
see; if something is wrong, look at the Console with them. Then commit:
`git add -A && git commit -m "feat: <Game> generated from RPG Framework Game Kit"`. Tick step 7.

## Step 8. What is this game? (`GAME.md`)

`GAME.md` was created from a template. Fill it **with** the developer, one short question at a time, offering a
sensible default they can accept or change:

1. The pitch in one or two sentences.
2. Genre and view; single player or co-op (how many); PvP or not. A real-time action RPG starts from the generated
   game (`ActionRpg`); a turn-based party RPG uses the `TurnBased` rules (recipe 15: turns, abilities, effects, enemy
   AI, rewards and saves are done; the battle screen is the game's to build); other genres use the modules and the
   core services (recipe 14).
3. The core loop: what the player does every 30 seconds, every 10 minutes, every session.
4. Features in priority order. Push for a **small first milestone** that is playable on its own (a "vertical
   slice": one area, one enemy type more, one quest chain, one new rule), not the whole game.
5. What the game will deliberately not have.

Compare the features with what the framework already has (the package README's module list, recipe 07): note for
each feature which modules it will use. Something the framework lacks is not a reason to drop a feature; mark it
"framework gap" in the notes. Show the finished `GAME.md`, get a yes, commit. Tick step 8.

## Step 9. Plan the first milestone

For each feature of the first milestone, write a short plan in `GAME.md` (or ask whether they prefer a separate
`PLAN.md`): which framework modules and assets it uses, what goes in `<Game>.Core` (rules), what in views, which
content assets designers will edit, which tests prove it, and any framework gap. Get the developer's approval.
Tick step 9.

## Step 10. Hand-off

Add a section **outside** the framework block of `CLAUDE.md` (the block between the `rpg-framework:begin/end`
markers is replaced on framework updates):

```markdown
# This project
- Game: <Game>. Unity editor: <Unity>. Framework: com.aizekhan.rpg-framework <version> (Packages/manifest.json).
- Read at the start of every session: GAME.md, DEVLOG.md (last entries), FRAMEWORK-GAPS.md (open gaps).
- Tests: <the two commands from step 6>. Close the Unity Editor first, or use its Test Runner.
- Work one feature at a time with the development loop in START-NEW-GAME.md (kept in Docs/).
```

Move this file to `Docs/START-NEW-GAME.md` (it stays the reference for the development loop), tick step 10, commit,
and tell the developer the setup is complete and what the first feature will be.

---

## The development loop (every feature, after setup)

1. **Pick** the next feature from `GAME.md` and re-read its plan. Re-read the recipe that fits
   (`Docs/RPGFramework/recipes/`).
2. **Look in the framework first.** The package README lists the modules; the source is in
   `Library/PackageCache/com.aizekhan.rpg-framework*`. Use what exists: combat, stats, progression, quests,
   inventory, items, loot, dialogue, crafting, factions, persistence, UI navigation, spawning, pooling, timers,
   co-op networking. For plumbing (spawning, pooling, assets, scenes, timers, logging, state machines, paths, UI
   screens) follow recipe 14: the core services above, never a copy of them.
3. **If something is missing, never cut the corner.** Do not rebuild the module inside the game, do not shrink the
   feature, do not fake it. Tell the developer what is missing. Add a section `## <id>` to `FRAMEWORK-GAPS.md`
   (needed for, missing, wanted API, stand-in). Write the smallest stand-in behind an interface in `<Game>.Core`
   with `// framework-gap: <id>` on the line above it, with tests. For a whole missing system, stop and ask whether
   to add it to the framework first. The architecture tests enforce this: GA11 fails when the game declares a type
   the framework already has, GA12 fails when a gap marker has no section.
4. **Generate the structure** instead of writing it by hand (Editor closed):
   `"<Unity>" -batchmode -quit -nographics -projectPath . -executeMethod RPGFramework.GameKit.Editor.GameScaffold.NewFeatureFromCommandLine -game <Game> -feature <Feature> -logFile Logs/feature.log`
5. **Rules first, with tests** in `<Game>.Core` (no UnityEngine): an EditMode test that boots `GameHost` with the
   game's composition. Then the **view** and input in the `<Game>` assembly. Then the **content** as assets
   (Game Setup, Characters, items, loot tables, quests) that a designer can edit in the inspector.
6. **Run all tests** (step 6 commands). Everything green, including `<Game>ArchitectureTests`. Never delete or
   weaken a test to make it pass; fix the code.
7. **The developer plays it** and says whether it does what they wanted. Adjust.
8. **Record and commit**: a line in `DEVLOG.md`, the feature's status in `GAME.md`, then
   `git commit -m "feat: <what the player can now do>"` (conventional commits: feat, fix, test, docs, chore).
9. Every few features, **review `FRAMEWORK-GAPS.md`** with the developer: gaps that every RPG would need are
   candidates for the framework itself (with the game's stand-in as proof); game-specific ones stay in the game.

## Updating the framework

When the developer wants a newer version: list the tags (step 4), read that version's `CHANGELOG.md` on GitHub,
change the tag in `Packages/manifest.json`, open the project once (or run step 5's import by running the tests),
run all tests, and reinstall the AI guidance so `CLAUDE.md`'s framework block and `Docs/RPGFramework` match the new
version (`-executeMethod RPGFramework.GameKit.Editor.AIGuidance.InstallFromCommandLine`; the game's own documents
are kept). Then go through **every** open gap in `FRAMEWORK-GAPS.md` against the changelog: a gap the new version
covers is replaced with the framework's API, its stand-in and `// framework-gap` marker deleted, and its section moved
to "Resolved" with the version (`CLAUDE.md`, "After the framework is updated").

## When something goes wrong

| Symptom | What to do |
|---|---|
| Batch command exits at once, log says the project is open | The Editor has it open: ask the developer to close it |
| `error CS` in a log | Show the first errors with file and line; fix the cause in the game's code |
| Tests: total is lower than before, or 0 | A test assembly did not compile or load: search the log for `error` near the assembly's name |
| Licence or sign-in errors | The developer signs in to Unity Hub and activates a licence |
| A `.meta` is missing or Unity ignores a file | Let Unity import (open the Editor or run a batch command); never invent a GUID |
| An architecture test fails | Its message names the rule (GA01-GA13) and the fix; `Docs/RPGFramework/architecture.md` explains it |
