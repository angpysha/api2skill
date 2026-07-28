# Data Model: Multi-Spec Skill Composition

**Feature**: `012-multi-spec-skill` | **Date**: 2026-07-28

## SpecPartManifest

One OpenAPI source within a composite skill. Persisted in `.api2skill.json` → `specSources[]`.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | string | yes | Stable slug; unique within manifest; `[a-z0-9-]+` |
| `source` | string | yes | File path, URL, or `-` (same semantics as generate) |
| `label` | string | no | Display name; defaults to `id` |

**Validation**:
- Duplicate `id` in one invocation → fail before parse.
- `id` defaults from `--spec-id` or slug of source filename.

## CompositeManifest (extends SkillManifest)

Secret-free `.api2skill.json` record.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | yes | Skill name (unchanged) |
| `specSources` | `SpecPartManifest[]` | yes (v2) | Ordered list of parts |
| `specSource` | string | legacy | Single source; loader maps to one-element `specSources` |
| `scriptKind` | string | yes | `cs` \| `fsx` \| `csx` |
| `include` | string[] | yes | Global filters (unchanged) |
| `exclude` | string[] | yes | Global filters (unchanged) |
| `format` | string? | no | Forced input format |
| `baseUrl` | string? | no | Canonical base after composition |
| `insecure` | bool | yes | Dev TLS flag |

## ParsedSpecPart (runtime)

| Field | Type | Notes |
|-------|------|-------|
| `Manifest` | `SpecPartManifest` | Provenance |
| `Document` | loaded OpenAPI | Not persisted |
| `PartialModel` | `SkillModel` | Per-part build before compose |
| `Warnings` | `string[]` | Per-part warnings |

## CompositionOptions

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `OnOperationIdConflict` | `fail` \| `prefix` | `fail` | Maps to `--on-conflict` |
| `BaseUrlOverride` | string? | null | `--base-url` |

## ComposedSkillModel (runtime output)

Same shape as `SkillModel` today, plus:

| Extension | Location | Purpose |
|-----------|----------|---------|
| `SpecParts` | new property on `SkillModel` or sidecar | `id`, `label`, `source`, `operationCount` for API parts table |
| `SourceSpecId` on operations | `OperationModel` optional field | Internal provenance; warnings only unless needed |
| Renamed operationIds | composed ops | After prefix conflict resolution |

## SkillModel changes

```text
SkillModel
├── ... existing fields ...
└── SpecParts: IReadOnlyList<SpecPartSummary>?   # null/empty when single-part

SpecPartSummary
├── Id: string
├── Label: string
├── Source: string
└── OperationCount: int

OperationModel (+ optional)
└── SourceSpecId: string?   # set during compose

SecuritySchemeModel (+ optional)
└── SourceSpecId: string?   # for SKILL.md auth table
```

## CreatorSkillVersion (shipped)

| Field | File | Notes |
|-------|------|-------|
| `version` | `.creator-version.json` | Matches api2skill package `Version` |

Not part of generated API skills — only under `api2skill-creator/` agent skill folder.

## Merge credential union (runtime)

When merging skill directories:

| File | Rule |
|------|------|
| `secrets.json` | Union keys; first source wins on value conflict + CLI warning; no silent deletion of the file (D9 / SC-002) |
| `auth.json` | **New target**: copy from first source that has `auth.json`; warn if other sources differ. **`--force` into existing**: preserve target `auth.json` (update-like) |
| `.auth-cache.json` | Same as `secrets.json` union policy (union keys; first-wins + warning) |
| `examples/` | Union by `operationId` directory name |
| `scriptKind` | Fail if any source differs from canonical (existing target when force into composite; else all sources must match) |

## State: composition pipeline

```text
[SpecPartManifest...]
       │
       ▼
  For each: Acquire → Parse → SkillModelBuilder.Build
       │
       ▼
  SkillModelComposer.Compose(parts, CompositionOptions)
       │  (base URL check, opId/scheme/tag/schema collisions)
       ▼
  SkillModel (single)
       │
       ▼
  Emitters + SkillMdWriter + SkillWriter (unchanged contract)
```
