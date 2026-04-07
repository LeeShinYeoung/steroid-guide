---
name: steroid-guide
description: "Orchestrates the Steroid Guide tModLoader mod development pipeline: analysis, implementation, and review. Use for any code change to the mod — bug fixes, new features, performance optimization, refactoring, UI changes, multiplayer fixes, recipe analysis changes, NPC modifications. Also handles: update, modify, fix, improve, re-run, partial re-run, iterate on feedback, review previous changes, revert, steroid guide 코드 수정, 버그 수정, 기능 추가."
---

# Steroid Guide Development Orchestrator

Coordinates analysis, implementation, and review for code changes in the Steroid Guide tModLoader mod.

## Execution Mode: Sub-agent

## Agent Composition

| Agent | subagent_type | Role | Output |
|-------|--------------|------|--------|
| sg-analyst | sg-analyst | Analyze change request, identify affected systems, produce plan | `_workspace/01_analyst_plan.md` |
| sg-developer | sg-developer | Implement changes following the plan | Modified files + `_workspace/02_developer_changes.md` |
| sg-reviewer | sg-reviewer | Review changes for correctness | `_workspace/03_reviewer_feedback.md` |

## Workflow

### Phase 0: Context Check

1. Check if `_workspace/` directory exists in the project root
2. Determine execution mode:
   - **No `_workspace/`** → Initial run. Proceed to Phase 1
   - **`_workspace/` exists + user requests partial fix** → Partial re-run. Skip to Phase 3 (developer) with reviewer feedback as input
   - **`_workspace/` exists + new change request** → New run. Rename existing to `_workspace_{YYYYMMDD_HHMMSS}/`, proceed to Phase 1

### Phase 1: Analysis

Spawn sg-analyst:

```
Agent(
  name: "sg-analyst",
  subagent_type: "sg-analyst",
  model: "opus",
  prompt: "Read your agent definition at .claude/agents/sg-analyst.md.
           Read the architecture reference at .claude/skills/steroid-guide/references/architecture.md.
           Analyze the following change request: {user_request}.
           Explore the affected source files. Write your analysis to _workspace/01_analyst_plan.md."
)
```

After completion:
- Read `_workspace/01_analyst_plan.md`
- Present the plan summary to the user
- Wait for user approval before proceeding to Phase 2

### Phase 2: Implementation

Spawn sg-developer:

```
Agent(
  name: "sg-developer",
  subagent_type: "sg-developer",
  model: "opus",
  prompt: "Read your agent definition at .claude/agents/sg-developer.md.
           Read the analyst's plan at _workspace/01_analyst_plan.md.
           Implement all changes described in the plan.
           Write a change summary to _workspace/02_developer_changes.md."
)
```

### Phase 3: Review

Spawn sg-reviewer:

```
Agent(
  name: "sg-reviewer",
  subagent_type: "sg-reviewer",
  model: "opus",
  prompt: "Read your agent definition at .claude/agents/sg-reviewer.md.
           Read _workspace/02_developer_changes.md for the change summary.
           Run 'git diff' to see all actual code changes.
           Read the full context of each modified file.
           Write your review to _workspace/03_reviewer_feedback.md."
)
```

After completion, read `_workspace/03_reviewer_feedback.md`:
- **PASS** → Report success to user. Proceed to Phase 5
- **PASS WITH FIXES** → Present warnings to user, ask if they want fixes applied
- **FAIL** → Proceed to Phase 4

### Phase 4: Fix Iteration (max 2 rounds)

Re-spawn sg-developer with reviewer feedback:

```
Agent(
  name: "sg-developer",
  subagent_type: "sg-developer",
  model: "opus",
  prompt: "Read your agent definition at .claude/agents/sg-developer.md.
           Read reviewer feedback at _workspace/03_reviewer_feedback.md.
           Fix all Critical and Warning items.
           Update _workspace/02_developer_changes.md with the fixes applied."
)
```

Then re-run Phase 3 review. Maximum 2 fix-review iterations. If still failing after 2 rounds, report remaining issues to user for manual resolution.

### Phase 5: Completion

1. Preserve `_workspace/` directory (do not delete — useful for audit trail)
2. Report to user:
   - Summary of changes made
   - Files modified
   - Review verdict
   - Any remaining warnings or notes

## Data Flow

```
User Request → [sg-analyst] → plan.md → [sg-developer] → changes → [sg-reviewer] → feedback
                                              ^                            |
                                              └──── fix iteration (max 2) ─┘
```

## Error Handling

| Situation | Strategy |
|-----------|----------|
| Analyst cannot determine scope | Present ambiguity to user, ask for clarification |
| Developer encounters unexpected code state | Retry once with expanded context. If still failing, report to user |
| Reviewer finds critical issues | Developer fix iteration (max 2 rounds) |
| Fix iteration exhausted (2 rounds) | Report remaining issues to user for manual resolution |
| Agent fails to produce output file | Retry once. If still failing, proceed without that step and note the gap |

## Test Scenarios

### Normal Flow
1. User: "Add a new sort option to sort items by value"
2. Analyst identifies: SortCriteria enum, ApplyFilter sort logic, UISortButton, localization
3. Developer adds `Value` to SortCriteria, implements comparison using `CachedItemProps.Value`, adds hjson entry
4. Reviewer validates: enum consistency, sort stability, localization pattern, no performance regression
5. Result: PASS

### Error Flow
1. User: "Fix chest sync dropping items in multiplayer"
2. Analyst identifies: ChestSyncSystem, ItemScanner sync state, packet handling
3. Developer modifies ChestSyncSystem.PostUpdateWorld
4. Reviewer finds: missing null check on `Main.chest[pending.ChestIndex]` after dequeue → FAIL (Critical)
5. Developer adds null check with `continue` on null
6. Reviewer re-reviews → PASS
