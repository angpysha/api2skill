# Phase 1 Data Model: Chat-authored endpoint examples

**Feature**: `010-chat-endpoint-examples` · **Plan**: [plan.md](./plan.md)

## 1. ExampleIdentity

| Field | Type | Notes |
|-------|------|-------|
| `OperationId` | `string` | Must match a skill operationId when adding via CLI (validate against skill model / reference index) |
| `Name` | `string` | Slug: `[a-z0-9]([a-z0-9-]*[a-z0-9])?`; default suggestion `default` if user omits |

## 2. ExampleArtifact

| Field | Type | Notes |
|-------|------|-------|
| `RequestPath` | `string?` | Relative `examples/<op>/<name>/request.json` |
| `ResponsePath` | `string?` | Relative `examples/<op>/<name>/response.json` |
| `RequestJson` | opaque file | Valid JSON object/array preferred; CLI may warn if not JSON |
| `ResponseJson` | opaque file | Same |

At least one of request/response required on add.

## 3. ExampleDiscoveryResult

| Field | Type | Notes |
|-------|------|-------|
| `Items` | list of ExampleIdentity + existing files | From filesystem scan |
| `Orphans` | list | `operationId` not in current SkillModel |

## 4. LinkBlock (emitted markdown)

Per operation in tag MD:

```markdown
**Authored examples**

| name | request | response |
|------|---------|----------|
| `happy` | [request.json](../examples/addPet/happy/request.json) | [response.json](../examples/addPet/happy/response.json) |
```

Omit missing columns/files. Section omitted if no examples for that op.

## 5. Preservation record (SkillWriter)

Binary/dir copy of entire `examples/` from preserve source into staging before finalize (same lifecycle as `auth.json` bytes, but recursive directory).
