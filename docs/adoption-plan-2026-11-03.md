# Adoption Plan

> **Generated**: 2026-11-03
> **Project phase**: Production (inferred: 85 C# files under `Assets/Scripts`; no `project.stage` set)
> **Engine**: Not configured (code is Unity; `project.yaml` has no `engine.name`, `CLAUDE.md` still imports the Godot reference)
> **Template version**: v1.2.0

Work through these steps in order. Check off each item as you complete it.
Re-run `/adopt` anytime to check remaining gaps.

**Audit scope**: `workflow: minimal`. Only `design/game-brief.md`, the engine reference row and the project config were audited.
**Not audited**: GDDs, ADRs, stories and `systems-index.md` (none exist, and they are not expected at `minimal`), the v1.0 migration check found nothing to migrate (`project.yaml` exists). `design/game-brief.md` could not be format-checked because it does not exist.

---

## Step 1: Fix Blocking Gaps

None.

---

## Step 2: Fix High-Priority Gaps

### 2a. Engine not configured
`project.yaml` has no `engine.*`; `.claude/docs/technical-preferences.md` is all `[TO BE CONFIGURED]`; `CLAUDE.md` imports `docs/engine-reference/godot/VERSION.md` although the project is Unity. ADR and engine-aware skills would work from the wrong engine.
Fix: `/setup-engine unity <version>` (version from `ProjectSettings/ProjectVersion.txt`). This also rewrites the engine import in `CLAUDE.md` to the Unity reference (`docs/engine-reference/unity/` already exists).
**Time**: 5 min
- [ ] Engine configured in `project.yaml`
- [ ] `CLAUDE.md` imports the Unity version reference

### 2b. No game brief
`design/game-brief.md` is the design record at `minimal`. Without it `/create-stories` has nothing to break down.
Fix: `/brainstorm` (writes a one-page brief) or write `design/game-brief.md` by hand from `.claude/docs/templates/game-brief.md`. RuneArena already exists, so describe what is built: core loop, MVP, out of scope, build order.
**Time**: 30 min
- [ ] `design/game-brief.md` exists with the six required fields filled in

---

## Step 3: Bootstrap Infrastructure

Not on the minimal path — the brief's build order is the plan. Next: `/create-stories` (or `/dev-story` once stories exist).

---

## Step 4: Medium-Priority Gaps

### 4a. Naming conventions not defined
No `naming.*` in `project.yaml`. Set when configuring the engine, or edit `project.yaml` / `/settings naming.<key>=<value>`.
**Time**: 5 min
- [ ] Naming conventions set

### 4b. Performance budgets not defined
No `performance.*` (target framerate, frame budget, draw calls, memory ceiling). This is a VR game, so the framerate target matters for `/perf-profile`.
Fix: `/settings performance.<key>=<value>`.
**Time**: 5 min
- [ ] Performance budgets set

---

## Step 5: Optional Improvements

### 5a. Forbidden Patterns / Allowed Libraries empty
Both start empty by design in `.claude/docs/technical-preferences.md`. Fill in as the team agrees on them.
- [ ] Reviewed

---

## What to Expect from Existing Stories

No stories exist yet. Once created, existing stories continue to work with all template skills. New format checks (TR-ID validation, manifest version staleness) auto-pass when the fields are absent. Do not regenerate stories that are in progress or done.

---

## Re-run

Run `/adopt` again after completing Step 2 to verify all blocking and high gaps are resolved. The new run will reflect the current state of the project.
