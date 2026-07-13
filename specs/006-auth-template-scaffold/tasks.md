# Tasks: Auth Template Scaffold & Script Working Directory

**Input**: Design documents from `specs/006-auth-template-scaffold/`

**Beads**: Epic `api2skill-006-auth-scaffold-h9p` — P2 `.1`, US1 `.2`, US2 `.3`, US3 `.4`, Polish `.5`

**Tests**: Included — constitution test-first default applies.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [x] T001 [P] Add `specs/006-auth-template-scaffold/contracts/` cross-links to `wiki/Authentication.md` and `wiki/Generate-Command.md` (stub anchors — filled in T024)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Guidance types + scaffold builder — both US1 and US2 depend on this.

- [x] T002 [P] Define `AuthScaffoldGuidance`, `SchemeGuidanceEntry`, `SchemeScaffoldStatus`, `TagAttachExample`, and `AuthScaffoldResult` records in `src/Api2Skill/Auth/AuthScaffoldGuidance.cs` (data-model.md §1)
- [x] T003 Extend `SkillModel` with optional `AuthScaffoldGuidance? AuthScaffoldGuidance` in `src/Api2Skill/Model/SkillModel.cs` and thread through `SkillModelBuilder.Build` return (depends on T002)
- [x] T004 Implement `AuthScaffold.Build(SkillModel model)` — scheme→profile mapping (research.md R3), `_guidance`, `_tagAttachExamples`, active `profiles` only in `src/Api2Skill/Auth/AuthScaffold.cs` (depends on T002)
- [x] T005 [P] Unit tests: bearer/basic/header-apiKey/oauth2 mapping; unsupported/query-apiKey omitted from profiles but listed in `_guidance`; active profiles pass `AuthConfigLoader.Load` in `tests/Api2Skill.Tests/Auth/AuthScaffoldTests.cs` (depends on T004)
- [x] T006 [P] Unit tests: profile `name` equals scheme ID; no literal secrets in output in `tests/Api2Skill.Tests/Auth/AuthScaffoldTests.cs` (extend — SC-004)

**Checkpoint**: `AuthScaffold.Build` produces valid inactive template JSON + guidance model.

---

## Phase 3: User Story 1 - Scaffold auth.json from OpenAPI (Priority: P1) 🎯 MVP

**Goal**: First `generate` without explicit auth writes `auth.json` into the skill folder when the
spec has security schemes.

**Independent Test**: quickstart.md §1 — `multi-auth.yaml` → skill dir contains scaffolded
`auth.json`.

### Tests for User Story 1 ⚠️

- [x] T007 [P] [US1] CLI test: generate without auth flags writes `auth.json` when spec has schemes in `tests/Api2Skill.Tests/Cli/AuthScaffoldCliTests.cs`
- [x] T008 [P] [US1] CLI test: generate against auth-less spec writes no `auth.json` in `tests/Api2Skill.Tests/Cli/AuthScaffoldCliTests.cs` (extend)
- [x] T009 [P] [US1] CLI test: `--force` with existing `auth.json` preserves bytes (no re-scaffold) in `tests/Api2Skill.Tests/Cli/AuthScaffoldCliTests.cs` (extend)
- [x] T010 [P] [US1] Integration test: scaffold + edit secrets + `generate --force --auth-config` succeeds in `tests/Api2Skill.Tests/Integration/AuthScaffoldIntegrationTests.cs`

### Implementation for User Story 1

- [x] T011 [US1] Extend `SkillWriter.Write` with optional `scaffoldAuthJson` parameter: write when `authConfigJson` is null, `scaffoldAuthJson` non-null, and no preserved auth in `src/Api2Skill/Output/SkillWriter.cs` (depends on T004)
- [x] T012 [US1] In `GenerateCommand.RunAsync`, when no `--auth`/`--auth-config`, call `AuthScaffold.Build`, pass JSON to `SkillWriter`, attach guidance to model for writers in `src/Api2Skill/Cli/GenerateCommand.cs` (depends on T003, T004, T011)
- [x] T013 [US1] Ensure scaffold path does **not** set `BuildOptions.AuthConfig` / `model.AuthConfig` (inactive template only) in `GenerateCommand` (depends on T012)

**Checkpoint**: User Story 1 independently testable.

---

## Phase 4: User Story 2 - Profile naming guidance (Priority: P1)

**Goal**: `SKILL.md` **Auth profile names** section + `_guidance` in `auth.json`.

**Independent Test**: quickstart.md §1 — SKILL.md lists scheme→profile mapping.

### Tests for User Story 2 ⚠️

- [x] T014 [P] [US2] Unit test: `SkillMdWriter` emits **Auth profile names** when `model.AuthScaffoldGuidance` is set in `tests/Api2Skill.Tests/Emit/SkillMdWriterScaffoldTests.cs`
- [x] T015 [P] [US2] Unit test: shared scheme ID appears once in guidance with aggregated operation IDs in `tests/Api2Skill.Tests/Auth/AuthScaffoldTests.cs` (extend)

### Implementation for User Story 2

- [x] T016 [US2] Add **Auth profile names** section to `SkillMdWriter.Write` when `AuthScaffoldGuidance` present (table: scheme, profile name, status, ops/tags) in `src/Api2Skill/Emit/SkillMdWriter.cs` (depends on T003)
- [x] T017 [US2] Include activation one-liner (`--auth-config` + `--force`) in SKILL.md section per contracts/auth-scaffold.md (depends on T016)

**Checkpoint**: User Stories 1 + 2 complete.

---

## Phase 5: User Story 3 - Script auth skill-root cwd (Priority: P2)

**Goal**: Script auth commands run with `WorkingDirectory` = skill root.

**Independent Test**: quickstart.md §5 — sentinel file in skill root regardless of caller cwd.

### Tests for User Story 3 ⚠️

- [x] T018 [P] [US3] Integration test: script command `touch .script-cwd-sentinel` creates file in skill root when dispatcher invoked from different cwd in `tests/Api2Skill.Tests/Integration/DispatcherScriptAuthTests.cs` (extend)
- [x] T019 [P] [US3] Golden/snapshot: emitted `RunScriptCommandAsync` sets `WorkingDirectory` in `tests/Api2Skill.Tests/Emit/AuthEngineGoldenTests.cs` (extend)

### Implementation for User Story 3

- [x] T020 [P] [US3] Thread `skillRoot` into `RunScriptCommandAsync` and set `WorkingDirectory` in `src/Api2Skill/Emit/CsFileEmitter.cs` (contracts/dispatcher-script-auth.md)
- [x] T021 [P] [US3] Same cwd fix in `src/Api2Skill/Emit/FsxEmitter.cs` (parallel with T020)
- [x] T022 [P] [US3] Same cwd fix in `src/Api2Skill/Emit/CsxEmitter.cs` (parallel with T020)

**Checkpoint**: All user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting

- [x] T023 [P] Extend `NoSecretLeakageTests` for scaffold output paths in `tests/Api2Skill.Tests/Auth/NoSecretLeakageTests.cs`
- [x] T024 [P] Update `wiki/Authentication.md` and `wiki/Generate-Command.md` with auto-scaffold workflow and profile-naming rules
- [x] T025 [P] Update `README.md` with one auto-scaffold example
- [x] T026 [P] Regenerate approved golden fixtures (`petstore-cs`/`petstore-fsx`/`petstore-csx`) after emitter cwd change
- [x] T027 Run full test suite (`dotnet test tests/Api2Skill.Tests`) and quickstart.md manual smoke

---

## Dependencies & Execution Order

```text
Phase 1 (T001)
    ↓
Phase 2: T002 → T003,T004 → T005,T006 [P]
    ↓
Phase 3 US1: T007–T010 [P] tests → T011 → T012 → T013
    ↓
Phase 4 US2: T014,T015 [P] → T016 → T017     } US2 can start after T003 (parallel with US1 impl after T011)
    ↓
Phase 5 US3: T018,T019 [P] → T020,T021,T022 [P]
    ↓
Phase 6: T023–T027 [P mostly]
```

### Parallel opportunities

| After | Parallel batch |
|-------|----------------|
| T004 | T005 + T006 + T007 + T008 + T009 |
| T011 | T014 + T015 + T018 + T019 (tests) |
| T020 | T021 + T022 (three emitters) |
| Phase 6 | T023 + T024 + T025 + T026 |

### User story independence

- **US1 MVP**: Phase 2 + Phase 3
- **US2**: Phase 2 + Phase 4 (needs T003; can overlap US1 after T011)
- **US3**: Phase 5 only — **fully parallel** with US1/US2 once Phase 2 complete

---

## Implementation Strategy

1. Land **US3** early if desired — no dependency on scaffold (T020–T022 parallel).
2. **US1 + US2** share Phase 2; ship together as MVP.
3. Polish + golden regen last (T026 after all emitter changes).
