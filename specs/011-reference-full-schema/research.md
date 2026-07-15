# Research: Full request/response schema in reference docs

**Feature**: `011-reference-full-schema`  
**Date**: 2026-07-15  
**Discovery**: **DEGRADED** — no codebase-search MCP / graphify; Grep/Read used. Existing draft on branch already maps enums/nested properties into `SchemaDetailModel` and richer `ReferenceWriter` output; plan must **align** that draft to locked grill decisions (raw schema files, MD depth 4, Content-Type-driven pasteable bodies).

## Decisions

### Decision: Raw schema serialization

**Choice**: Persist each referenced `components.schemas.<Name>` object by serializing the Microsoft.OpenApi `IOpenApiSchema` instance with `SerializeAsJsonAsync` / `OpenApiJsonWriter` at the document’s OpenAPI version. Output path: `reference/schemas/<Name>.json` (pretty-printed, deterministic key order from the writer).

**Rationale**: Meets grill Q5/Q8 (“raw” OpenAPI schema, not a flattened rewrite). No new NuGet packages. Writer already on the dependency graph.

**Alternatives considered**:
- Hand-built `JsonObject` from `SchemaDetailModel` — rejected (loses `allOf`/`$ref`/constraints).
- Copy a slice of the original YAML/JSON file via string surgery — rejected (fragile; broken on multiline YAML).
- Emit YAML schema files — rejected (Q5 = JSON).

### Decision: `$ref` inside raw schema files

**Choice**: **Keep** OpenAPI-form references (`#/components/schemas/Other`) exactly as in the source component. Do **not** rewrite to `./Other.json` in v1.

**Rationale**: “Raw” means source shape. Sibling files are still discoverable by name; MD already links `[Other](schemas/Other.json)`. Rewriting refs is a later enhancement if local JSON Schema tooling needs relative URIs.

**Alternatives considered**: Relative `./Other.json` rewrite (deferred); fully resolve/inline all refs into each file (bloats disks, loses composition identity).

### Decision: Which schemas are emitted

**Choice**: Emit a file for every **named** component schema reachable from any **emitted** operation (after include/exclude filters): request body, response content, parameter schemas, and nested `$ref` / `allOf` / `oneOf` / `anyOf` / `items` / `properties` edges. Skip unused components in a large registry.

**Rationale**: Progressive disclosure + filter semantics (Constitution V / FR-004b). Anonymous inline schemas stay only in MD (no file).

### Decision: Capturing schema names in the intermediate model

**Choice**: Extend `RequestBodyModel` / `ResponseModel` (and parameter `SchemaDetailModel` usage) with optional `SchemaName` (component id). Extend `SkillModel` with `IReadOnlyList<ComponentSchemaModel>` (or dictionary) of `{ Name, RawJson }` collected during `SkillModelBuilder.Build`. Emitters/Readers consume the model only (Constitution III).

**Rationale**: Writers must not touch `OpenApiDocument`. Builder is the only place that resolves refs today.

### Decision: MD depth 4 + truncation note

**Choice**: Keep `DescribeSchema` / example serialization depth cap at **4**. When depth is hit and a named `SchemaName` exists, append a line such as:  
`_(Nested fields truncated at depth 4 — see [Pet](schemas/Pet.json) for the full schema.)_`

**Rationale**: Grill Q8. Draft already caps at 4; missing piece is the explicit note + schema link.

### Decision: `allOf` / `oneOf` / `anyOf` in MD

**Choice**: For MD property tables + pasteable examples: merge `allOf` property sets (current unwrap improved to union properties); for `oneOf`/`anyOf` emit a short **Variants** list (index + summary Shape) and use the **first** variant for the pasteable example with a note. Raw schema file retains full composition.

**Rationale**: Grill Q7 = A. Agents get one pasteable default without losing exclusivity semantics.

### Decision: Non-JSON media types

**Choice**: Prefer JSON media (`application/json`, then `*+json`, then first content entry — refine `PreferJsonMedia`). If selected Content-Type is non-JSON: document Content-Type + property metadata if schema present; **omit** pasteable JSON fence; one-line note per FR-011.

**Rationale**: Grill Q6 = A; dispatcher `--body` is JSON-oriented today.

### Decision: Pretty pasteable JSON in MD

**Choice**: Emit pasteable examples as indented JSON (multi-line fenced blocks) for readability when Content-Type is JSON. Deterministic whitespace (fixed indent 2).

**Rationale**: Agents paste into `--body`; single-line blobs are harder to edit. Goldens already partially single-line — update goldens when implementing.

### Decision: Version bump

**Choice**: User-facing behavior change → bump package `Version` per project rule (currently draft already at `0.6.1`; keep or bump again if 0.6.1 already published when this ships).

## Decision log

| Topic | Choice |
|-------|--------|
| Raw serialize | OpenApi `SerializeAsJsonAsync` → `reference/schemas/<Name>.json` |
| `$ref` in files | Keep `#/components/schemas/…` |
| Schema set | Reachable from filtered operations only |
| Model | `SchemaName` + `ComponentSchemaModel` list on `SkillModel` |
| MD depth | 4 + truncation note + link |
| Composition | Merge allOf; variants for oneOf/anyOf; raw untouched |
| Non-JSON | Metadata only |
| Pasteable JSON | Indented, Content-Type driven |
