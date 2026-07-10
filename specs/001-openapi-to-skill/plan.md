# Implementation Plan: OpenAPI → Claude Skill generator (core)

**Branch**: `001-openapi-to-skill` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-openapi-to-skill/spec.md`

## Summary

api2skill is a .NET 10 console app that reads an OpenAPI/Swagger document (file / URL / stdin),
parses it with Microsoft.OpenApi into an emitter-agnostic `SkillModel`, and emits a self-contained
Claude Skill: a compact `SKILL.md` (tag-grouped operation index), a single **dispatcher script**
(`.cs` default / `.fsx` / `.csx`) that owns base-URL, request shaping, auth (apiKey/bearer/basic/
oauth2) and TLS policy, on-demand `reference/<tag>.md`, a `secrets.example.json` template, and a
`.gitignore`. Technical approach (from research): System.CommandLine 2.0 for the CLI, a
Parse→Model→Emit pipeline with an `IScriptEmitter` abstraction, plain `HttpClient`/`System.Text.Json`
in generated code (no third-party HTTP client), and golden-file tests per emitter.

## Technical Context

**Language/Version**: C# on **.NET 10** (`net10.0`). Generated `.cs` scripts use .NET 10 file-based apps.

**Primary Dependencies**: `Microsoft.OpenApi` 3.8.0 (parse); `System.CommandLine` 2.0.x (CLI).
Generated scripts: **zero third-party deps** — `System.Net.Http` + `System.Text.Json` only.

**Storage**: Filesystem only — reads a spec, writes a skill directory. No database.

**Testing**: xUnit — unit (parse→model, auth codegen) + golden/snapshot (per-emitter) + integration
smoke (dispatcher vs. stub server). Manifest test cmd: `dotnet-unit-tests.ps1`.

**Target Platform**: Cross-platform .NET CLI (macOS/Linux/Windows). Generated skills run wherever
the chosen emitter's runner exists (.NET 10 SDK for `.cs`/`.fsx`; `dotnet-script` for `.csx`).

**Project Type**: CLI tool (single project) + generated-artifact templates.

**Performance Goals**: Not latency-critical. Generation of a 100+ operation spec should complete in
seconds; output MUST be byte-stable for a given (spec, options) pair (NFR-4).

**Constraints**: Compact always-loaded `SKILL.md` on large APIs (NFR-2); no third-party runtime dep
in generated code (NFR-3); no real credential in any generated/committed file (NFR-1); untrusted-HTTPS
opt-in off by default (NFR-6); new emitter addable without touching parse/model (NFR-5).

**Scale/Scope**: MVP = one `generate` command, three emitters, four auth schemes, three input
sources, filtering, force/overwrite policy. Target specs up to a few hundred operations.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Output is `SKILL.md` + script(s); no compiled artifact emitted; runner needs no build step (`dotnet run app.cs` compiles-on-run, not a pre-built binary shipped in the skill) | ✅ |
| II. .NET-native, zero unnecessary deps | Generator uses only Microsoft.OpenApi + System.CommandLine; **generated code uses only HttpClient + System.Text.Json** (R5) | ✅ |
| III. Pluggable emitters | `Parse → SkillModel → IScriptEmitter`; 3 emitters implement one interface; SKILL.md/reference/secrets writers are emitter-independent (R4) | ✅ |
| IV. Secrets never committed | Generator emits only `secrets.example.json` + `.gitignore`; never reads/embeds a real credential; `--force` preserves real `secrets.json` (R5, OQ-1) | ✅ |
| V. Progressive disclosure | `SKILL.md` = overview + tag-grouped index; detail in `reference/<tag>.md`; filters for subsets | ✅ |
| Untrusted-HTTPS opt-in only | Off by default; single `--insecure` flag/env var gates the `DangerousAcceptAnyServerCertificateValidator` path for BOTH spec fetch (R3) and generated calls | ✅ |
| Test-first default | xUnit; golden fixtures written before emitter code per task (Phase 6/7) | ✅ (enforced in tasks) |

**Result: PASS — no violations.** Complexity Tracking below is empty (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/001-openapi-to-skill/
├── spec.md              # /speckit.specify (canonical requirements)
├── plan.md              # This file (/speckit-plan)
├── research.md          # Phase 0 (/speckit-plan)
├── data-model.md        # Phase 1 (/speckit-plan)
├── quickstart.md        # Phase 1 (/speckit-plan)
├── contracts/           # Phase 1 (/speckit-plan)
│   ├── cli.md           # CLI command/flag surface
│   └── skill-output.md  # Generated-skill directory contract
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)

# ADRs (design decisions reserved for Phase 5)
_code_agent/20260710-openapi-to-skill/artifacts/sdlc/adr/
├── 0001-scripts-over-compiled-client.md
├── 0002-plain-httpclient-no-refit.md
└── 0003-single-dispatcher-script.md
```

### Source Code (repository root)

```text
src/
└── Api2Skill/                     # console app (net10.0)
    ├── Api2Skill.csproj
    ├── Program.cs                 # entry — System.CommandLine wiring
    ├── Cli/
    │   ├── GenerateCommand.cs     # `generate` verb + options
    │   └── GenerateOptions.cs     # parsed options record
    ├── Input/
    │   ├── SpecSource.cs          # file | url | stdin acquisition → MemoryStream
    │   └── FormatSniffer.cs       # JSON vs YAML detection
    ├── Parsing/
    │   └── OpenApiLoader.cs       # Microsoft.OpenApi LoadAsync → ReadResult
    ├── Model/
    │   ├── SkillModel.cs          # intermediate representation (see data-model.md)
    │   ├── OperationModel.cs
    │   ├── SecuritySchemeModel.cs
    │   └── SkillModelBuilder.cs   # OpenApiDocument → SkillModel (+ id/tag normalization, filters)
    ├── Emit/
    │   ├── IScriptEmitter.cs      # emitter abstraction
    │   ├── CsFileEmitter.cs       # default (.cs, dotnet run)
    │   ├── FsxEmitter.cs          # .fsx (dotnet fsi)
    │   ├── CsxEmitter.cs          # .csx (dotnet-script)
    │   ├── SkillMdWriter.cs       # SKILL.md (shared)
    │   ├── ReferenceWriter.cs     # reference/<tag>.md (shared)
    │   └── SecretsScaffold.cs     # secrets.example.json + .gitignore (shared)
    └── Output/
        └── SkillWriter.cs         # dir layout, overwrite/--force + secrets preservation

tests/
└── Api2Skill.Tests/               # xUnit
    ├── Parsing/ …                 # unit: parse → model
    ├── Emit/ …                    # golden/snapshot per emitter
    ├── Auth/ …                    # unit: per-scheme codegen
    ├── Integration/ …             # dispatcher vs stub server
    └── fixtures/                  # petstore.json, multi-auth.yaml, __approved__/ trees
```

**Structure Decision**: Single console project `src/Api2Skill` with a clear
`Input → Parsing → Model → Emit → Output` flow mirroring the Parse→Model→Emit pipeline (R4); tests
alongside in `tests/Api2Skill.Tests`. No web/mobile split — this is a CLI tool.

## Complexity Tracking

> No Constitution violations — section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Design decisions (ADRs)

Hard-to-reverse forks are recorded as ADRs and linked here:

- **ADR-0001 — Scripts over compiled client** (Constitution I; spec D1/D2)
- **ADR-0002 — Plain HttpClient, not Refit** (Constitution II; spec D2; Refit's build-time source
  generator does not run in scripts)
- **ADR-0003 — Single dispatcher script** (spec D6; centralizes auth/TLS, scales file count)

## Phase boundary

`/speckit-plan` ends here (Phase 0 + Phase 1 design artifacts). Next: `/speckit-tasks` (Phase 6)
produces `tasks.md`. Implementation (Phase 7) follows per task.
