---
description: Start a session - read the project's state and propose the next step
---
Start a work session on this game.

1. If `SETUP.md` has an unfinished step, continue setup from it (`Docs/START-NEW-GAME.md`, or `START-NEW-GAME.md`
   before step 10) and stop here.
2. Read `CLAUDE.md`, `STATUS.md`, `GAME.md`, `Docs/ARCHITECTURE.md`, the last 10 lines of `DEVLOG.md` and the
   `## Open` sections of `FRAMEWORK-GAPS.md`.
3. Run `git status` and `git log --oneline -10`. Compare with `STATUS.md`: the commits it names exist, and nothing
   uncommitted is unexplained. Do not change, stash or discard files you did not write; ask about them.
4. Framework version: the update check hook (`.claude/hooks/framework-update-check.sh`) has already told you at the
   start of the session when newer versions exist, with each one's summary and whether it is breaking. If it said
   nothing because it did not run, compare the tag in `Packages/manifest.json` with
   `git ls-remote --tags --refs <the repository in that line>`. When a newer one exists, say so with what it brings
   (`releases.json` and `CHANGELOG.md` at the newest tag), and which open gaps it may close; update when the developer
   agrees, or right away when they gave standing permission to update (say so in `CLAUDE.md`) ("Updating the
   framework" in `Docs/START-NEW-GAME.md`).
5. When the documents contradict each other or the code, say where and settle it with the developer before coding.
6. Report in a few lines: milestone, feature in progress, last verified tests (with commit), open gaps, blockers,
   and the next step you propose. Wait for a yes before you start it.
