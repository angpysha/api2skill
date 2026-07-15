# Data model: Full request/response schema in reference docs

**Feature**: `011-reference-full-schema`  
**Extends**: `specs/001-openapi-to-skill/data-model.md` (intermediate `SkillModel`)

## Entities

### ComponentSchemaModel (new)

| Field | Type | Notes |
|-------|------|-------|
| `Name` | string | Key from `components.schemas` (e.g. `Pet`) |
| `RawJson` | string | Deterministic pretty JSON of the **raw** OpenAPI schema object |

Collected only for schemas **reachable** from filtered operations. Ordered alphabetically by `Name` for determinism.

### SchemaDetailModel (extend)

| Field | Type | Notes |
|-------|------|-------|
| `Summary` | string? | Shape hint / type summary |
| `Properties` | list of `SchemaPropertyModel` | Flattened dotted/`[]` paths; depth ≤ 4 |
| `ExampleJson` | string? | Pasteable schematic document (JSON when media is JSON) |
| `EnumValues` | list of string? | Root-level enum when schema is scalar enum |
| `SchemaName` | string? | Component id when sourced from `$ref` |
| `Truncated` | bool | True when nesting hit depth 4 |
| `Variants` | list of `SchemaVariantModel`? | For `oneOf`/`anyOf` display in MD |

### SchemaPropertyModel (extend)

| Field | Type | Notes |
|-------|------|-------|
| `Name` | string | Path (`category.id`, `tags[]`) |
| `Type` | string | Summary type (may include `enum { … }` or `object { … }`) |
| `Required` | bool | |
| `Description` | string? | |
| `Format` | string? | |
| `EnumValues` | list of string? | |

### SchemaVariantModel (new)

| Field | Type | Notes |
|-------|------|-------|
| `Index` | int | 0-based |
| `Summary` | string | Short shape for the variant |
| `SchemaName` | string? | If variant is a named `$ref` |

### ParameterModel (extend)

| Field | Type | Notes |
|-------|------|-------|
| `Name`, `In`, `Required`, `Type`, `Description` | existing | |
| `Format` | string? | |
| `EnumValues` | list of string? | |
| `Example` | string? | From `example` or `default` |
| `Schema` | `SchemaDetailModel`? | For object/array query params |

### RequestBodyModel / ResponseModel (extend)

| Field | Type | Notes |
|-------|------|-------|
| Existing fields | | ContentType, Required / StatusCode, Description, SchemaSummary, Schema |
| `SchemaName` | string? | Named component when applicable |

### SkillModel (extend)

| Field | Type | Notes |
|-------|------|-------|
| `ComponentSchemas` | `IReadOnlyList<ComponentSchemaModel>` | Empty when none |

## Validation / invariants

- `ComponentSchemas[].Name` unique; filesystem-safe (OpenAPI component names as used in `$ref`; reject or slug only if illegal path chars — document escape in tasks if needed).
- `RawJson` MUST round-trip structurally to source schema shape (`$ref`, composition preserved).
- MD expansion MUST NOT exceed depth 4; when `Truncated`, ReferenceWriter MUST emit truncation note.
- Anonymous schemas: `SchemaName` null → no file under `reference/schemas/`.

## Relationships

```text
SkillModel
├── Tags → Operations
│            ├── Parameters → SchemaDetail?
│            ├── RequestBody → SchemaDetail? + SchemaName?
│            └── Responses → SchemaDetail? + SchemaName?
└── ComponentSchemas[]  (named raw copies for all names referenced above / nested)
```
