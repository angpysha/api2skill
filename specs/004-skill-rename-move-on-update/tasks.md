# Tasks: Skill Rename/Move During Update

**Input**: Design documents from `specs/004-skill-rename-move-on-update/`

**Tests**: Included — constitution test-first default applies.

**Depends on**: Feature 003 complete (manifest + `UpdateCommand` + integration tests).

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [ ] T001 [P] Confirm feature branch `feature/004-skill-rename-move-on-update` and update `.specify/feature.json` → `specs/004-skill-rename-move-on-update`
- [ ] T002 [P] Link beads parent `api2skill-4zx` to this spec (comment + child implementation issue)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-directory preservation hook — both user stories need it for moves; rename-only can ship without it but implement together for one `SkillWriter` change.

### Tests for Foundational ⚠️

- [ ] T003 [P] Unit test: when `SkillWriter.Write` target dir ≠ preserve source dir, `secrets.json`/`auth.json`/`.auth-cache.json` bytes from source appear in final output in `tests/Api2Skill.Tests/Output/SkillWriterTests.cs` (new or extend existing if present)

### Implementation for Foundational

- [ ] T004 Extend `SkillWriter.Write` to accept optional preserve-from-source directory (or explicit preserved byte buffers from caller) so cross-directory moves copy credential files before atomic finalize in `src/Api2Skill/Output/SkillWriter.cs` (depends on T003)
- [ ] T005 Add internal helper on `UpdateCommand` (or small `SkillRelocate` type in `Output/`) to resolve/normalize source vs target paths and pre-read preserve set when paths differ in `src/Api2Skill/Cli/UpdateCommand.cs` (depends on T004)

**Checkpoint**: Cross-directory byte preservation works in isolation.

---

## Phase 3: User Story 1 - Rename during update (Priority: P1) 🎯 MVP part 1

**Goal**: `update <path> [<spec>] --name <new>` regenerates in place with new name in output + manifest.

**Independent Test**: generate with `--name foo`; run `update <path> <new-spec> --name bar`; assert manifest `name` is `bar`, SKILL.md reflects `bar`, secrets preserved.

### Tests for User Story 1 ⚠️

- [ ] T006 [P] [US1] CLI test: `update --name` with no `--out` succeeds and rewrites manifest name in `tests/Api2Skill.Tests/Cli/UpdateCommandTests.cs`
- [ ] T007 [P] [US1] Integration test: rename-only preserves `secrets.json`, `auth.json`, `.auth-cache.json` byte-identical in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs`

### Implementation for User Story 1

- [ ] T008 [US1] Add `--name` option to `UpdateCommand.Create()`; pass resolved name into `GenerateOptions` in `RunAsync` in `src/Api2Skill/Cli/UpdateCommand.cs` (depends on T005)
- [ ] T009 [US1] Regression test: `update` without `--name`/`--out` still matches 003 behavior in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs` (depends on T008)

**Checkpoint**: Rename-only works; 003 regression green.

---

## Phase 4: User Story 2 - Relocate during update (Priority: P1) 🎯 MVP part 2

**Goal**: `update <path> [<spec>] --out <new-dir>` moves skill with preserved credentials and removes source dir.

**Independent Test**: populate credential files; `update ./old <spec> --out ./new`; assert `./new` complete, `./old` gone, files preserved.

### Tests for User Story 2 ⚠️

- [ ] T010 [P] [US2] CLI test: `--out` collision with existing foreign directory fails clearly in `tests/Api2Skill.Tests/Cli/UpdateCommandTests.cs`
- [ ] T011 [P] [US2] Integration test: move preserves credentials and deletes source directory in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs`
- [ ] T012 [P] [US2] Integration test: `--out` equal to `<skill-path>` (normalized) behaves as in-place update in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs`

### Implementation for User Story 2

- [ ] T013 [US2] Add `--out` option to `UpdateCommand.Create()`; wire relocate orchestration (pre-flight, generate to target, source delete) in `RunAsync` in `src/Api2Skill/Cli/UpdateCommand.cs` (depends on T004, T005)
- [ ] T014 [US2] Surface best-effort warning when source delete fails after successful write (edge case) in `src/Api2Skill/Cli/UpdateCommand.cs` (depends on T013)

**Checkpoint**: Move works independently; US1 + US2 both green.

---

## Phase 5: User Story 3 - Rename and relocate together (Priority: P2)

**Goal**: `--name` + `--out` in one invocation.

### Tests for User Story 3 ⚠️

- [ ] T015 [P] [US3] Integration test: combined `--name` + `--out` produces correct name at new path with preserved credentials and no source dir in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs`

### Implementation for User Story 3

- [ ] T016 [US3] Verify combined flags path uses both resolved name and target dir (likely already covered by T008+T013 — add explicit test-first gate, fix gaps only) in `src/Api2Skill/Cli/UpdateCommand.cs` (depends on T013)

**Checkpoint**: All three user stories independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T017 [P] Update `README.md` with `update --name` / `update --out` examples
- [ ] T018 [P] Close or update beads `api2skill-4zx` when implementation merges (keep open until then)
- [ ] T019 Run full test suite + dev gate; confirm zero new warnings

---

## Dependencies & Execution Order

```text
Setup (T001–T002)
  → Foundational (T003–T005) blocks US2/US3 move paths
  → US1 rename (T006–T009) can parallelize tests with Foundational but T008 depends on T005
  → US2 move (T010–T014) depends on Foundational
  → US3 combined (T015–T016) depends on US1 + US2
  → Polish (T017–T019)
```

## Implementation Strategy

**MVP** = Setup + Foundational + US1 + US2 (rename and move — the two P1 stories from spec).
US3 is a thin combination check. Polish follows immediately given small surface area.

## Parallel Tracks

| Track | Tasks | Notes |
|-------|-------|-------|
| A — SkillWriter preserve | T003, T004 | Can start immediately after Setup |
| B — UpdateCommand CLI | T006–T008, T010–T014 | Tests first; implementation after T005 |
| C — Docs/beads | T001, T002, T017, T018 | Planning/docs parallel to code |

Tracks A and B merge at T013 (`UpdateCommand` relocate wiring).
