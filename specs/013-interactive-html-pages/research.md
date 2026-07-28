# Research: Interactive HTML pages

**Feature**: `013-interactive-html-pages` | **Date**: 2026-07-28

## R1 — Docs asset vs generate emit

**Decision**: Ship repository HTML + wiki only (D1). Do not change `generate` default output (FR-007).

**Rationale**: Teaching surface for 012 without touching skill emit / Constitution skill layout.

**Alternatives considered**: `generate --html-guide` into every skill — deferred.

## R2 — Single self-contained HTML

**Decision**: One file `docs/interactive/multi-spec-guide.html` with inline CSS/JS; no CDN for core UX (D3, D6, FR-006).

**Rationale**: Works offline (SC-003); pasteable into Claude Artifacts / Codex sites; openable in any browser.

**Alternatives considered**: Multi-file SPA with build — rejected (FR-006). CDN Tailwind — rejected for offline core UI.

## R3 — Scenario-driven UI

**Decision**: Radio/select scenarios (new composite / merge existing / add-spec / update) → show copyable command blocks + short tips (base URL, secrets, Plan mode).

**Rationale**: Maps 1:1 to US1 and `wiki/Multi-Spec-Skills.md` “Choose a workflow” table.

**Alternatives considered**: Free-form chatbot in-page — out of scope.

## R4 — Host open documentation

**Decision**: Wiki page section (or `wiki/Interactive-Guides.md`) with four host subsections: Cursor Canvas, Copilot Canvas, Claude Artifact, Codex sites — each with “open file / paste HTML” fallback (FR-003).

**Rationale**: Host APIs differ and change; documented steps + paste fallback is enough for v1.

**Alternatives considered**: Automating host APIs via CLI — not available / out of scope.

## R5 — Optional Cursor `.canvas.tsx`

**Decision**: Optional only (D5). If shipped, live under `docs/interactive/` as a **template/example** noting that Cursor IDE canvases normally live in the host canvases directory — wiki explains copying or opening HTML in browser / pasting into a canvas. Do not require Cursor-managed path in-repo for MVP.

**Rationale**: Repo cannot reliably own `~/.cursor/projects/.../canvases/`; HTML is source of truth.

## R6 — Drift control

**Decision**: Document that command snippets in HTML MUST match `wiki/Multi-Spec-Skills.md`. Optional later: lightweight test that greps key flag strings. No CI gate required for v1 MVP.

**Rationale**: Avoid dual sources of truth; keep process light.
