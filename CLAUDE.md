# Steroid Guide — tModLoader Mod

A Terraria tModLoader mod adding a town NPC that analyzes player inventory + nearby chests to show all craftable items with recipe tree visualization.

**Stack:** C# / .NET 8.0 / tModLoader / XNA (MonoGame)
**Build:** `dotnet build` via tModLoader targets. Test in-game (no unit test framework).

## Harness: Steroid Guide

**Goal:** Analysis → implementation → review pipeline for code changes to ensure correctness in a game mod context where runtime testing requires in-game verification.

**Agent Team:**

| Agent | Role |
|-------|------|
| sg-analyst | Analyzes change requests, identifies affected systems, produces implementation plans |
| sg-developer | Implements changes across all layers (recipe analysis, UI, networking, content) |
| sg-reviewer | Reviews for tModLoader compliance, multiplayer safety, performance, correctness |

**Skills:**

| Skill | Purpose | Used By |
|-------|---------|---------|
| steroid-guide | Development pipeline orchestrator (analyze → implement → review) | All agents |

**Execution Rules:**
- For non-trivial code changes, use the `steroid-guide` skill to run the full pipeline
- Simple questions, single-line fixes, or exploratory reads can be handled directly
- All agents use `model: "opus"`
- Intermediate artifacts: `_workspace/` directory

**Directory Structure:**
```
.claude/
├── agents/
│   ├── sg-analyst.md
│   ├── sg-developer.md
│   └── sg-reviewer.md
└── skills/
    └── steroid-guide/
        ├── SKILL.md
        └── references/
            └── architecture.md
```

**Change History:**

| Date | Change | Target | Reason |
|------|--------|--------|--------|
| 2026-04-08 | Initial harness build | All | New harness for tModLoader mod development |
