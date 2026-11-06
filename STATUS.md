# RPG Framework - Project Status

**Last Updated:** 2026-11-06
**Release:** `v0.42.1` (2026-11-06; proof in `Docs/RELEASES.md`)
**Repository:** https://github.com/Aizekhan/RPG-Framework (public, MIT)
**Branch:** main (force-push and deletion blocked)
**Unity:** 6000.0.45f1 (`ProjectSettings/ProjectVersion.txt`; the Unity CLI version is kept in `CLAUDE.md` only)

This file is the **current state only**. History lives elsewhere and is not rewritten.

## Single sources of truth

Each fact has one owner. Every other mention repeats it and is checked against it by
`.github/scripts/check_docs.py` (CI job "Docs agree with their sources", or `python -I .github/scripts/check_docs.py . --local`).

| Fact | Owner | Repeated in (checked) |
|---|---|---|
| Package version | `Packages/com.aizekhan.rpg-framework/package.json` | CHANGELOG heading, `#vX.Y.Z` install tags, TARGET-ARCHITECTURE header, the Release line above, top row of `Docs/RELEASES.md` |
| What each version changed | package `CHANGELOG.md` | TARGET-ARCHITECTURE (by hand, in the release commit); the release feed `releases.json` (generated, checked) |
| Proof of each release (SHAs, tests, CI) | `Docs/RELEASES.md` (append-only) | nowhere else |
| Unity Editor version | `ProjectSettings/ProjectVersion.txt` | CLAUDE.md, this file, CI workflow |
| Unity CLI version | `CLAUDE.md` | nowhere else (`--local` compares it with `unity --version`) |
| What the framework can do (for WhiteWrite and agents) | package `capabilities.json` (`rpgframework.capabilities/1`, by hand; every release updates it, `release.py` writes its version and refuses an unchanged one for a version that adds things) | the check: its version and date, `since`, every `api`/`data` name in the code, every `ki` still open |
| What is open or missing | **Open issues** below (`KI-NN`) | a CHANGELOG line that fixes one names its KI; the check fails if that KI is still open |
| Codebase counts | `git ls-files` | the Codebase line below |
| API names in docs | the code | every backticked `Type` / `Type.Member` in current docs must exist in code |
| Session history | `Docs/archive/SESSION-LOG.md` | nowhere else |

A version bump goes through `python .github/scripts/release.py X.Y.Z`, which writes every copy at once and runs the check.

## Codebase (committed, from `git ls-files`)

- The framework is the embedded UPM package `Packages/com.aizekhan.rpg-framework`; install in other projects by git URL (see its README).
- **61 module folders**, **184 asmdef**, **1088 .cs files** in the package
- `Assets/ReferenceGame`: sample game (Goblin Valley) built only on framework assemblies
- `Assets/_Tests/Editor/FrameworkArchitectureTests.cs`: framework-wide rules (no game references, no editor code in player builds, no assembly cycles, `RPGFramework.*` namespaces, every MonoBehaviour/ScriptableObject in a file of its own name)
- No AI, Economy or Objectives modules exist (enemy AI lives in `ActionCombat`, plus the `BehaviorTree` module).
- Games on the framework and their gap lists: see the session process in `CLAUDE.md` (MythRunner, The Bloodline Hunt, the RPG-Games demos).

## Open issues

The register of everything known to be open, missing or limited. One `KI-NN` per bullet; ids are never reused.
When a change closes one, its CHANGELOG line names the KI and the bullet is deleted here in the same commit.
Next id: **KI-26**.

**Medium**
- **KI-01** TurnBased: no content assets (the game fills `ConfigureGame` in code); items only in battle; no auto-battle setting for the party
- **KI-02** Consumable effect ids have no shared meaning across genres (`ItemEffects` in TurnBased, `ConsumableEffects` in the kit); TurnBased keeps its own ability executor and status effect manager (its callbacks need the battle) instead of the ones `AbilitiesModule`/`StatusEffectsModule` register
- **KI-03** UI panels are thin defaults (`EquipmentPanel` hard-codes 8 slot types; `EquipmentSlotsPanel` takes the game's list)
- **KI-04** `ICharacter.Inventory`/`Progression` are `object` (keeps Characters free of those modules)
- **KI-05** Internet co-op (Unity Relay module) is proven only against a fake service; the live two-network test needs the owner's UGS project (host creates → friend joins by code → both see and interact → disconnect and rejoin → bad connection). No host migration (by design for now). See `Documentation~/networking.md`
- **KI-06** Tests missing: the `Editor` module has none (`FrameworkValidation` included); `AssetManagement.Addressables` (`AddressablesAssetProvider`) has no automated test. `Cards` is tested in `Dice/Tests` (`DiceAndDecksTests`)

**Low**
- **KI-07** Dialogue: several active dialogues on one entity act on the first
- **KI-08** Abilities: `CanMoveWhileCasting` is only a field; the game decides movement while casting
- **KI-09** No architecture rule for "module added but unused" (GA14 catches hand-made loot instead); `SendLatestToAll` sends whole messages (no `DeltaCompression` on top yet)
- **KI-10** `EventBusPerformanceTests.AThousandEventsAFrame_AllocateNothing` is flaky locally (no Retry); the ECS movement test has `[Retry(3)]`
- **KI-11** ReferenceGame art is generated placeholders (primitives)
- **KI-12** Co-op: the host checks positions against move speed horizontally only (height is trusted); networking keeps running while the game is paused; guests do not see elite names
- **KI-13** CI: the licence file is bound to another machine, so GameCI falls back to email/password activation; PlayMode sometimes fails on activation with no results file (rerun the job)
- **KI-14** `EnemyDirector` steers straight with separation; no pathfinding hook
- **KI-15** The TurnBased battle log is English only (`BattleRefusal` covers refusals only)
- **KI-16** The action RPG kit has no companions and no menu screen kit
- **KI-17** Performance: `StatContainer`, ECS, EventBus, `ActorWorld` and ActionCombat are measured; a scene with 1000 Unity views is not
- **KI-18** Editor tools: no visual graph windows for dialogue, quests or skill trees (the checks exist: `DialogueGraph`, `QuestGraph`); no combat balancing simulator
- **KI-19** Not built: procedural generation (dungeons, quests), boss AI with phases, cloud saves (`ICloudSaveProvider` has no implementation)
- **KI-20** Release extras missing: API reference, tutorials, CONTRIBUTING, issue templates, Asset Store submission
- **KI-21** Assets imported outside `Assets/ThirdParty/<Source>/<Asset>/` are not detected by GA15
- **KI-22** MythHunter's parallel system registry is not ported (Unity API is main-thread only; no proven need)
- **KI-24** ActionCombat: enemies do not parry (`EnemyBehaviour` has only `BlockChance` and `DodgeChance`; parry is for the player and the game's own AI)
- **KI-25** Telemetry: the queue lives in memory (events not yet sent are lost if the game crashes); `HttpTelemetryTransport` uses .NET `HttpClient`, so a WebGL build cannot send (it needs a transport of its own)

## Next steps

1. Whatever the games record in their `FRAMEWORK-GAPS.md` (read at every session start, see `CLAUDE.md`). MythRunner's next step on its side: the v0.34 `ImpactDelay` for Shaya's track strike.
2. 1.0 prep (public API review, KI-20, a stable-API note) → tag 1.0 only on the owner's explicit yes.
3. KI-05 when the owner provides a UGS project.
