# Research: Multi-Spec Skill Composition

**Feature**: `012-multi-spec-skill` | **Date**: 2026-07-28

## R1 — Composition layer placement

**Decision**: Insert `SkillModelComposer.Compose(...)` between parse and emit. Each spec part is parsed independently via existing `OpenApiLoader` + `SkillModelBuilder.Build`; composer unions the partial models into one `SkillModel`.

**Rationale**: Constitution III — emitters and parsers stay unchanged. Collision rules (D2–D4) live in one testable module.

**Alternatives considered**:
- *Pre-merge OpenAPI documents into one mega-spec* — rejected; loses per-part auth metadata and complicates provenance.
- *Multiple dispatchers in one skill dir* — rejected; violates single-skill mental model and duplicates base URL/secrets.

## R2 — Manifest evolution (`specSources[]`)

**Decision**: Extend `SkillManifest` / `SkillManifestIo` with `IReadOnlyList<SpecPartManifest> SpecSources`. Serialize as `specSources` array. Load path: if `specSources` present → use it; else if legacy `specSource` string → synthesize one `SpecPartManifest` with `id` = slug of skill name or last path segment.

**Rationale**: Feature 003 pattern; no migration command; `update` on legacy skills keeps working (SC-005).

**Alternatives considered**:
- *Separate `composite.json`* — rejected in spec (D1).
- *Bump manifest schema version field* — deferred; backward-compatible loader is sufficient for v1.

## R3 — Base URL normalization

**Decision**: Normalize each part's resolved base URL (after `--base-url` override applied per-part during build, then compare across parts). Compare: scheme (lowercase), host (lowercase), port (default stripped), path prefix (trim trailing slash). Mismatch → `CompositionException` listing `specId` + URL.

**Rationale**: D2 — one canonical base for dispatcher; explicit override is the escape hatch.

**Alternatives considered**:
- *Hostname-only match* — too loose (different API paths on same host).
- *Warn but continue* — rejected; breaks call correctness.

## R4 — `operationId` collision handling

**Decision**: Before emit, scan all operations across parts. Duplicates → fail with message listing `(specId, operationId)` pairs unless `--on-conflict prefix` → rename to `{specId}__{operationId}` in composed model only; add warning for example folder renames.

**Rationale**: D3 — agents call by operationId; silent merge is unsafe.

## R5 — Security scheme collision

**Decision**: Union schemes by id; on collision rename to `{specId}_{schemeId}` and rewrite `OperationModel.SecuritySchemeIds` on affected operations. Add optional `SourceSpecId` on `SecuritySchemeModel` for SKILL.md provenance column.

**Rationale**: D4; existing auth.json attach by profile name unchanged.

## R6 — Tag collision in SKILL.md

**Decision**: Union tags; if tag name appears in >1 part, prefix heading `[specId]` in `SkillMdWriter` only for colliding tags.

**Rationale**: D5 progressive disclosure.

## R7 — Component schema file collisions

**Decision**: `reference/schemas/{specId}.{schemaName}.json` on name collision; MD links updated accordingly.

**Rationale**: Spec edge case; raw schemas must not overwrite.

## R8 — `merge` vs `add-spec` implementation

**Decision**:
- `merge` loads N skill manifests + merges `specSources` lists + unions credential files (D9) + calls shared composite regenerate helper.
- `add-spec` loads one manifest, appends one `SpecPartManifest`, regenerates in place via same helper as `update`.

**Rationale**: D8 — distinct verbs, shared `CompositeGenerateService` (name TBD) to avoid duplication.

## R9 — Multi-part `update`

**Decision**: `UpdateCommand` loads manifest → acquires **all** spec sources in order → compose → emit. Optional positional + `--spec-id` replaces one part's `source` field then refreshes all (D10).

**Rationale**: Mirrors 003 delegation pattern extended to N acquisitions.

## R10 — Creator skill self-update (D11)

**Decision**: **Already implemented** on current branch: `CreatorSkillVersion`, `creator check-update` (exit 8), `install-creator` version prompt, `.creator-version.json`, bundled SKILL.md self-update section.

**Rationale**: Delivered early per user request; tasks mark as complete, verify in quickstart.

**Remaining**: golden test for bundled template content; wiki cross-links (partial).

## R11 — CLI `--spec` on `generate`

**Decision**: Add repeatable `--spec <source>` with optional paired `--spec-id` / `--spec-label` (apply to the **preceding** `--spec` or use System.CommandLine's pattern of multiple options). Keep positional `spec-source` for backward compat when only one spec; if any `--spec` provided, positional optional.

**Rationale**: Minimize breaking change; document that multi-spec prefers all `--spec` flags.

**Alternatives considered**:
- *Remove positional* — breaking; rejected.
- *@file list* — deferred.

## R12 — Exit codes for new commands

**Decision**: Reuse existing codes: composition/base-url conflict → `UsageError` (2); parse failure → `ParseFailure` (1); merge target exists → `OutputExists` (3). `creator check-update` keeps exit **8** (already shipped).

**Rationale**: Consistency with `contracts/cli.md` from 001/009.

## R13 — Parallelism (runtime + implementation)

**Decision**:
- **Runtime**: Parallelize per-part acquire/parse/build via `Task.WhenAll`; serialize compose + emit. Preserve manifest order; fail-fast on any part failure (no partial skill write).
- **Implementation**: Track B first; then C ∥ F; then D ∥ E after C. Track A already shipped.

**Rationale**: Spec fetch is the expensive step for multi-URL composites; compose cannot race. Task tracks share `CompositeGenerateService` / `SpecPartSummary` — parallelizing wrong tracks duplicates logic.

**Alternatives considered**: Fully sequential acquire (simpler, slower for N URLs) — acceptable fallback if `WhenAll` proves flaky in tests; document as optional NFR not MVP gate.
