# Implementation Plan: Interactive HTML pages

**Branch**: `feature/012-multi-spec-skill` (Spec Kit feature `013-interactive-html-pages`) | **Date**: 2026-07-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/013-interactive-html-pages/spec.md`

## Summary

Ship a **self-contained interactive HTML walkthrough** at `docs/interactive/multi-spec-guide.html` that teaches multi-spec / same-base-URL workflows (generate ×N, merge, add-spec, update). Document how to open it in **Cursor Canvas**, **GitHub Copilot Canvas**, **Claude Artifacts**, and **Codex sites**. Link from README, multi-spec wiki, and `api2skill-creator` (Plan mode + guide path). **No** change to default `generate` skill output.

## Technical Context

**Language/Version**: HTML5 + vanilla JS/CSS (inline). Docs in Markdown. Existing C# CLI unchanged for v1.

**Primary Dependencies**: None new (no NuGet, no npm build).

**Storage**: Repo files under `docs/interactive/`; wiki + README + creator template.

**Testing**: Manual quickstart (browser offline, scenario copy). Optional grep asserts in a lightweight test or CI later — not required for MVP.

**Target Platform**: Any browser; host canvases/artifacts/sites via documented open/paste.

**Project Type**: Documentation / interactive teaching asset co-shipped with 012 multi-spec work.

**Performance Goals**: Instant local load; &lt;100KB single file preferred.

**Constraints**: Constitution IV (no secrets); offline core UX; command text aligned with 012 wiki/contracts; FR-007 no generate emit.

**Scale/Scope**: One HTML guide + wiki host section + link updates + creator SKILL.md tweak; optional thin canvas example.

## Constitution Check

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Generated skills unchanged | ✅ |
| II. .NET-native / zero deps in emitters | No CLI/emitter change | ✅ |
| III. Pluggable emitters | Untouched | ✅ |
| IV. Secrets never committed | Placeholders only; no fetch | ✅ |
| V. Progressive disclosure | Guide is opt-in docs, not always-loaded SKILL.md | ✅ |
| Test-first | Quickstart manual validation; no production code path | ✅ N/A for HTML |

**Result: PASS.**

### Post–Phase 1

Contracts are content/host-open contracts only — **PASS**.

## Project Structure

### Documentation (this feature)

```text
specs/013-interactive-html-pages/
├── spec.md
├── plan.md              # this file
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── interactive-guide.md
│   └── host-open.md
└── tasks.md             # /speckit-tasks + br epic children
```

### Repo deliverables

```text
docs/interactive/
└── multi-spec-guide.html    # NEW — source of truth for interactive UX

wiki/
├── Multi-Spec-Skills.md     # + link + optional hosts section
├── Interactive-Guides.md    # NEW (or hosts section only in Multi-Spec)
├── Home.md / README.md      # index links
README.md                    # link to guide
src/Api2Skill/templates/api2skill-creator/SKILL.md  # Plan mode + guide path
```

**Structure Decision**: Docs-only feature; keep assets under `docs/interactive/` separate from `wiki/` prose and from generated skills.

## Implementation tracks

1. **HTML guide** — scenario picker + copy commands + tips (US1, FR-001/002/006)
2. **Wiki hosts** — Cursor / Copilot / Claude / Codex open + paste fallback (US2, FR-003)
3. **Links** — README, Multi-Spec-Skills, Home (FR-004)
4. **Creator** — Plan mode + guide path (US3, FR-005)
5. **Optional** — thin Cursor canvas example under `docs/interactive/` (D5)
6. **Verify** — quickstart.md scenarios; confirm generate does not emit HTML (FR-007)

## Design decisions

### DD1 — Inline single file

Prefer one HTML file with inline CSS/JS over multi-file assets unless size becomes unwieldy.

### DD2 — Command snippets mirror wiki

Copy examples from `wiki/Multi-Spec-Skills.md` when authoring; update both if CLI text changes.

## Complexity Tracking

> Empty — no Constitution violations.

## Phase 2

Run `/speckit-tasks` then implement on the same git branch as 012. Version bump: docs-only may skip package bump per project version-bump rule (docs-only commits skip bump); if shipped in same PR as 0.7.0 multi-spec behavior, no extra bump required for 013 alone.
