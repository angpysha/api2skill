# Implementation Plan: Full request/response schema in reference docs

**Branch**: `011-reference-full-schema` | **Date**: 2026-07-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-reference-full-schema/spec.md`

## Summary

Enrich generated `reference/<tag>.md` so agents can call APIs without the original OpenAPI file: full path/query/header parameters (type, format, enum, example), Content-Type–driven **pasteable** request/response JSON documents, property tables (nested up to depth 4), and **raw** component schemas persisted at `reference/schemas/<Name>.json` with links from tag MD. Align existing draft emitter/model changes to locked grill decisions; keep `SKILL.md` index-only (Constitution V).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`).

**Primary Dependencies**: Existing `Microsoft.OpenApi` 3.8 (schema serialize), `System.Text.Json` — no new packages.

**Storage**: Generated skill filesystem — `reference/<tag>.md`, `reference/schemas/*.json`.

**Testing**: xUnit — mapping tests (params/enums/nested/`SchemaName`), schema-file golden/content tests, reference MD golden updates (petstore-cs/csx/fsx), progressive-disclosure assertion on `SKILL.md`, regenerate preservation of `examples/` + schema refresh.

**Target Platform**: Cross-platform .NET tool + generated skills.

**Project Type**: CLI (`src/Api2Skill`) — model builder + emit writers.

**Performance Goals**: Negligible vs parse cost; schema walk once per generate; depth-capped MD expansion.

**Constraints**: Constitution I–V; secrets never in examples/schemas; deterministic emit (NFR-4); Dedupe Ticket before new types; version bump on ship.

**Scale/Scope**: Extend `SkillModelBuilder`, `ReferenceWriter`, new `SchemaWriter` (or equivalent), model records; fixture + goldens; wiki one-liner if docs mention reference layout.

## Constitution Check

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Docs/schema files only; dispatchers unchanged | ✅ |
| II. .NET-native BCL in emitters | No new deps in emitted scripts | ✅ |
| III. Pluggable emitters | Schema/reference writers shared; emitters untouched | ✅ |
| IV. Secrets never committed | Placeholders only in examples; raw schemas are contract not credentials | ✅ |
| V. Progressive disclosure | Detail in `reference/` + `reference/schemas/`; SKILL.md stays index | ✅ |
| Test-first | Mapping + golden tests before/with writer changes in tasks | ✅ |

**Result: PASS.**

### Post–Phase 1

Contracts extend skill output layout only; no auth/secret surface change. **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/011-reference-full-schema/
├── spec.md
├── plan.md              # this file
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── skill-reference.md
│   └── schemas-layout.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks later
```

### Source Code

```text
src/Api2Skill/
├── Model/
│   ├── SkillModel.cs              # + ComponentSchemas collection
│   ├── ParameterModel.cs          # format/enum/example/Schema (draft exists)
│   ├── SchemaDetailModel.cs       # + SchemaName?, EnumValues, truncation flag
│   ├── RequestBodyModel.cs        # + SchemaName?
│   ├── ResponseModel.cs           # + SchemaName?
│   └── SkillModelBuilder.cs       # collect refs, DescribeSchema depth 4, allOf merge
├── Emit/
│   ├── ReferenceWriter.cs         # Schema links, variants, truncation note, pasteable JSON
│   └── SchemaWriter.cs            # NEW — write reference/schemas/<Name>.json
└── Output/SkillWriter.cs          # call SchemaWriter after ReferenceWriter

tests/Api2Skill.Tests/
├── Model/SchemaDetailMappingTests.cs
├── Emit/…GoldenTests.cs           # approved trees include schemas/
└── fixtures/petstore.{json,yaml} + __approved__/petstore-{cs,csx,fsx}/
```

**Structure Decision**: **Extend** existing model + `ReferenceWriter`; **new** `SchemaWriter` (dedupe: no existing schema-file emitter). Keep Microsoft.OpenApi confined to `Parsing` + `SkillModelBuilder` (+ SchemaWriter only receives pre-serialized JSON strings from the model to preserve Constitution III — prefer raw JSON on `ComponentSchemaModel` built in the builder).

## Complexity Tracking

| Tension | Why needed | Rejected simpler |
|---------|------------|------------------|
| Persist raw schemas *and* expand in MD | Grill: agents need pasteable short view + full raw later | MD-only (loses full/raw) or schemas-only (weak call UX) |
| Depth 4 MD vs full raw files | Token budget vs completeness | Unlimited MD expansion on large graphs |
| Keep `#/components/schemas/` refs in files | Raw fidelity | Relative rewrite (extra complexity, not required for v1) |

## Phase 0 / 1 outputs

| Artifact | Path |
|----------|------|
| Research | [research.md](./research.md) |
| Data model | [data-model.md](./data-model.md) |
| Contracts | [contracts/](./contracts/) |
| Quickstart | [quickstart.md](./quickstart.md) |

**Agent context script**: not present under `.specify/scripts` — skipped.

## Implementation notes (for `/speckit-tasks`)

1. **Model**: Add `ComponentSchemaModel(Name, RawJson)`; `SkillModel.ComponentSchemas`; optional `SchemaName` on body/response/detail; collect reachable named schemas during map (including nested `$ref`).
2. **Builder**: Serialize raw schema via OpenApi JSON writer into `RawJson`; MD path continues `DescribeSchema` @ depth 4 with truncation marker; improve `allOf` merge; surface `oneOf`/`anyOf` variants on `SchemaDetailModel`.
3. **SchemaWriter**: Write `reference/schemas/<Name>.json` for each component (sorted by name for determinism); overwrite on generate/update.
4. **ReferenceWriter**: Parameter table with enum/format; `Schema: [Name](schemas/Name.json)`; pasteable indented JSON for JSON media; truncation note; non-JSON note; Variants section for oneOf/anyOf.
5. **Goldens / fixtures**: Ensure petstore uses `$ref` components; approve `reference/schemas/*.json` + updated `pet.md`.
6. **Version**: Confirm `0.6.1` (or next patch) at PR time.

## Next command

`/speckit-tasks` → `/speckit-analyze` → `/speckit-implement` (converge draft code to this plan).
