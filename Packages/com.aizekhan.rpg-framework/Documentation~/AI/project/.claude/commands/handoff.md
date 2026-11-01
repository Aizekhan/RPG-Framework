---
description: End a session or a feature - record the state so the next session can continue
---
Record the state of the work so the next session continues without asking.

1. Run the tests if code changed since the last run (`CLAUDE.md`, "This project"). Note passed/total and the commit.
   Never record a result for a commit it was not measured on.
2. Rewrite `STATUS.md`: milestone, in progress, next step, last verified table, framework version and open gaps,
   open questions, anything left uncommitted on purpose.
3. Add a line to `DEVLOG.md` for every finished step (date, what, commit, tests).
4. Update the feature statuses and decisions in `GAME.md`.
5. If the structure changed (assembly, module, system, event, content asset type, saved part, stand-in), update
   `Docs/ARCHITECTURE.md`.
6. Check `FRAMEWORK-GAPS.md`: every `// framework-gap: <id>` marker has its section, and resolved gaps are under
   `## Resolved`.
7. Commit the documents (`docs: status and handoff`), naming the files, never `git add -A` over files you did not
   change.
8. Report in a few lines: what changed, which commit was verified and how, what is next.
