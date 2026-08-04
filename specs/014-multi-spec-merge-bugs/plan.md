# Implementation Plan: Multi-spec merge/generate bugfixes

**Branch**: `fix/014-multi-spec-merge-bugs` | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

## Technical Context

- **Language**: C# / .NET 10 (`src/Api2Skill`)
- **Testing**: xUnit in `tests/Api2Skill.Tests`
- **Constraints**: Native AOT-friendly (no new reflection); version bump to 0.7.1

## Constitution Check

- Secrets stay out of manifests/caches (raw OpenAPI only — no credentials).
- Fail loud on unusable composite base URL (no silent `file://`).
- Small, reviewable changes with tests per bug.

## Design

| Bug | Approach |
|-----|----------|
| 1 | Shared `ReferencePaths.SanitizeTagFileName`; use in `ReferenceWriter` + `SKILL.md` links; collision suffix |
| 2 | `SpecCache` under `.api2skill/specs/{id}.{ext}`; write on emit; `MergeCommand` rewrites part sources to cache when present |
| 3 | `SkillModelComposer.NormalizeBaseUrl` treats `file:` / non-http(s) as unset; pick first HTTP(S); else throw asking for `--base-url`; merge picks best manifest base |
| 4 | After compose, rebuild `_guidance` via `AuthScaffold` and merge into preserved/explicit auth.json |
| 5–6 | `AttachmentResolver` takes per-op scheme ids; unscoped profile whose name ∈ scheme ids → scheme-scoped; bake profiles after scaffold; unused-profile warning |

## Complexity Tracking

None — extends existing types; no new packages.
