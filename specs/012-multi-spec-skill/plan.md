# Implementation Plan: Multi-Spec Skill Composition

**Branch**: `012-multi-spec-skill` | **Date**: 2026-07-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/012-multi-spec-skill/spec.md`

## Summary

Extend api2skill so **multiple OpenAPI documents** (same canonical base URL, often different auth) compose into **one** generated skill: unified `SKILL.md`, one dispatcher, one secrets scaffold. Add `generate --spec` (repeatable), `merge --into`, `add-spec`, and multi-part `update`. Evolve `.api2skill.json` with backward-compatible `specSources[]`. Insert `SkillModelComposer` between parse and emit (Constitution III). Refresh bundled **api2skill-creator** (interview: base URL, auth, secrets, multi-spec, self-update).

**Partial delivery**: Creator self-update (D11) — `CreatorSkillVersion`, `creator check-update`, version-aware `install-creator`, template + wiki — **already implemented** on branch; verify via quickstart §1.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`), single project `src/Api2Skill`.

**Primary Dependencies**: Existing `Microsoft.OpenApi` 3.8, `System.CommandLine` 2.0.9 — no new packages.

**Storage**: Filesystem — `.api2skill.json` (evolved), generated skill dirs, `.creator-version.json` (creator skill only).

**Testing**: xUnit — unit (`SkillModelComposer`, manifest loader), CLI exit codes, integration (generate → merge → add-spec → update), golden SKILL.md for composite fixture, creator version tests (existing).

**Target Platform**: Cross-platform .NET global tool.

**Project Type**: CLI — extend `generate` / `update`; add `merge`, `add-spec`, `creator` (partial).

**Performance Goals**: O(N) in number of spec parts; same envelope as N sequential generates plus one compose pass.

**Constraints**: Constitution I–V; secrets never in manifest; staged atomic write; legacy single-spec manifests load without migration; version bump on ship.

**Scale/Scope**: ~8–12 new/changed source files, 2 new CLI commands, 1 composer module, manifest v2, creator template (mostly done).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Still one dispatcher per skill | ✅ |
| II. .NET-native BCL | Compose + manifest use existing JSON patterns | ✅ |
| III. Pluggable emitters | Composer produces one `SkillModel`; emitters unchanged | ✅ |
| IV. Secrets never committed | Manifest secret-free; merge unions gitignored files only | ✅ |
| V. Progressive disclosure | API parts table compact; tag prefix only on collision | ✅ |
| Untrusted HTTPS opt-in | `insecure` persisted in manifest, not defaulted | ✅ |
| Test-first | Composer + CLI tests before/alongside implementation | ✅ |

**Result: PASS — no violations.**

### Post–Phase 1

Contracts add CLI/manifest/skill-output only; no auth secret surface change. Composer warnings are stderr-only. **PASS.**

## Project Structure

### Documentation (this feature)

```text
specs/012-multi-spec-skill/
├── spec.md
├── plan.md              # this file
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli.md
│   ├── manifest.md
│   └── skill-output.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks + br epic children
```

### Source Code

```text
src/Api2Skill/
├── Program.cs                         # + MergeCommand, AddSpecCommand (creator done)
├── Cli/
│   ├── GenerateCommand.cs             # + --spec ×N, --on-conflict; multi-acquire path
│   ├── GenerateOptions.cs             # + SpecParts[], OnConflict
│   ├── UpdateCommand.cs               # multi-part acquire; --spec-id
│   ├── MergeCommand.cs                # NEW
│   ├── AddSpecCommand.cs              # NEW
│   ├── CompositeGenerateService.cs    # NEW — shared regenerate-in-place
│   ├── CreatorCommand.cs              # DONE (check-update)
│   ├── CreatorSkillVersion.cs         # DONE
│   └── InstallCreatorCommand.cs       # DONE (version prompt + write)
├── Model/
│   ├── SkillModel.cs                  # + SpecParts?, OperationModel.SourceSpecId?
│   ├── SkillModelComposer.cs          # NEW — collision rules D2–D4
│   ├── SpecPartManifest.cs            # NEW
│   └── CompositionOptions.cs          # NEW
├── Output/
│   ├── SkillManifest.cs               # EXTEND — specSources[], legacy load
│   └── SkillWriter.cs                 # merge secrets union helper for merge cmd
├── Emit/
│   └── SkillMdWriter.cs               # API parts section; tag prefix; auth provenance
└── templates/api2skill-creator/
    └── SKILL.md                       # DONE (multi-spec, auth, secrets, self-update)

tests/Api2Skill.Tests/
├── Model/
│   └── SkillModelComposerTests.cs     # NEW
├── Output/
│   └── CompositeManifestTests.cs      # NEW — legacy + v2 round-trip
├── Cli/
│   ├── MergeCommandTests.cs           # NEW
│   ├── AddSpecCommandTests.cs         # NEW
│   └── CreatorSkillVersionTests.cs    # DONE
├── Integration/
│   └── CompositeSkillIntegrationTests.cs  # NEW
└── fixtures/multi-spec/               # NEW — public.json, admin.json, billing.json
```

**Structure Decision**: Single CLI project; composition is a new model-layer module; CLI commands delegate to `CompositeGenerateService` mirroring feature 003's `UpdateCommand` → `GenerateCommand` delegation.

## Implementation tracks

### Track A — Creator self-update (D11) ✅ Shipped

| Item | Status |
|------|--------|
| `CreatorSkillVersion` + `.creator-version.json` | Done |
| `creator check-update` (exit 8) | Done |
| `install-creator` update prompt | Done |
| Creator SKILL.md self-update section | Done |
| wiki/Install-Creator.md | Done |
| Unit tests | Done |

### Track B — Manifest v2 + composer (P1)

1. `SpecPartManifest`, extend `SkillManifestIo` (load legacy + serialize v2).
2. `SkillModelComposer` with base URL check, opId/scheme/tag/schema rules.
3. Unit tests with two minimal fixtures.

### Track C — `generate --spec` (P1)

1. Repeatable `--spec` / `--spec-id` / `--spec-label` / `--on-conflict`.
2. Multi-acquire in `GenerateCommand.RunAsync` → compose → existing emit path.
3. Manifest writes `specSources[]`.

### Track D — `merge` + `add-spec` (P1/P2)

1. `CompositeGenerateService` — load manifest(s), merge spec source lists, union credentials (D9), call generate path.
2. `MergeCommand`, `AddSpecCommand` registered in `Program.cs`.
3. Integration tests US2, US3.

### Track E — Multi-part `update` (P2)

1. `UpdateCommand` acquire all parts; `--spec-id` replace one (D10).
2. Integration tests US4, legacy manifest US SC-005.

### Track F — Emit + docs (P2)

1. `SkillMdWriter` API parts section (FR-010).
2. `SchemaWriter` namespacing on collision.
3. README/wiki one-liners; version bump.

## Design decisions

### DD1 — Shared `CompositeGenerateService`

**Decision**: One internal service used by `generate` (N specs), `update`, `add-spec`, and `merge` for acquire → compose → emit.

**Rationale**: Avoid four copies of collision handling and manifest rewrite.

### DD2 — Legacy manifest

**Decision**: Loader synthesizes `specSources` from `specSource`; writer always emits v2 when ≥1 part.

**Rationale**: SC-005 without migration command.

### DD3 — `merge` credential union

**Decision**: Union keys from `secrets.json` and `.auth-cache.json`; first skill in CLI argument order wins on value conflicts; stderr warning per key; no silent deletion of credential files. `auth.json`: first source that has one for a new target; preserve target on `--force` into existing.

**Rationale**: D9 / grilling Q2 / I2–I3 remediation.

### DD4 — `scriptKind` on merge (I1)

**Decision**: Fail if any source's `scriptKind` differs from the canonical choice. Canonical = existing target's `scriptKind` when `--force` into an existing composite; else all sources must match each other.

**Rationale**: Analyze remediation I1 — one rule, no silent pick.

## Parallelism

### Runtime (generate / update / merge)

| Stage | Parallel? | Notes |
|-------|-----------|-------|
| Acquire + parse + `SkillModelBuilder.Build` per part | **Yes** | `Task.WhenAll`; independent I/O/CPU |
| Base URL / opId / scheme / tag / schema collision checks | **No** | Needs all parts |
| Compose → emit | **No** | Single skill directory |

**NFR**: `CompositeGenerateService` SHOULD parallelize per-part acquire/parse/build; MUST preserve `specSources[]` order in the composed model and fail the whole operation (cancel siblings, no partial write) if any part fails. Warnings merged in stable `specId` order.

### Implementation tracks

```text
A (creator self-update) ── ✅ done; verify-only
B (manifest + composer) ──► blocks C, D, E, F
                         ├──► C (generate --spec) ──► D (merge/add-spec) ∥ E (multi update)
                         └──► F (SKILL.md API parts)  ∥ C after B lands SpecParts shape
```

| After… | Can run in parallel |
|--------|---------------------|
| **B** | **C** and **F** (disjoint files if `SpecPartSummary` fixed in B) |
| **C** | **D** and **E** |
| Do not | Start D before `CompositeGenerateService` exists (owned by C) |

**Suggested order**: B → (C ∥ F) → (D ∥ E) → polish. Track A = verification only.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Constitution IV — merge MAY read gitignored `secrets.json` / `.auth-cache.json` | US2/US3 require union/preserve of runtime credentials into the target skill dir without forcing users to re-enter secrets | Requiring users to manually copy secrets after every merge breaks SC-002 and D9; writing secrets into committed artifacts is forbidden |
| Narrow exception only | Read + union/preserve into target skill directory only | MUST NEVER embed real credentials into `SKILL.md`, `.api2skill.json`, `secrets.example.json`, examples, or reference |

## Phase 2

`tasks.md` + `br` epic/children created. Run `/speckit.analyze` before implementation gate.

**Suggested task order**: B → (C ∥ F) → (D ∥ E) → polish (Track A verify first / already shipped).
