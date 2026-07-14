# Implementation Plan: Chat-authored endpoint examples

**Branch**: `010-chat-endpoint-examples` (spec currently on `feature/009-oauth-https-callback`) | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/010-chat-endpoint-examples/spec.md`

## Summary

Add optional, multi-named **request/response example files** under `examples/<operationId>/<name>/`, linked from regenerated `reference/<tag>.md`. Support authorship via **SKILL.md chat instructions** and **`api2skill example` CLI**. Preserve `examples/` across generate/update like `auth.json`. Document fail→ask→propose→await-approval. Never auto-apply example/contract changes.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`).

**Primary Dependencies**: Existing `System.CommandLine`, `System.Text.Json` — no new packages.

**Storage**: Skill filesystem — `examples/**` (committed, secret-free by policy); links in `reference/*.md`.

**Testing**: xUnit — unit (path slug validation, link rewriter), CLI tests (`example add/list/remove/sync`), generate `--force` preservation integration, SkillMd/Reference golden deltas.

**Target Platform**: Cross-platform .NET tool + generated skills.

**Project Type**: CLI (`src/Api2Skill`) + emitters/writers.

**Performance Goals**: Negligible; scan `examples/` once per generate.

**Constraints**: Constitution I–V; no secrets in examples guidance; human approval for mutations after failed calls; Dedupe before new modules.

**Scale/Scope**: New `example` command group; `SkillWriter`/`ReferenceWriter`/`SkillMdWriter`/`UpdateCommand` preserve+link; wiki + quickstart; version bump coordinated with 009 release line.

## Constitution Check

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Examples are data + markdown; callers unchanged | ✅ |
| II. .NET-native BCL in emitters | No new deps in emitted scripts | ✅ |
| III. Pluggable emitters | Link injection in ReferenceWriter / shared helper; emitters untouched beyond Skills that already share writers | ✅ |
| IV. Secrets never committed | Guidance: no secrets in examples; scaffold never invents credentials | ✅ |
| V. Progressive disclosure | Full payloads in `examples/`; tag MD only links + short captions | ✅ |
| Test-first | Tests before CLI/writer changes in tasks | ✅ |

**Result: PASS.**

### Post–Phase 1

Contracts keep approval protocol out of silent automation. **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/010-chat-endpoint-examples/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── examples-layout.md
│   ├── cli-example.md
│   └── skill-guidance.md
└── tasks.md             # /speckit-tasks later
```

### Source Code

```text
src/Api2Skill/
├── Cli/ExampleCommand.cs          # NEW — add/list/remove/sync
├── Examples/                      # NEW — path helpers, link rewriter, discovery
│   ├── ExamplePaths.cs
│   ├── ExampleStore.cs
│   └── ExampleReferenceLinker.cs
├── Output/SkillWriter.cs          # preserve examples/ tree into staging
├── Emit/ReferenceWriter.cs        # call linker after op sections
├── Emit/SkillMdWriter.cs          # Examples + failure-protocol sections
└── Program.cs                     # register example commands
wiki/…                             # short “Examples” page or Authentication sibling
tests/Api2Skill.Tests/Examples/…
```

**Structure Decision**: New `Examples/` helper namespace (dedupe: **new**); extend SkillWriter/ReferenceWriter/SkillMdWriter (**extend**).

## Complexity Tracking

| Tension | Why | Rejected simpler |
|---------|-----|------------------|
| Re-derive links every generate | Avoid merge conflicts with schema sections | Patching markdown diffs is brittle |
| Orphan keep+warn | User data safety | Auto-delete orphans |

## Phase 0 / 1 outputs

| Artifact | Path |
|----------|------|
| Research | [research.md](./research.md) |
| Data model | [data-model.md](./data-model.md) |
| Contracts | [contracts/](./contracts/) |
| Quickstart | [quickstart.md](./quickstart.md) |

**Agent context script**: not present — skipped.

## Next command

`/speckit-tasks` (then analyze before implement). Prefer shipping after 009 merge or on a dedicated branch cut from main/009.
