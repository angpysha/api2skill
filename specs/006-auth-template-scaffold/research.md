# Research: Auth Template Scaffold & Script Working Directory

**Feature**: `006-auth-template-scaffold` | **Date**: 2026-07-13

## R1 — Scaffold trigger and activation split

**Decision**: Auto-scaffold writes an **inactive** `auth.json` template when `generate` runs without
`--auth` / `--auth-config`, the spec has ≥1 referenced security scheme, and no existing
`auth.json` is preserved. The template is **not** parsed into `SkillModel.AuthConfig` on that
run — explicit auth activation still requires a later `--auth-config` (or supplying it on the
same run).

**Rationale**: Clarification Q2 + FR-001a. Users edit the committed template first; generation
without `--auth-config` continues to use spec-derived runtime auth until they opt in.

**Alternatives considered**:
- *Parse scaffold into model immediately* — rejected; would override spec-derived auth before
  secrets are filled, breaking first-run calls.
- *Separate subcommand* — rejected in Q1; user chose `generate`-integrated flow.

## R2 — Non-profile metadata in `auth.json`

**Decision**: Add two **top-level, loader-ignored** keys on the scaffold file only:
- `"$comment"`: short human intro (JSON string, same pattern as `secrets.example.json`).
- `"_guidance"`: structured object `{ "schemes": [ { "schemeId", "suggestedProfileName", "status", "operations", "tags" } ], "manualOnlySchemes": [...] }`.
- `"_tagAttachExamples"`: array of example profile objects (not active — copy-paste only).

`AuthConfigLoader` continues to require only `"profiles"`; unknown top-level keys are ignored by
`System.Text.Json` deserialization into `AuthConfigDto`.

**Rationale**: Standard JSON has no comments; `$comment` matches existing repo convention.
`_tagAttachExamples` satisfies Q3 (global active + tag examples) without putting invalid/inactive
entries in `profiles` (which would fail collision validation or activate unwanted auth).

**Alternatives considered**:
- *JSONC / `.auth.json.example`* — rejected; breaks “default name `auth.json` in skill folder”.
- *Comment-only blocks inside `profiles`* — rejected; invalid JSON and fails loader.

## R3 — Scheme kind → profile type mapping

**Decision**:

| `SecuritySchemeKind` | Scaffold `type` | Notes |
|----------------------|-----------------|-------|
| `Bearer` | `bearer` | `token: "{secret:<SCHEME_ID>_TOKEN}"` |
| `Basic` | `basic` | `username`/`password` secret refs |
| `ApiKey` (header) | `custom` | single header from `ApiKeyName` |
| `ApiKey` (query) | *(omit from profiles)* | listed in `_guidance.manualOnlySchemes`; query keys have no explicit-auth equivalent yet |
| `OAuth2` (client_credentials inferrable) | `oauth2` | `grant: client_credentials`, `tokenUrl` from spec |
| `OAuth2` (authorization_code only / ambiguous) | *(omit from profiles)* | `_guidance` entry + `$comment` to add oauth2 block manually |
| `Unsupported` | *(omit)* | `_guidance.manualOnlySchemes` only (Q4) |

**Rationale**: FR-004/FR-006 — active `profiles` must pass `AuthConfigLoader` today without user edits.

## R4 — Script auth working directory

**Decision**: Pass `scriptDir + "/.."` (skill root — directory containing `auth.json`) as
`ProcessStartInfo.WorkingDirectory` in all three emitters' `RunScriptCommandAsync`. Thread
`scriptDir` into the call site (already available in explicit-auth apply paths).

**Rationale**: FR-007/FR-008; `scriptDir` is `scripts/`; skill root is already resolved for
`auth.json`/`secrets.json` paths.

**Alternatives considered**:
- *Use caller cwd* — current broken behavior.
- *Use `scripts/` cwd* — rejected; user examples (`get-token.sh` next to `auth.json`) expect skill root.

## R5 — SKILL.md guidance section

**Decision**: When auto-scaffold runs, `SkillMdWriter` emits **Auth profile names** (new H2)
between existing **Auth (from the spec)** and **Explicit auth profiles** sections. Content derived
from `_guidance` — not gated on `model.AuthConfig`.

**Rationale**: Q5 — LLM-facing guidance in always-loaded SKILL.md; compact JSON mapping stays in
`auth.json`.

## R6 — `--force` preservation interaction

**Decision**: Reuse existing `SkillWriter` preservation: if `auth.json` exists at preserve source
and this run supplies no new auth config, scaffold is skipped (bytes preserved). Matches FR-001a.

**Rationale**: Consistent with 002/003 `--force` policy; no new flag surface.
