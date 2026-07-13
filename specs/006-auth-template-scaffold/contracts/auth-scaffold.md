# Contract: Auto auth.json scaffold

Emitted automatically by `generate` under the conditions in [cli.md](./cli.md). Supplements
[002 auth-config.md](../../002-explicit-auth-config/contracts/auth-config.md) — scaffold files
use the same profile schema for the active `"profiles"` array.

## Trigger conditions (all required)

1. Neither `--auth` nor `--auth-config` supplied.
2. Filtered `SkillModel` has ≥1 referenced security scheme (any kind, including unsupported).
3. No existing `auth.json` preserved this run (first generate, or target had no file; `--force`
   with existing file preserves bytes — no re-scaffold).

## Output location

`<skill-output-dir>/auth.json` — default name, no CLI path argument.

## File shape

Top-level keys (order not significant):

| Key | Required | Loader reads | Purpose |
|-----|----------|--------------|---------|
| `$comment` | yes (scaffold) | no | One-line activation hint |
| `_guidance` | yes (scaffold) | no | Scheme → profile naming map |
| `_tagAttachExamples` | when tags exist | no | Copy-paste tag-attach examples |
| `profiles` | yes | yes | Active global-attach profiles only |

## Active profile rules

- `name` === OpenAPI security scheme ID (FR-002).
- `attach` omitted or `{ "scope": "global" }` (FR-004a).
- Secret values are `{secret:NAME}` placeholders only (FR-003).
- Supported kinds only in active array (see research.md R3).
- MUST pass `AuthConfigLoader` validation as-is.

## `_guidance.schemes[]` entry

| Field | Type | Description |
|-------|------|-------------|
| `schemeId` | string | Scheme ID |
| `suggestedProfileName` | string | Same as `schemeId` |
| `status` | `"scaffolded"` \| `"manualOnly"` | Whether an active profile was emitted |
| `kind` | string | `Bearer`, `Basic`, `ApiKey`, `OAuth2`, `Unsupported` |
| `operations` | string[] | Operation IDs using this scheme |
| `tags` | string[] | Tags of those operations |

## `_tagAttachExamples[]` entry

Example object mirroring an `AuthProfile` with `"attach": { "scope": "tags", "tags": ["TagName"] }`.
Not loaded at runtime; documentation only.

## SKILL.md section

When scaffold runs, generated `SKILL.md` includes:

```markdown
## Auth profile names

…table: scheme ID | suggested profile name | status | operations/tags summary…

Activate explicit auth after editing `auth.json`:
`api2skill generate <spec> --auth-config ./auth.json --force`
```

## Non-goals (v1)

- Inferring unsupported schemes into active profiles.
- Auto tag-attach in active `profiles`.
- Replacing `secrets.example.json` scheme-keyed layout (unchanged).
