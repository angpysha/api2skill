# Feature Specification: Full request/response schema in reference docs

**Feature Branch**: `011-reference-full-schema`

**Created**: 2026-07-15

**Status**: Tasks ready — see [tasks.md](./tasks.md); next `/speckit.analyze` → `/speckit.implement`

**Input**: In `reference/<tag>.md`, document **full OpenAPI input and output model information** (types, required fields, enums, nested shapes, examples) for every operation so agents and humans can call the skill without guessing payloads. Applies to path/query/header parameters, request bodies, and response bodies. `SKILL.md` stays compact (progressive disclosure, Constitution V). Persist **raw** component schemas under the skill for deeper lookup.

## Problem

Today many generated `reference/<tag>.md` files show only shallow hints (e.g. `Shape: object { name, status }` or a bare parameter type like `Integer`) without property tables, enum values, formats, nested fields, or JSON examples. An agent loading the skill later cannot reliably construct `--body` JSON or query arguments from Swagger/OpenAPI alone without opening the original spec.

This feature closes the gap between **what the OpenAPI document defines** and **what the on-demand reference doc exposes**, while keeping `SKILL.md` as an index only.

## Grill decisions (locked)

| # | Topic | Decision |
|---|-------|----------|
| 1 | Scope — which parts of each operation are fully documented? | **B** — inputs (path/query/header + request body) **and** response bodies (per status code) with full model detail |
| 2 | Nested object/array / body presentation | **Swagger-style pasteable document** — request/response bodies shown as full nested schema-shaped documents so an agent knows what to paste / expect |
| 3 | Body/response serialization vs OpenAPI file format | **Content-Type driven**: the pasteable body MUST match what the **API accepts/returns** (e.g. `application/json` → JSON fence). OpenAPI specs themselves MAY be authored as JSON or YAML — that does **not** force the reference body format. Prefer JSON media type when multiple content types exist (existing PreferJsonMedia behavior). Property tables for types/required/enums remain alongside the pasteable document. |
| 4 | `$ref` / component schema naming + persistence | **B** — show schema name (e.g. `Schema: Pet`) in reference, **expand** fields inline for paste (depth-capped), **and** emit a **schema copy** into the skill for each referenced `components.schemas` entry. Reference links to the on-disk copy. |
| 5 | Schema file layout & format | **A (revised)** — `reference/schemas/<Name>.json` containing the **raw** OpenAPI schema object for that component (as defined under `components.schemas`), not a generator-rewritten fragment. Only schemas referenced by emitted operations. Linked from `reference/<tag>.md` as e.g. `[Pet](schemas/Pet.json)`. |
| 6 | Non-JSON request/response content types | **A** — JSON-first v1: full pasteable document + property tables for `application/json` (and `*+json`); other content types document Content-Type + property metadata when present, with an explicit note that no pasteable body is emitted for non-JSON media |
| 7 | Schema composition (`allOf` / `oneOf` / `anyOf`) | **A** — reference shows a **merged** property table + one pasteable JSON for `allOf`; for `oneOf`/`anyOf` list variants with an explicit “use one of…” note. Raw composition remains intact in `reference/schemas/*.json`. |
| 8 | Maximum nesting depth | **Custom** — property tables and inline pasteable examples in `reference/<tag>.md` are capped at **depth 4**. Full detail lives in **raw** `reference/schemas/*.json` with **no depth truncation**. |

## Grill status

All blocking grill questions answered. Residual implementation detail (serialization of raw schemas via OpenAPI writer, exact truncated-depth note wording) deferred to `/speckit.plan`.

| # | Topic | Decision |
|---|-------|----------|
| 1 | Scope | Inputs + response bodies |
| 2 | Body presentation | Pasteable Swagger-style document + property tables |
| 3 | Serialization | Content-Type of API, not OpenAPI file format |
| 4 | `$ref` | Name + expand + persist schema copy |
| 5 | Schema files | `reference/schemas/<Name>.json` **raw** component schema |
| 6 | Non-JSON | Metadata only in v1 |
| 7 | Composition | Merge in MD; raw in schema files |
| 8 | Depth | **4 in MD**; **full raw** in schema files |

## User Scenarios & Testing

### User Story 1 - Agent reads reference to call an endpoint (Priority: P1)

An agent (or human) opens `reference/<tag>.md` for an operation and sees everything needed to invoke it: each path/query/header parameter with type, format, enum, required flag, and example/default when present; request body content-type, required flag, property table, and a pasteable body document; auth requirements unchanged from today.

**Why this priority**: Without complete **input** documentation the skill is unusable for real calls — the user's stated pain point.

**Independent Test**: Generate a skill from a fixture with nested request body, query enum, and path `format`; open `reference/pet.md` and confirm all inputs are documented without opening the OpenAPI source.

**Acceptance Scenarios**:

1. **Given** an operation with query parameters using `enum` and `default`, **When** reference is generated, **Then** the parameter table lists allowed enum values and shows the default/example.
2. **Given** a `POST` with a JSON request body schema (including nested objects and arrays), **When** reference is generated, **Then** the request body section lists every documented property (within depth 4) with type, required, and description, plus a pasteable JSON body.
3. **Given** a path parameter with `format: int64`, **When** reference is generated, **Then** the parameter type column reflects both JSON type and format.

---

### User Story 2 - Agent understands response shapes (Priority: P1)

For each documented response status, reference shows content-type (when present), response property table, enums, and a pasteable response document so callers know what to expect.

**Why this priority**: Response shape is part of the contract; agents validating or chaining calls need it in the same place as inputs.

**Independent Test**: Operation with `200` JSON response containing nested fields → reference shows per-status sections with property tables and example JSON.

**Acceptance Scenarios**:

1. **Given** a `200` response with `application/json` and an object schema, **When** reference is generated, **Then** a `### \`200\`` section includes a property table and pasteable JSON.
2. **Given** a response with no documented body, **When** reference is generated, **Then** the status section states that no body is documented in OpenAPI.

---

### User Story 5 - Reusable raw component schemas in the skill (Priority: P1)

When an operation body or response `$ref`s `#/components/schemas/Pet`, the generated skill includes a **raw** copy of that component schema on disk (`reference/schemas/Pet.json`) and `reference/<tag>.md` names the schema and links to it. Agents can open the schema file for full model detail (including depth beyond MD’s cap and composition keywords) without the upstream OpenAPI document.

**Why this priority**: Skills today ship no standalone schema artifacts; persisting raw schemas enables richer tooling and future features.

**Independent Test**: Generate from a spec with `Pet` and `PetInput` components → both raw JSON schema files exist under `reference/schemas/` and `reference/pet.md` links to them by name. Content matches the source `components.schemas` entries (modulo stable JSON serialization).

**Acceptance Scenarios**:

1. **Given** `addPet` request body `$ref: PetInput`, **When** skill is generated, **Then** `reference/schemas/PetInput.json` exists as the raw OpenAPI schema object and `reference/pet.md` shows `Schema: PetInput` with a relative link.
2. **Given** regenerate/update with `--force`, **When** schemas change in the OpenAPI source, **Then** on-disk schema copies are refreshed to match the raw source.

---

### User Story 3 - Progressive disclosure preserved (Priority: P1)

`SKILL.md` remains a compact tag-grouped index; full schema detail appears only in `reference/<tag>.md` and `reference/schemas/`.

**Independent Test**: Golden test asserts parameter descriptions and property tables are absent from `SKILL.md` but present in reference.

---

### User Story 4 - Regenerate/update keeps reference accurate (Priority: P2)

`generate --force` and `update` rewrite reference and schema copies from the current OpenAPI model; authored examples (feature 010) continue to link after regenerate.

**Independent Test**: Change fixture schema → regenerate → reference and `reference/schemas/*.json` reflect changes; example links still present.

## Edge Cases

- Circular or deeply nested schemas — MD capped at depth 4 with a truncation note when hit; raw schema files store the full component (source `$ref` cycles preserved as in OpenAPI); must not hang or stack-overflow
- Integer/non-string enum values on query parameters — enum values stringified for markdown tables
- Operations with no request body — explicit `**Request body**: none`
- Multiple response codes with mixed body/no-body
- `$ref` to `components.schemas` — show schema name, link to raw on-disk copy, expand inline in reference up to depth 4
- `allOf` / `oneOf` / `anyOf` — merge for reference display; raw composition in schema files
- Non-JSON media types — JSON-first v1: metadata only, no fake pasteable body
- Very large single-tag APIs — per-tag file may grow; out of scope to split files in v1
- Inline (anonymous) schemas with no component name — document fully in reference MD; no `reference/schemas/` file unless a named component is used

## Requirements

- **FR-001**: For every operation, `reference/<tag>.md` MUST document all path, query, and header parameters with: name, location (`in`), required, type, description, and when present in OpenAPI: `format`, `enum` values, `example`/`default`.
- **FR-002**: For every operation with a JSON (`application/json` or `*+json`) request body, reference MUST document content-type, required/optional, a property table, and a **pasteable body document** matching the API Content-Type. OpenAPI source format (JSON vs YAML file) MUST NOT dictate the pasteable body format.
- **FR-003**: For every documented JSON response with a body schema, reference MUST include status code, description, content-type, property table, and a pasteable response document. Responses with no body MUST say so explicitly.
- **FR-004**: Nested object and array fields MUST appear inside the pasteable body/response document in their natural nested form (as in Swagger). Property tables MAY use flattened dotted/`[]` paths for the same fields, capped per FR-013.
- **FR-005**: `SKILL.md` MUST NOT include per-parameter or per-property schema tables (Constitution V).
- **FR-006**: Generated reference and schema files MUST remain deterministic for the same OpenAPI input (NFR-4).
- **FR-007**: Schema-derived examples MUST NOT embed real secrets; placeholders only (`"string"`, `0`, first enum value, etc.).
- **FR-008**: Regenerate/update MUST refresh reference and schema copies from the current spec without breaking feature-010 example links.
- **FR-009**: For every `components.schemas` entry referenced by any emitted operation (request body, response, or nested `$ref`), the generator MUST write the **raw OpenAPI schema object** as JSON at `reference/schemas/<Name>.json` and MUST link it from `reference/<tag>.md` when that schema is used (Q5/Q8).
- **FR-010**: Raw schema files MUST preserve the source schema shape (including `$ref`, `allOf`/`oneOf`/`anyOf`, and nesting) without depth truncation — not a generator-flattened rewrite of the component.
- **FR-011**: For non-JSON request/response content types, reference MUST document the Content-Type and any available schema properties, and MUST NOT emit a misleading pasteable JSON body (Q6 = A).
- **FR-012**: Reference MUST flatten `allOf` into a single property table and pasteable document; for `oneOf`/`anyOf` MUST list each variant with a note that callers pick one (Q7 = A).
- **FR-013**: Property tables and inline pasteable examples in `reference/<tag>.md` MUST stop expanding nested structures beyond **depth 4**, emitting a clear truncation note and pointing callers to `reference/schemas/<Name>.json` when a named schema is available (Q8).

## Success Criteria

- **SC-001**: For a representative Petstore-style fixture, 100% of operations expose complete input documentation (params + body) in reference without consulting the OpenAPI file.
- **SC-002**: For the same fixture, every JSON response with a schema exposes a property table and pasteable document in reference.
- **SC-003**: Agents following reference alone can construct valid `--body` and query arguments for nested schemas in golden-fixture operations (within the MD depth cap).
- **SC-004**: `SKILL.md` stays index-only; detail lives in `reference/<tag>.md` and `reference/schemas/`.
- **SC-005**: Skills include raw `reference/schemas/<Name>.json` for every component schema used by emitted operations; files match source component schemas and are linked from tag markdown.

## Assumptions

- Primary pasteable body format follows the operation/response **page Content-Type**, not whether the OpenAPI document was loaded as `.json` or `.yaml`.
- When multiple content types exist, prefer `application/json` (existing generator behavior).
- Markdown property tables accompany pasteable documents for type, required, enum, and description.
- “Raw” schema means the OpenAPI `components.schemas.<Name>` object serialized to JSON for the skill (stable ordering for determinism); cross-`#/components/schemas/…` refs MAY be rewritten to relative `./Other.json` links in plan if needed for local browsing, without stripping structure.
- v1 does not add JSON Schema constraint keywords to MD tables (`pattern`, `minLength`, `maximum`) unless already present in the raw schema file.

## Dependencies

- **001-openapi-to-skill** — progressive disclosure layout, `ReferenceWriter`, `SkillModelBuilder`
- **010-chat-endpoint-examples** — authored example sections appended to reference; must remain compatible
