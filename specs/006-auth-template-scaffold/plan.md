# Implementation Plan: Auth Template Scaffold & Script Working Directory

**Branch**: `006-auth-template-scaffold` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-auth-template-scaffold/spec.md`

## Summary

Extend `generate` to **automatically write an inactive, secret-free `auth.json` template** into
the skill output folder when no explicit auth is supplied and the spec references security
schemes — profile names match OpenAPI scheme IDs, global attach only, with `_guidance` /
`_tagAttachExamples` metadata and a new **Auth profile names** section in `SKILL.md`. Fix **script
auth** so subprocess `WorkingDirectory` is the skill root (parent of `scripts/`) across all three
emitters. No new CLI flags; `--force` preservation policy unchanged.

## Technical Context

**Language/Version**: C# on **.NET 10** (`net10.0`); generated `.cs` / `.fsx` / `.csx` dispatchers.

**Primary Dependencies**: Unchanged — `Microsoft.OpenApi`, `System.CommandLine`,
`System.Text.Json` source-gen (`AuthConfigJsonContext`). Scaffold serialization may use
`JsonObject` (like `SecretsScaffold`) for metadata keys.

**Storage**: Filesystem — new committed artifact path: auto-written `<skill>/auth.json` (inactive
template). No change to gitignore rules.

**Testing**: xUnit — unit (`AuthScaffold` mapping, JSON validates via `AuthConfigLoader`), CLI
(scaffold/no-scaffold/preserve), integration (script cwd sentinel), extend `NoSecretLeakageTests`.

**Target Platform**: Cross-platform .NET CLI.

**Project Type**: CLI tool (`src/Api2Skill`) + generated artifacts.

**Performance Goals**: Negligible — one extra JSON write per qualifying generate.

**Constraints**: Constitution IV (placeholders only); inactive scaffold must not alter runtime
auth until `--auth-config`; byte-stable regeneration when `auth.json` preserved.

**Scale/Scope**: 1 new generator module (`AuthScaffold`), `SkillModel` + `SkillWriter` +
`GenerateCommand` + `SkillMdWriter` touch; 3 emitter script-cwd edits; docs/wiki updates.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Script cwd fix is runtime-only in emitted scripts; no build step | ✅ |
| II. .NET-native, zero unnecessary deps | Scaffold uses existing STJ + model types; no new packages | ✅ |
| III. Pluggable emitters | Scaffold in generator layer; cwd fix applied per emitter (same pattern as auth engine) | ✅ |
| IV. Secrets never committed | Scaffold uses `{secret:…}` only; `_guidance` has no credentials | ✅ |
| V. Progressive disclosure | New SKILL.md section compact; detail in `auth.json._guidance` | ✅ |
| Test-first default | Tests before/alongside implementation per tasks.md | ✅ |

**Result: PASS — no violations.**

## Project Structure

### Documentation (this feature)

```text
specs/006-auth-template-scaffold/
├── spec.md
├── plan.md              # This file
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── auth-scaffold.md
│   ├── cli.md
│   └── dispatcher-script-auth.md
├── tasks.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/Api2Skill/
├── Auth/
│   ├── AuthScaffold.cs           # NEW — build scaffold JSON + guidance from SkillModel
│   └── AuthScaffoldGuidance.cs   # NEW — guidance records (or colocate in AuthScaffold.cs)
├── Model/
│   └── SkillModel.cs             # + AuthScaffoldGuidance?
├── Output/
│   └── SkillWriter.cs            # write scaffold auth.json when conditions met
├── Cli/
│   └── GenerateCommand.cs        # compute scaffold; pass to SkillWriter; set model guidance
├── Emit/
│   ├── SkillMdWriter.cs          # Auth profile names section
│   ├── CsFileEmitter.cs          # RunScriptCommandAsync(skillRoot)
│   ├── FsxEmitter.cs             # same
│   └── CsxEmitter.cs             # same

tests/Api2Skill.Tests/
├── Auth/
│   └── AuthScaffoldTests.cs      # NEW — mapping + loader validation
├── Cli/
│   └── AuthScaffoldCliTests.cs   # NEW — generate writes/skips/preserves
└── Integration/
    └── DispatcherScriptAuthTests.cs  # extend — cwd sentinel test
```

**Structure Decision**: Single project; scaffold logic lives in `Auth/` next to `AuthConfigLoader`.

## Complexity Tracking

No constitution violations. Tag-attach examples deferred to metadata array (not active profiles)
to avoid collision-validation complexity — documented in research.md R2.

## Phase 0 & 1 Outputs

- [research.md](./research.md) — R1–R6 resolved
- [data-model.md](./data-model.md) — entities + lifecycle
- [contracts/](./contracts/) — scaffold, CLI, dispatcher cwd
- [quickstart.md](./quickstart.md) — manual validation scenarios

**Post-design Constitution re-check**: PASS (unchanged).

## Implementation Notes

1. **Inactive scaffold**: `GenerateCommand` passes scaffold JSON to `SkillWriter` as a new
   parameter distinct from `authConfigJson` (e.g. `scaffoldAuthJson`) OR writes via dedicated
   branch that does **not** set `model.AuthConfig`.
2. **Loader validation**: Unit-test that stripping `_guidance` / `_tagAttachExamples` / `$comment`
   leaves valid `profiles` for each fixture.
3. **Golden fixtures**: Regenerate approved emitters after script cwd change (T022).
4. **US3 parallel**: Script cwd emitter edits are independent per file — parallelize in tasks.
