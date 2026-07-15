# Tasks: Full request/response schema in reference docs

**Input**: Design documents from `/specs/011-reference-full-schema/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included — constitution **test-first** default (plan Constitution Check).

**Beads (`br`)**: Parent epic and phase/story children created with this file. See
[Beads mapping](#beads-mapping-br) below. Prefer claiming a `br` child for the current
phase, then work checklist items `Tnnn` inside it.

**Organization**: By user story (US1, US2, US5, US3, US4 — matching spec.md numbering) after shared foundation.

**Note**: Branch already has a draft (ParameterModel/SchemaDetail/ReferenceWriter/petstore fixture). Tasks below are the **canonical remaining/alignment work** — implement or reconcile draft to locked grill + contracts; do not leave gaps (raw schemas, SchemaName links, truncation notes, indented JSON, oneOf variants).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: `[US1]`…`[US5]` for story phases only
- Include exact file paths in descriptions

---

## Beads mapping (`br`)

| Phase / story | `br` id | Title |
|---------------|---------|-------|
| Epic (parent) | `api2skill-011-ref-schema-ep7` | 011: Full request/response schema in reference docs |
| Phase 1–2 Setup + Foundation | `api2skill-011-ref-schema-ep7.1` | 011-P12: Model foundation (T001–T008) |
| Phase 3 US1 Request inputs | `api2skill-011-ref-schema-ep7.2` | 011-US1: Request inputs in reference MD (T009–T014) |
| Phase 4 US2 Responses | `api2skill-011-ref-schema-ep7.3` | 011-US2: Response shapes in reference MD (T015–T018) |
| Phase 5 US5 Raw schemas | `api2skill-011-ref-schema-ep7.4` | 011-US5: Raw schemas on disk + MD links (T019–T024) |
| Phase 6 US3 Progressive disclosure | `api2skill-011-ref-schema-ep7.5` | 011-US3: Progressive disclosure SKILL.md (T025–T027) |
| Phase 7 US4 Regenerate | `api2skill-011-ref-schema-ep7.6` | 011-US4: Regenerate/update refreshes schemas (T028–T030) |
| Phase 8 Polish | `api2skill-011-ref-schema-ep7.7` | 011-PL: Goldens, quickstart, version (T031–T036) |

Ready work: `br ready` → claim `.1` (foundation) first.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Align fixtures so generate exercises `$ref` components, nested fields, and query enums (contracts + quickstart).

**Beads**: `api2skill-011-ref-schema-ep7.1` (shared with Phase 2)

- [x] T001 Ensure `tests/Api2Skill.Tests/fixtures/petstore.json` and `petstore.yaml` include named `components.schemas` (`Pet`, `PetInput`, nested `Category`/`Tag`), `findPetsByStatus` query enum, and `$ref` body/responses per plan/quickstart
- [x] T002 [P] Add failing skeleton asserts in `tests/Api2Skill.Tests/Model/SchemaDetailMappingTests.cs` for `SchemaName`, `Truncated`, and `ComponentSchemas` presence (will stay red until Phase 2–5)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Intermediate model + builder collection of raw schemas and detail fields — **blocks all stories**

**⚠️ CRITICAL**: No user story emit work until this phase completes

**Beads**: `api2skill-011-ref-schema-ep7.1`

### Tests (write first — must FAIL then pass)

- [x] T003 [P] Extend mapping tests in `tests/Api2Skill.Tests/Model/SchemaDetailMappingTests.cs` for enum/query example, nested dotted properties, `SchemaName` on `$ref` body/response, depth-4 `Truncated` flag
- [x] T004 [P] Add builder test that reachable `components.schemas` appear as `SkillModel.ComponentSchemas` with non-empty `RawJson` containing `"properties"` / `$ref` in `tests/Api2Skill.Tests/Model/SchemaDetailMappingTests.cs` (or new `ComponentSchemaMappingTests.cs`)

### Implementation

- [x] T005 [P] Add `ComponentSchemaModel` + extend `SchemaDetailModel` / `SchemaPropertyModel` / new `SchemaVariantModel` in `src/Api2Skill/Model/SchemaDetailModel.cs` (and companion file if needed) per `data-model.md`
- [x] T006 [P] Extend `ParameterModel`, `RequestBodyModel`, `ResponseModel` with format/enum/example/`Schema`/`SchemaName` fields in `src/Api2Skill/Model/ParameterModel.cs`, `RequestBodyModel.cs`, `ResponseModel.cs`
- [x] T007 Extend `SkillModel` with `IReadOnlyList<ComponentSchemaModel> ComponentSchemas` in `src/Api2Skill/Model/SkillModel.cs` (update all `new SkillModel(...)` call sites / tests)
- [x] T008 In `src/Api2Skill/Model/SkillModelBuilder.cs`: map param detail; `DescribeSchema` depth 4 + `Truncated`; `allOf` merge + `oneOf`/`anyOf` `Variants`; set `SchemaName` from `$ref`; collect reachable named schemas; serialize each via OpenApi JSON writer into `RawJson` (keep `#/components/schemas/…` refs) per `research.md`

**Checkpoint**: Mapping tests green for model fields; `ComponentSchemas` populated for petstore fixture

---

## Phase 3: User Story 1 — Agent reads reference to call an endpoint (Priority: P1) 🎯 MVP

**Goal**: Path/query/header params + JSON request body fully documented in `reference/<tag>.md` (pasteable example + property table)

**Independent Test**: quickstart Scenario A (param enum + request body table + ```json fence); no need for schema files yet

**Beads**: `api2skill-011-ref-schema-ep7.2`

### Tests

- [x] T009 [P] [US1] Assert generated `reference/pet.md` contains param enum columns, `example:` for query default, request-body property table, and indented JSON example in `tests/Api2Skill.Tests/Emit/CsEmitterGoldenTests.cs` (or dedicated `ReferenceWriterTests.cs`)

### Implementation

- [x] T010 [US1] Implement/align parameter table (`name|in|required|type|enum|description`) + param examples + object/array param subsections in `src/Api2Skill/Emit/ReferenceWriter.cs` per `contracts/skill-reference.md`
- [x] T011 [US1] Emit JSON request-body section with Content-Type, Shape, property table, indented pasteable Example fence in `src/Api2Skill/Emit/ReferenceWriter.cs` (Content-Type driven; non-JSON note without fake JSON per FR-011)
- [x] T012 [US1] Pretty-print `ExampleJson` (indent 2) when writing fences in `src/Api2Skill/Emit/ReferenceWriter.cs` or when building examples in `SkillModelBuilder.cs`
- [x] T013 [US1] Emit `**Variants**` list for `oneOf`/`anyOf` on request bodies in `src/Api2Skill/Emit/ReferenceWriter.cs` per grill Q7 / contract
- [x] T014 [US1] Emit depth-4 truncation note when `Schema.Truncated` (link text may be placeholder until US5 wires schema href) in `src/Api2Skill/Emit/ReferenceWriter.cs`

**Checkpoint**: Generating petstore yields complete **input** docs in `reference/pet.md` without consulting OpenAPI

---

## Phase 4: User Story 2 — Agent understands response shapes (Priority: P1)

**Goal**: Per-status response sections with property tables + pasteable JSON for JSON media

**Independent Test**: `### \`200\`` sections with Content-Type, property table, Example fence; empty body line for 404

**Beads**: `api2skill-011-ref-schema-ep7.3`

### Tests

- [x] T015 [P] [US2] Assert response sections in generated reference include property tables + Example for 200 and “none documented” for empty responses in `tests/Api2Skill.Tests/Emit/CsEmitterGoldenTests.cs` / `ReferenceWriterTests.cs`

### Implementation

- [x] T016 [US2] Align response writing in `src/Api2Skill/Emit/ReferenceWriter.cs` to contract (`### \`code\``, Content-Type, Shape, table, Example)
- [x] T017 [US2] Apply same Variants / Truncated / non-JSON rules to responses in `src/Api2Skill/Emit/ReferenceWriter.cs`
- [x] T018 [US2] Ensure array responses show nested `items.*` paths within depth 4 in `SkillModelBuilder.DescribeSchema` + golden coverage

**Checkpoint**: Response models fully readable from `reference/<tag>.md` alone

---

## Phase 5: User Story 5 — Reusable raw component schemas in the skill (Priority: P1)

**Goal**: Write `reference/schemas/<Name>.json` (raw OpenAPI schema objects) and link them from tag MD

**Independent Test**: Files exist for `Pet` / `PetInput`; MD shows `Schema: [`Pet`](schemas/Pet.json)`; RawJson matches source shape

**Beads**: `api2skill-011-ref-schema-ep7.4`

### Tests

- [x] T019 [P] [US5] Unit/integration test that generate writes `reference/schemas/Pet.json` and `PetInput.json` with `"properties"` and source `$ref` strings in `tests/Api2Skill.Tests/Emit/SchemaWriterTests.cs`
- [x] T020 [P] [US5] Assert tag MD contains relative `schemas/Pet.json` / `schemas/PetInput.json` links in `tests/Api2Skill.Tests/Emit/CsEmitterGoldenTests.cs`

### Implementation

- [x] T021 [US5] Create `src/Api2Skill/Emit/SchemaWriter.cs` writing sorted `reference/schemas/<Name>.json` from `SkillModel.ComponentSchemas` per `contracts/schemas-layout.md`
- [x] T022 [US5] Call `SchemaWriter.Write` from `src/Api2Skill/Output/SkillWriter.cs` after `ReferenceWriter.Write`
- [x] T023 [US5] Emit `Schema: [`Name`](schemas/Name.json)` lines for body/response/truncation notes in `src/Api2Skill/Emit/ReferenceWriter.cs`
- [x] T024 [US5] Confirm filtered operations only emit reachable schemas (unit test with `--include`/`BuildOptions` include filter) in `tests/Api2Skill.Tests/Model/` or Emit tests

**Checkpoint**: Skill contains raw schemas; MD links work; unused components omitted

---

## Phase 6: User Story 3 — Progressive disclosure preserved (Priority: P1)

**Goal**: `SKILL.md` stays index-only; detail only in `reference/`

**Independent Test**: Parameter descriptions / property tables absent from `SKILL.md`, present in `reference/pet.md`

**Beads**: `api2skill-011-ref-schema-ep7.5`

### Tests

- [x] T025 [P] [US3] Strengthen progressive-disclosure asserts in `tests/Api2Skill.Tests/Emit/CsEmitterGoldenTests.cs` (`GeneratedDispatcher_CompactSkillMdStaysIndexOnly_…`) — no property/enum tables in SKILL.md; schemas/ not linked from SKILL.md body

### Implementation

- [x] T026 [US3] Verify `src/Api2Skill/Emit/SkillMdWriter.cs` does not emit schema tables (no code change expected; fix if regression)
- [x] T027 [US3] Optionally add one-line pointer in SKILL.md overview that schema models live under `reference/schemas/` (only if it stays one short sentence — skip if it bloats; prefer wiki in polish)

**Checkpoint**: Constitution V still holds for petstore golden

---

## Phase 7: User Story 4 — Regenerate/update keeps reference accurate (Priority: P2)

**Goal**: `--force` / `update` refresh reference + schemas; preserve `examples/`

**Independent Test**: quickstart Scenario C; ForcePreservesExamples still green

**Beads**: `api2skill-011-ref-schema-ep7.6`

### Tests

- [x] T028 [P] [US4] Test that `--force` regenerate updates `reference/schemas/*.json` content when fixture changes in `tests/Api2Skill.Tests/Output/` or Integration tests
- [x] T029 [US4] Confirm `tests/Api2Skill.Tests/Output/ForcePreservesExamplesTests.cs` still passes with schema files present (examples preserved; schemas rewritten)

### Implementation

- [x] T030 [US4] Ensure staging `SkillWriter` always rewrites `reference/` + `schemas/` (no preserve of schemas — unlike `examples/`) in `src/Api2Skill/Output/SkillWriter.cs`; fix if any keep-old-schema path exists

**Checkpoint**: Update/regenerate refreshes schemas and MD; examples intact

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Goldens, docs, version, quickstart validation

**Beads**: `api2skill-011-ref-schema-ep7.7`

- [x] T031 Regenerate and commit approved trees under `tests/Api2Skill.Tests/fixtures/__approved__/petstore-cs/`, `petstore-csx/`, `petstore-fsx/` including `reference/schemas/*.json` and updated `reference/*.md` / `SKILL.md` / scripts if needed
- [x] T032 [P] Fix any fixture-count assumptions (`4 operation(s)` → `5`, filter tests) in `tests/Api2Skill.Tests/` so full suite green (exclude known OAuth listener flakes only if pre-existing)
- [x] T033 [P] Update wiki/README reference to mention `reference/schemas/` in `wiki/Getting-Started.md` or `docs/usage.md` (short bullet)
- [x] T034 Run `specs/011-reference-full-schema/quickstart.md` Scenario A manually or script; fix gaps
- [x] T035 Confirm package `<Version>` in `src/Api2Skill/Api2Skill.csproj` bumped for user-facing change (0.6.1+ per version-bump rule)
- [x] T036 Mark feature ready for human review; close `br` children when done; leave epic for PR merge

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Immediate
- **Foundational (Phase 2)**: After Setup — **BLOCKS** all stories
- **US1 / US2 / US5 (P1)**: After Foundation — can proceed in parallel once T008 done (US5 needs ComponentSchemas; US1/US2 need SchemaDetail)
- **US3**: After US1 + US2 (validates progressive disclosure on final MD shape)
- **US4**: After US5 (schema files exist to refresh)
- **Polish**: After US1–US5 + US3 + US4

### User Story Dependencies

| Story | Depends on | Independently testable deliverable |
|-------|------------|--------------------------------------|
| US1 | Foundation | Input docs in tag MD |
| US2 | Foundation | Response docs in tag MD |
| US5 | Foundation | `reference/schemas/*.json` + links |
| US3 | US1 + US2 | SKILL.md stays compact |
| US4 | US5 | Regenerate refreshes schemas |

### Parallel Opportunities

```bash
# After T008:
# Dev A — US1 (ReferenceWriter request/params)
# Dev B — US2 (ReferenceWriter responses)
# Dev C — US5 (SchemaWriter + links)

# Parallel tests within stories:
T009 || T015 || T019 || T020
```

---

## Parallel Example: User Story 1

```bash
Task: "T009 [US1] Assert generated reference/pet.md contains param enums + request body"
# then sequential impl T010 → T014 on ReferenceWriter.cs
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1–2 foundation
2. Phase 3 US1 → validate petstore request/params in MD
3. **STOP** for demo if needed

### Incremental Delivery

1. US1 inputs → US2 responses → US5 raw schemas → US3 disclosure check → US4 update → Polish/goldens

### Suggested MVP scope

**US1 only** (full call inputs in reference MD). Ship value increases sharply once US5 (raw schemas) lands with US2.

---

## Notes

- Draft code on the branch may already satisfy parts of T005–T014 — treat each task as **verify + fill gaps**, not blind rewrite
- Do not preserve `reference/schemas/` across `--force` (always regenerate)
- Keep Microsoft.OpenApi usage in builder for RawJson; SchemaWriter writes strings only (Constitution III)
- After tasks: prefer `/speckit.analyze` before `/speckit.implement`
