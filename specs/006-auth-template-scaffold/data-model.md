# Data Model: Auth Template Scaffold & Script Working Directory

**Feature**: `006-auth-template-scaffold`

## 1. Generator-side domain (new)

### `AuthScaffoldGuidance`

Structured naming guidance attached to a scaffold run (also serialized into `auth.json._guidance`
and rendered in SKILL.md).

| Field | Type | Description |
|-------|------|-------------|
| `Schemes` | `IReadOnlyList<SchemeGuidanceEntry>` | One row per referenced scheme in the filtered model |
| `TagAttachExamples` | `IReadOnlyList<TagAttachExample>` | Per-tag example profiles (not active) |

### `SchemeGuidanceEntry`

| Field | Type | Description |
|-------|------|-------------|
| `SchemeId` | `string` | OpenAPI `components.securitySchemes` key |
| `SuggestedProfileName` | `string` | Always equals `SchemeId` (FR-002) |
| `Status` | `SchemeScaffoldStatus` | `Scaffolded` \| `ManualOnly` |
| `Kind` | `SecuritySchemeKind` | From existing model |
| `OperationIds` | `IReadOnlyList<string>` | Ops referencing this scheme (deduped) |
| `Tags` | `IReadOnlyList<string>` | Tags of those ops (deduped) |

### `SchemeScaffoldStatus`

- **`Scaffolded`**: appears in active `profiles` array
- **`ManualOnly`**: listed only in `_guidance` (unsupported, query apiKey, ambiguous oauth2)

### `TagAttachExample`

| Field | Type | Description |
|-------|------|-------------|
| `Tag` | `string` | OpenAPI tag name |
| `SchemeIds` | `IReadOnlyList<string>` | Schemes used by ops under this tag |
| `ExampleProfiles` | `IReadOnlyList<AuthProfile>` | Suggested tag-scoped profile shapes (domain objects, serialized to `_tagAttachExamples`) |

### `AuthScaffoldResult`

| Field | Type | Description |
|-------|------|-------------|
| `Json` | `string` | Full scaffold file content (profiles + metadata) |
| `Guidance` | `AuthScaffoldGuidance` | For SKILL.md rendering |

## 2. Extensions to existing types

### `SkillModel` (extend)

| Field | Type | When set |
|-------|------|----------|
| `AuthScaffoldGuidance` | `AuthScaffoldGuidance?` | Auto-scaffold ran this generation (template written, explicit auth inactive) |

Unchanged: `AuthConfig` still null until user passes `--auth-config`.

## 3. Scaffold file shape (committed artifact)

```jsonc
{
  "$comment": "Edit profiles, then activate with: api2skill generate ... --auth-config ./auth.json",
  "_guidance": { "schemes": [ /* SchemeGuidanceEntry */ ], "manualOnlySchemes": [ /* ids */ ] },
  "_tagAttachExamples": [ /* example AuthProfile objects */ ],
  "profiles": [ /* active global-attach profiles only */ ]
}
```

**Validation**: `AuthConfigLoader.Load` reads only `profiles`; file MUST validate when metadata
keys are stripped.

## 4. Runtime (generated dispatcher)

No new domain types. **Behavior change**: `RunScriptCommandAsync(command, skillRoot)` sets
`WorkingDirectory = skillRoot` where `skillRoot = Path.GetFullPath(Path.Combine(scriptDir, ".."))`.

## 5. State / lifecycle

```text
generate (no --auth/--auth-config, has schemes, no existing auth.json)
  → write inactive auth.json scaffold + SKILL.md guidance
  → AuthConfig null, spec-derived auth at runtime

user edits auth.json + secrets.json

generate --force --auth-config ./skill/auth.json
  → AuthConfig loaded, explicit auth active, auth.json preserved/replaced per 002 policy
```
