# Tasks: Chat-authored endpoint examples

**Input**: Design documents from `/specs/010-chat-endpoint-examples/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included — constitution **test-first** default (plan Constitution Check).

**Beads (`br`)**: Parent epic and phase/story children created with this file. See
[Beads mapping](#beads-mapping-br) below. Prefer claiming a `br` child for the current
phase, then work checklist items `Tnnn` inside it.

**Organization**: By user story (US1–US5) after shared foundation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: `[US1]`…`[US5]` for story phases only
- Include exact file paths in descriptions

---

## Beads mapping (`br`)

| Phase / story | `br` id | Title |
|---------------|---------|-------|
| Epic (parent) | `api2skill-010-chat-examples-89e` | 010: Chat-authored endpoint examples |
| Phase 1–2 Setup + Foundation | `api2skill-010-chat-examples-89e.1` | 010-P12: Examples foundation (T001–T010) |
| Phase 3 US1 Add + CLI | `api2skill-010-chat-examples-89e.2` | 010-US1: example add + link (T011–T015) |
| Phase 4 US2 Prefer guidance | `api2skill-010-chat-examples-89e.3` | 010-US2: SKILL prefer-examples (T016–T018) |
| Phase 5 US3 Preserve | `api2skill-010-chat-examples-89e.4` | 010-US3: Preserve examples/ on force (T019–T023) |
| Phase 6 US4 Failure protocol | `api2skill-010-chat-examples-89e.5` | 010-US4: Fail→ask→propose→approve docs (T024–T027) |
| Phase 7 US5 List/remove/sync | `api2skill-010-chat-examples-89e.6` | 010-US5: list/remove/sync CLI (T028–T032) |
| Phase 8 Polish | `api2skill-010-chat-examples-89e.7` | 010-PL: Wiki, goldens, quickstart, version (T033–T038) |

Ready work: `br ready` → claim `.1` (foundation) first. Story children blocked on `.1`; polish blocked on US1–US5.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold `Examples/` helpers and CLI stub per plan.md

**Beads**: `api2skill-010-chat-examples-89e.1` (shared with Phase 2)

- [ ] T001 Create `src/Api2Skill/Examples/` directory with stubs: `ExamplePaths.cs`, `ExampleStore.cs`, `ExampleReferenceLinker.cs`
- [ ] T002 [P] Create stub `src/Api2Skill/Cli/ExampleCommand.cs` (`example` parent + add|list|remove|sync subcommands, exit 2 on missing `--skill`)
- [ ] T003 [P] Register `ExampleCommand.Create()` on root in `src/Api2Skill/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Path/slug validation, discovery store, link rewriter — **blocks all stories**

**⚠️ CRITICAL**: No user story work until this phase completes

**Beads**: `api2skill-010-chat-examples-89e.1`

### Tests (write first — must FAIL then pass)

- [ ] T004 [P] Unit tests for path/slug validation in `tests/Api2Skill.Tests/Examples/ExamplePathsTests.cs`
- [ ] T005 [P] Unit tests for discovery + orphans in `tests/Api2Skill.Tests/Examples/ExampleStoreTests.cs`
- [ ] T006 [P] Unit tests for Authored examples markdown table in `tests/Api2Skill.Tests/Examples/ExampleReferenceLinkerTests.cs`

### Implementation

- [ ] T007 Implement `ExamplePaths` (safe segments, default name `default`, request/response paths) in `src/Api2Skill/Examples/ExamplePaths.cs` per `data-model.md` + `contracts/examples-layout.md`
- [ ] T008 Implement `ExampleStore` (scan, add files, remove dir, list) in `src/Api2Skill/Examples/ExampleStore.cs`
- [ ] T009 Implement `ExampleReferenceLinker` (emit/replace **Authored examples** section; relative `../examples/...` links) in `src/Api2Skill/Examples/ExampleReferenceLinker.cs` per `data-model.md` LinkBlock
- [ ] T010 Wire linker call from `src/Api2Skill/Emit/ReferenceWriter.cs` after each operation section (when examples present under skill dir — accept optional discovery result / skill root path)

**Checkpoint**: Path/store/linker unit tests green; `example --help` works

---

## Phase 3: User Story 1 — Add examples via CLI (Priority: P1) 🎯 MVP

**Goal**: `example add` writes files and patches tag MD links

**Independent Test**: quickstart Scenario A

**Beads**: `api2skill-010-chat-examples-89e.2`

### Tests

- [ ] T011 [P] [US1] CLI/integration tests for `example add` in `tests/Api2Skill.Tests/Cli/ExampleCommandTests.cs` (happy, second name, unknown op → exit 2, exists without `--force` → exit 2)

### Implementation

- [ ] T012 [US1] Resolve operationId → tag file via skill model or reference headings in `ExampleStore` / CLI helper
- [ ] T013 [US1] Implement `example add` (--skill, --op, --name, --request/--response, --force) in `src/Api2Skill/Cli/ExampleCommand.cs` per `contracts/cli-example.md`
- [ ] T014 [US1] After write, run linker sync for affected tag(s)
- [ ] T015 [US1] Exit codes: 0 success, 2 validation, 4 missing skill path

**Checkpoint**: Scenario A automated test green

---

## Phase 4: User Story 2 — Prefer authored examples (Priority: P1)

**Goal**: SKILL.md instructs agents to prefer examples over inventing JSON

**Independent Test**: quickstart Scenario B

**Beads**: `api2skill-010-chat-examples-89e.3`

### Tests

- [ ] T016 [P] [US2] Golden/string assert SKILL.md contains prefer-examples guidance in `tests/Api2Skill.Tests/Emit/SkillMdExamplesGuidanceTests.cs`

### Implementation

- [ ] T017 [US2] Emit Examples section from `contracts/skill-guidance.md` (prefer path) in `src/Api2Skill/Emit/SkillMdWriter.cs`
- [ ] T018 [US2] Brief caption under Authored examples table in reference MD (via linker)

**Checkpoint**: Scenario B greps pass on generated petstore skill

---

## Phase 5: User Story 3 — Preserve across generate/update (Priority: P1)

**Goal**: `--force` / update keeps `examples/` and re-links

**Independent Test**: quickstart Scenario C

**Beads**: `api2skill-010-chat-examples-89e.4`

### Tests

- [ ] T019 [P] [US3] Integration test preserve `examples/` on `--force` in `tests/Api2Skill.Tests/Output/ForcePreservesExamplesTests.cs`
- [ ] T020 [P] [US3] Extend or add update-path preservation assert in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs` (or dedicated test)

### Implementation

- [ ] T021 [US3] Preserve entire `examples/` tree into staging in `src/Api2Skill/Output/SkillWriter.cs` (like auth.json lifecycle; recursive copy)
- [ ] T022 [US3] After ReferenceWriter, re-link from preserved examples (linker scan) before finalize
- [ ] T023 [US3] Warn orphans on stderr during generate/sync (v1: keep files)

**Checkpoint**: Scenario C green

---

## Phase 6: User Story 4 — Failure protocol docs (Priority: P1)

**Goal**: SKILL.md documents fail → ask → propose → await approval; no auto-apply tooling

**Independent Test**: quickstart Scenario E

**Beads**: `api2skill-010-chat-examples-89e.5`

### Tests

- [ ] T024 [P] [US4] Assert failure-protocol phrases in `tests/Api2Skill.Tests/Emit/SkillMdExamplesGuidanceTests.cs`

### Implementation

- [ ] T025 [US4] Append Failure protocol section from `contracts/skill-guidance.md` in `SkillMdWriter.cs`
- [ ] T026 [US4] Append chat authorship steps (create files + `example sync`) in `SkillMdWriter.cs`
- [ ] T027 [US4] Confirm CLI has no path that rewrites examples from HTTP failures (docs-only; comment or test negative)

**Checkpoint**: Scenario E green

---

## Phase 7: User Story 5 — List / remove / sync (Priority: P2)

**Goal**: list, remove, sync CLI + chat instructions

**Independent Test**: quickstart Scenario D

**Beads**: `api2skill-010-chat-examples-89e.6`

### Tests

- [ ] T028 [P] [US5] Tests for list/remove/sync in `tests/Api2Skill.Tests/Cli/ExampleCommandTests.cs`

### Implementation

- [ ] T029 [US5] Implement `example list` table output
- [ ] T030 [US5] Implement `example remove` (delete name dir + re-link)
- [ ] T031 [US5] Implement `example sync` (all reference/*.md + orphan warnings)
- [ ] T032 [US5] Ensure SkillMd chat authorship mentions `example sync`

**Checkpoint**: Scenario D green

---

## Phase 8: Polish & cross-cutting

**Purpose**: Docs, goldens, version coordination with 009

**Beads**: `api2skill-010-chat-examples-89e.7`

- [ ] T033 [P] Update / add `wiki/` Examples page (or Authentication sibling section) pointing at layout + CLI
- [ ] T034 [P] Refresh goldens impacted by SkillMd/Reference deltas (`tests/Api2Skill.Tests/Emit/` fixtures)
- [ ] T035 Run quickstart Scenarios A–E (manual or scripted under tests)
- [ ] T036 Version: keep **0.6.0** if 009 unreleased on same line; else bump **0.6.1** — document choice in handoff (`src/Api2Skill/Api2Skill.csproj`)
- [ ] T037 `dotnet format` + `dotnet build` Release + unit-test skill green
- [ ] T038 Update `specs/010-chat-endpoint-examples/spec.md` status to Implemented (when MVP done)

---

## Dependencies & execution order

```text
Phase 1–2 foundation
    → US1 (add) 🎯 MVP
    → US2 prefer + US4 failure protocol (can parallel after SkillMd section work)
    → US3 preserve (needs linker + SkillWriter; can start after T010)
    → US5 list/remove/sync (needs store + linker)
    → Polish
```

**Parallel opportunities**: T001–T003 setup; T004–T006 tests; US2∥US4 SkillMd; US5 after US1 store; goldens late.

**MVP**: Phase 1–2 + US1 + US3 + SkillMd prefer/failure (US2+US4) — enough for SC-001–SC-003.

---

## Implementation strategy

1. Foundation paths/store/linker
2. `example add` + ReferenceWriter hook
3. SkillWriter preserve `examples/`
4. SkillMd guidance (prefer + failure)
5. list/remove/sync
6. Polish/goldens/version
