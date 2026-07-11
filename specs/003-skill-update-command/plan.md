# Implementation Plan: Skill Update Command

**Branch**: `feature/003-skill-update-command` | **Date**: 2026-07-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-skill-update-command/spec.md`

## Summary

Every `generate` writes a small, secret-free `.api2skill.json` manifest into the skill directory
recording the resolved name, spec source, script kind, filters, forced format, base URL, and
insecure flag. A new `update <skill-path> [<spec-source>]` command reads that manifest back,
reconstructs the equivalent `GenerateOptions`, and **delegates directly to the existing
`GenerateCommand.RunAsync`** with `Force: true` and no auth options — reusing 100% of the
already-tested acquire → parse → build → emit → write pipeline, including its staged-then-atomic-
move safety and its existing preservation of `secrets.json`/`auth.json`/`.auth-cache.json`. No new
pipeline is built; `update` is a thin options-reconstruction layer in front of `generate`.

## Technical Context

**Language/Version**: C# on .NET 10, same project (`src/Api2Skill`) as the rest of the CLI.

**Primary Dependencies**: None new — manifest I/O uses `System.Text.Json.Nodes` (`JsonObject`),
matching `SecretsScaffold`'s existing hand-built-JSON style.

**Storage**: Filesystem only. New file: `.api2skill.json` at the skill directory root (committed
— secret-free).

**Testing**: xUnit — unit (manifest serialize/parse round-trip, GenerateOptions reconstruction),
CLI exit-code tests (missing/corrupt manifest), integration (generate → mutate spec → update →
assert regenerated content + preserved secrets/auth.json, matching `ForcePreservesSecretsTests`'
existing pattern).

**Target Platform**: Same as the rest of the CLI (cross-platform .NET tool).

**Project Type**: CLI tool — adds one new command to the existing `src/Api2Skill` project.

**Performance Goals**: Not latency-critical; identical performance envelope to `generate --force`.

**Constraints**: Manifest MUST NOT contain secrets (Constitution IV — extends the existing
guarantee to a new file type). `update` MUST NOT change auth configuration (spec FR-008).

**Scale/Scope**: One new file type, one new CLI command, zero new pipeline logic (pure delegation).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | No change to generated dispatcher scripts | ✅ |
| II. .NET-native, zero unnecessary deps | `System.Text.Json.Nodes` only, already used by `SecretsScaffold` | ✅ |
| III. Pluggable emitters | Manifest records `scriptKind`; emitter selection logic in `GenerateCommand` untouched | ✅ |
| IV. Secrets never committed | Manifest is explicitly secret-free (FR-002); never records `secrets.json` contents or resolved auth values | ✅ |
| V. Progressive disclosure | Manifest is a small bookkeeping file, not loaded into `SKILL.md`/context | ✅ |
| Untrusted-HTTPS opt-in only | `insecure` flag is only ever *persisted and reused*, never defaulted on by this feature | ✅ |
| Test-first default | Unit + CLI + integration tests written alongside implementation | ✅ (enforced in tasks) |

**Result: PASS — no violations.**

## Project Structure

### Documentation (this feature)

```text
specs/003-skill-update-command/
├── spec.md              # /speckit.specify (canonical)
├── plan.md              # This file (/speckit-plan)
└── tasks.md             # /speckit-tasks (implementation checklist)
```

### Source Code (repository root)

```text
src/Api2Skill/
├── Program.cs                    # + UpdateCommand.Create() registered on RootCommand
├── Cli/
│   ├── GenerateCommand.cs        # + manifest JSON construction, passed into SkillWriter.Write
│   ├── GenerateOptions.cs        # unchanged — UpdateCommand reconstructs one from the manifest
│   └── UpdateCommand.cs          # NEW — thin: load manifest, build GenerateOptions, delegate
├── Output/
│   ├── SkillManifest.cs          # NEW — record + Serialize/TryLoad (System.Text.Json.Nodes)
│   └── SkillWriter.cs            # + optional manifestJson param, written into staging (always overwritten, no preservation logic)

tests/Api2Skill.Tests/
├── Output/
│   └── SkillManifestTests.cs     # NEW — unit: serialize/parse round-trip, missing/corrupt handling
├── Cli/
│   └── UpdateCommandTests.cs     # NEW — exit codes: missing manifest, corrupt manifest, success
└── Integration/
    └── UpdateCommandIntegrationTests.cs  # NEW — generate (non-default options) → update from a
                                            #       changed spec → assert options honored + secrets/
                                            #       auth.json/.auth-cache.json preserved
```

**Structure Decision**: No new project, no new namespace beyond one new `Output/SkillManifest.cs`
file alongside the existing `SkillWriter`. `UpdateCommand` lives in `Cli/` next to
`GenerateCommand`, matching existing convention, and depends on `GenerateCommand.RunAsync` being
`internal` (already is, via `InternalsVisibleTo` to the test project) — made accessible to
`UpdateCommand` by making it `internal` within the same assembly (no visibility change needed,
they're already in the same project).

## Design decision: delegation over duplication

**Decision**: `UpdateCommand` does not re-implement any part of the generate pipeline. It loads
the manifest, builds a `GenerateOptions`, and calls `GenerateCommand.RunAsync` directly.

**Rationale**: `GenerateOptions` already models every field the manifest needs to carry
(`SpecSource`, `ScriptKind`, `Include`, `Exclude`, `Force`, `Insecure`, `Format`, `BaseUrl`) plus
the auth fields `update` intentionally leaves `null`/`false` (`AuthConfigPath`, `AuthShorthand`,
`Login`). `GenerateCommand.RunAsync` already has full test coverage for spec acquisition failure,
parse failure, filtering, `--force` preservation, and auth handling — none of that needs
duplicate tests or duplicate implementation. This keeps `update` at roughly 30 lines of code.

**Alternatives considered**: A separate regeneration pipeline reusing only `SkillModelBuilder`/
`SkillWriter` — rejected as pure duplication of `GenerateCommand.RunAsync`'s existing
acquire/parse/error-handling logic for no benefit.

## Complexity Tracking

> No Constitution violations — section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| — | — | — |

## Phase boundary

`/speckit-plan` ends here. Next: `/speckit-tasks` produces `tasks.md`; then implementation.
