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
4. Check the framework tag in `Packages/manifest.json` against the newest tag
   (`git ls-remote --tags --refs https://github.com/Aizekhan/RPG-Framework.git`). If a newer one exists, say so;
   update only when the developer agrees ("Updating the framework" in `Docs/START-NEW-GAME.md`).
5. When the documents contradict each other or the code, say where and settle it with the developer before coding.
6. Report in a few lines: milestone, feature in progress, last verified tests (with commit), open gaps, blockers,
   and the next step you propose. Wait for a yes before you start it.
