# Archived plans

Historical planning documents from 2026-10-25/26. They describe the project before the audit and the MythHunter
migration, so many of their claims and paths (`Assets/_Framework`, "999+ errors", EPIC 25 modules) are out of date.
`STATUS.md` in the repository root is the single source of truth.

| File | What it was | Outcome |
|---|---|---|
| `ARCHITECTURE-GAP-ANALYSIS.md` | MythHunter core vs framework, migration plan (§6) | plan completed, see STATUS.md "Core migration" |
| `EPIC-25-INFRASTRUCTURE-PLAN.md` | DI, scenes, systems, resources, debug, time, pooling modules | superseded by the migration (Scenes, Systems, AssetManagement, Diagnostics, Timers, Pooling); lazy DI injector landed in `DI/Runtime` |
| `FRAMEWORK-MIGRATION-AUDIT.md` | diagnosis of the first transfer from MythHunter | fixed in the audit (STATUS.md "Fixed in the audit") |
| `RPG-FRAMEWORK-ROADMAP.md` | phases 0-6 and KPIs | phases 0-3 done in another form (Goblin Valley, Game Kit); the still-open ideas are listed in STATUS.md "Backlog from archived plans" |
