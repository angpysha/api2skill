# Tasks: Multi-Spec Skill Composition

**Input**: Design documents from `/specs/012-multi-spec-skill/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included — constitution **test-first** default (plan Constitution Check).

**Beads (`br`)**: Parent epic and phase/story children created with this file. See
[Beads mapping](#beads-mapping-br) below. Prefer claiming a `br` child for the current
phase, then work checklist items `Tnnn` inside it.

**Organization**: By user story after shared foundation. Parallelism: B → (C ∥ F) → (D ∥ E).

**Note**: Track A (creator self-update D11) is already implemented on branch — verify + close `.1`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: `[US1]`…`[US5]` for story phases only
- Include exact file paths in descriptions

---

## Beads mapping (`br`)

| Phase / story | `br` id | Title |
|---------------|---------|-------|
| Epic (parent) | `api2skill-012-multi-spec-ep-gsb` | 012: Multi-spec skill composition |
| Phase 1 Track A verify (D11) | `api2skill-012-multi-spec-ep-gsb.1` | 012-A: Verify creator self-update (shipped) |
| Phase 2 Foundation (Track B) | `api2skill-012-multi-spec-ep-gsb.2` | 012-B: Manifest v2 + SkillModelComposer |
| Phase 3 US1 Generate multi-spec (C) | `api2skill-012-multi-spec-ep-gsb.3` | 012-US1: generate --spec ×N |
| Phase 4 US2 Merge skills (D) | `api2skill-012-multi-spec-ep-gsb.4` | 012-US2: merge --into |
| Phase 5 US3 Add-spec (D) | `api2skill-012-multi-spec-ep-gsb.5` | 012-US3: add-spec |
| Phase 6 US4 Multi-part update (E) | `api2skill-012-multi-spec-ep-gsb.6` | 012-US4: update all parts / --spec-id |
| Phase 7 Emit + US5 creator polish (F) | `api2skill-012-multi-spec-ep-gsb.7` | 012-F/US5: SKILL.md API parts + creator contract |
| Phase 8 Polish | `api2skill-012-multi-spec-ep-gsb.8` | 012-PL: Goldens, quickstart, version bump |

**Deps**: `.3` and `.7` blocked on `.2`. `.4`, `.5`, `.6` blocked on `.3`. `.8` blocked on `.3`–`.7`.

Ready work: `br ready` → claim `.1` (verify/close) or `.2` (foundation) first.

**Parallel after `.2`**: `.3` ∥ `.7` (if SpecPartSummary shape locked in `.2`).  
**Parallel after `.3`**: `.4` ∥ `.5` ∥ `.6`.

---

## Phase 1: Track A — Creator self-update verify (D11) ✅

**Purpose**: Confirm shipped D11 work; close beads child when green.

**Beads**: `.1`

- [x] T001 `CreatorSkillVersion` + `.creator-version.json` write on `install-creator` — `src/Api2Skill/Cli/CreatorSkillVersion.cs`, `InstallCreatorCommand.cs`
- [x] T002 [P] `api2skill creator check-update` (exit 8 when outdated) — `src/Api2Skill/Cli/CreatorCommand.cs`
- [x] T003 [P] Creator template self-update + auth/base-url/secrets interview — `src/Api2Skill/templates/api2skill-creator/SKILL.md`
- [x] T004 [P] Unit tests — `tests/Api2Skill.Tests/Cli/CreatorSkillVersionTests.cs`, `InstallCreatorCommandTests.cs`
- [x] T005 Run quickstart §1 (`creator check-update`, `install-creator --force`); close `br` `.1` when verified

**Checkpoint**: D11 verified; agent never auto-updates without user ask

---

## Phase 2: Foundational — Manifest v2 + composer (Track B)

**Purpose**: Spec part model + composition collision rules — **blocks US1–US4 emit paths**

**⚠️ CRITICAL**: No multi-spec CLI story until this phase completes

**Beads**: `.2`

### Tests (write first — must FAIL then pass)

- [x] T006 [P] Failing tests for legacy `specSource` → `specSources[]` load + v2 serialize round-trip in `tests/Api2Skill.Tests/Output/CompositeManifestTests.cs`
- [x] T007 [P] Failing composer tests: base URL mismatch fail, opId collide fail/prefix, scheme rename, tag collision, **scriptKind mismatch fail** — `tests/Api2Skill.Tests/Model/SkillModelComposerTests.cs`
- [x] T008 [P] Add fixtures `tests/Api2Skill.Tests/fixtures/multi-spec/public.json`, `admin.json`, `billing.json` (same base URL; distinct ops/schemes)

### Implementation

- [x] T009 [P] Add `SpecPartManifest`, `SpecPartSummary`, `CompositionOptions` per `data-model.md` in `src/Api2Skill/Model/`
- [x] T010 Extend `SkillManifest` / `SkillManifestIo` for `specSources[]` + legacy load in `src/Api2Skill/Output/SkillManifest.cs`
- [x] T011 Extend `SkillModel` with `SpecParts`; optional `SourceSpecId` on `OperationModel` / `SecuritySchemeModel` in `src/Api2Skill/Model/SkillModel.cs`, `OperationModel.cs`, `SecuritySchemeModel.cs`
- [x] T012 Implement `SkillModelComposer.Compose` (D2–D4, schema file namespacing) in `src/Api2Skill/Model/SkillModelComposer.cs`
- [x] T013 Green T006–T007; document fail-fast + parallel acquire NFR in composer/service XML docs (research R13)

**Checkpoint**: Manifest v2 round-trip green; composer unit tests green

---

## Phase 3: User Story 1 — Generate one skill from multiple docs (Priority: P1) 🎯 MVP

**Goal**: `generate --spec ×N` → one skill, `specSources[]`, shared base URL policy

**Independent Test**: quickstart §2–§3

**Beads**: `.3` (depends on `.2`)

### Tests

- [x] T014 [P] CLI/unit tests for multi `--spec` + `--spec-id` / `--on-conflict` in `tests/Api2Skill.Tests/Cli/` (new or extend Generate tests)
- [x] T015 Integration: two fixtures → one skill; conflicting base URLs → exit 2, no output — `tests/Api2Skill.Tests/Integration/CompositeSkillIntegrationTests.cs`

### Implementation

- [x] T016 Extend `GenerateOptions` with `SpecParts[]`, `OnConflict` in `src/Api2Skill/Cli/GenerateOptions.cs` (or equivalent)
- [x] T017 Wire repeatable `--spec` / `--spec-id` / `--spec-label` / `--on-conflict` on `generate` in `src/Api2Skill/Cli/GenerateCommand.cs` (keep positional single-spec compat)
- [x] T018 Add `CompositeGenerateService` — parallel per-part acquire/parse/build (`Task.WhenAll`), then compose → emit; preserve `secrets.json`/`auth.json`/`.auth-cache.json` — `src/Api2Skill/Cli/CompositeGenerateService.cs`
- [x] T019 Manifest write always emits `specSources[]` from generate path in `GenerateCommand` / service
- [x] T020 Green T014–T015; quickstart §2–§3 pass

**Checkpoint**: US1 MVP — multi-spec generate works end-to-end

---

## Phase 4: User Story 2 — Merge previously generated skills (Priority: P1)

**Goal**: `merge --into` from ≥2 skill dirs; sources untouched; secrets first-wins + warning (D9)

**Independent Test**: quickstart §4

**Beads**: `.4` (depends on `.3`)

### Tests

- [ ] T021 [P] CLI tests: merge requires ≥2 skills; refuse existing target without `--force` — `tests/Api2Skill.Tests/Cli/MergeCommandTests.cs`
- [ ] T022 Integration: merge two skills; secrets key collision warning; sources unmodified — `CompositeSkillIntegrationTests.cs`

### Implementation

- [ ] T023 Implement `MergeCommand` + credential union helper (D9) in `src/Api2Skill/Cli/MergeCommand.cs` (+ SkillWriter/helper as needed)
- [ ] T024 Register `merge` on `RootCommand` in `src/Api2Skill/Program.cs`
- [ ] T025 Green T021–T022; steer errors away from using merge as add-spec (D8)

**Checkpoint**: US2 merge works; sources preserved

---

## Phase 5: User Story 3 — Add-spec to existing skill (Priority: P2)

**Goal**: `add-spec <skill> <source>` appends one part; preserves credentials (D8)

**Independent Test**: quickstart §5

**Beads**: `.5` (depends on `.3`; **parallel with `.4` / `.6`**)

### Tests

- [ ] T026 [P] CLI tests: add-spec duplicate `spec-id` fails; default id from filename — `tests/Api2Skill.Tests/Cli/AddSpecCommandTests.cs`
- [ ] T027 Integration: three-part skill; `auth.json` / `.auth-cache.json` preserved (assert both files unchanged when pre-seeded) — `CompositeSkillIntegrationTests.cs`

### Implementation

- [ ] T028 Implement `AddSpecCommand` using `CompositeGenerateService` in `src/Api2Skill/Cli/AddSpecCommand.cs`
- [ ] T029 Register `add-spec` in `src/Api2Skill/Program.cs`
- [ ] T030 Green T026–T027

**Checkpoint**: US3 add-spec works

---

## Phase 6: User Story 4 — Update refreshes all parts (Priority: P2)

**Goal**: `update` re-acquires all parts; positional + `--spec-id` when N>1 (D10)

**Independent Test**: quickstart §6–§7

**Beads**: `.6` (depends on `.3`; **parallel with `.4` / `.5`**)

### Tests

- [ ] T031 [P] UpdateCommand tests: multi-part without `--spec-id` + positional → usage error; with `--spec-id` replaces one part — `tests/Api2Skill.Tests/Cli/UpdateCommandTests.cs`
- [ ] T032 Integration: legacy singular `specSource` still updates (SC-005); multi-part refresh all — `CompositeSkillIntegrationTests.cs`

### Implementation

- [ ] T033 Extend `UpdateCommand` for multi-part acquire + `--spec-id` (D10) in `src/Api2Skill/Cli/UpdateCommand.cs`
- [ ] T034 Green T031–T032; quickstart §6–§7

**Checkpoint**: US4 multi-part update + legacy OK

---

## Phase 7: Emit + User Story 5 — Creator contract polish (Priority: P2)

**Goal**: SKILL.md API parts / tag prefix (FR-010); creator template covers multi-spec + auth + secrets + self-update (US5)

**Independent Test**: quickstart §9; greps in template

**Beads**: `.7` (depends on `.2`; **parallel with `.3`** once SpecParts shape stable)

### Tests

- [x] T035 [P] Assert composite SKILL.md contains `## API parts` and colliding tags use `[specId]`; **single-part skill hides API parts section** — emit/integration test
- [x] T036 [P] Assert creator template contains `creator check-update`, base URL, secrets.example, merge/add-spec — extend `InstallCreatorCommandTests` / content asserts

### Implementation

- [x] T037 [P] `SkillMdWriter` API parts section + tag collision prefix + auth provenance — `src/Api2Skill/Emit/SkillMdWriter.cs`
- [x] T038 Schema collision filenames `{specId}.{schemaName}.json` in schema writer path (as applicable)
- [ ] T039 Align `templates/api2skill-creator/SKILL.md` with full Creator skill contract (spec §); refresh `wiki/Install-Creator.md` / Generate/Update wiki one-liners
- [x] T040 Green T035–T036 (T038 covered by composer namespace → SchemaWriter; T039 wiki polish remains)

**Checkpoint**: US5 creator + FR-010 emit complete

---

## Phase 8: Polish & cross-cutting

**Beads**: `.8` (depends on `.3`–`.7`)

- [ ] T041 [P] Update `contracts` consumers: README / `wiki/` for `generate --spec`, `merge`, `add-spec`, multi-part `update`
- [ ] T042 Run full quickstart.md scenarios; fix gaps
- [ ] T043 Bump `<Version>` in `src/Api2Skill/Api2Skill.csproj` if not already bumped for this PR (behavior change)
- [ ] T044 Mark feature ready for human review; close eligible `br` children; leave epic for PR merge

---

## Dependencies & execution order

### Story completion order

```text
.1 Track A verify (can close early)
.2 Foundation B ──► .3 US1 (C) ──► .4 US2 ∥ .5 US3 ∥ .6 US4
                 └──► .7 F/US5 (∥ .3)
.8 Polish (after .3–.7)
```

### Parallel opportunities

- After `.2`: `.3` ∥ `.7`
- After `.3`: `.4` ∥ `.5` ∥ `.6`
- Within phases: tasks marked **[P]** with disjoint files

### Runtime NFR (in CompositeGenerateService)

- Parallel per-part acquire/parse/build; serial compose/emit; fail-fast; ordered warnings (R13)

---

## Implementation strategy

1. Close/verify Track A (`.1`).
2. Finish foundation B (`.2`) — MVP gate for multi-spec.
3. Ship US1 generate (`.3`) as first user-facing value.
4. Parallelize merge / add-spec / update (`.4`–`.6`) and emit/creator (`.7`) as capacity allows.
5. Polish + version + human review (`.8`).

**MVP**: Phase 2 + Phase 3 (US1) — one skill from N swagger docs.
