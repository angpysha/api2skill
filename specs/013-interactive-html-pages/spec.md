# Feature Specification: Interactive HTML pages (host canvases / artifacts / sites)

**Feature Branch**: `feature/012-multi-spec-skill` (same branch as 012 — no new git branch)

**Created**: 2026-07-28

**Status**: Implemented — ready for human review (same branch as 012)

**Input**: User description: "Add interactive HTML page support as examples that work across Cursor Canvas, GitHub Copilot Canvas, Claude Artifacts, and Codex sites; create a new Spec Kit feature on the same branch."

## Problem & Scope

**Problem**: Wiki and README explain multi-spec flows in prose, but users and agents learn faster from a **clickable walkthrough** (choose generate vs merge vs add-spec, see flags, copy commands). Hosts already offer interactive surfaces — Cursor **Canvas**, GitHub Copilot **Canvas**, Claude **Artifacts**, Codex **sites** — but api2skill has no portable interactive HTML example or host-open guidance.

**In scope (v1)**:

- Ship **one primary interactive HTML example** in-repo that teaches **multi-spec / same base URL** workflows (generate ×N, merge, add-spec, update, auth/base URL/secrets reminders).
- Document how to **open that page** (or an equivalent view) in:
  - **Cursor** — Canvas (`.canvas.tsx` or host instructions to present the HTML)
  - **GitHub Copilot** — Canvas
  - **Claude** — Artifact (HTML)
  - **Codex** — sites
- Provide a **thin host adapter** pattern: shared content (HTML + optional JSON data) + short per-host open notes so agents know which surface to use.
- Link from README / `wiki/Multi-Spec-Skills.md` / creator skill: prefer **Plan mode** for the interview, then optionally open the interactive page as a companion.

**Out of scope (v1)**:

- Emitting interactive HTML into **every** generated API skill by default (`generate` output unchanged).
- Live calls against real APIs from the HTML page (no secrets, no network from the example — static/demo only).
- Replacing `SKILL.md` progressive disclosure with HTML inside generated skills.
- Building a full SPA framework or new NuGet dependency for the CLI.

## Resolved Decisions (defaults)

### D1 — Delivery model: docs example first, not generate-time emit

**Decided**: v1 ships **repository documentation assets** (interactive HTML + host guidance). Optional future feature may add `generate --html-guide` or similar; not required for this spec.

**Why**: Fast user value without changing the skill emit pipeline or Constitution I–V surface for every API.

### D2 — Content: multi-spec walkthrough as the first example

**Decided**: The first interactive page is a **multi-spec skill composition guide** (same base URL, different Swagger docs / auth): choose scenario → see recommended CLI → copy-paste commands → tips for secrets and Plan-mode creator.

**Why**: Matches current product focus (012) and the user’s “as an example” request.

### D3 — Portability: one HTML source of truth

**Decided**: Author **self-contained HTML** (inline CSS/JS, no build step, no external CDN required for core UX). Host-specific wrappers (e.g. Cursor `.canvas.tsx`) MAY re-embed or deep-link to that HTML; they MUST NOT diverge on command text.

**Why**: Claude Artifacts and Codex sites consume HTML naturally; Cursor/Copilot canvases can wrap or display the same content.

### D4 — Security / secrets

**Decided**: Interactive pages MUST NOT contain real credentials. Example `secrets.json` shapes use empty placeholders only. No `fetch()` to user APIs in v1.

### D5 — Cursor adapter form (grilling Q1 → **A**)

**Decided**: Ship **self-contained HTML + wiki open steps** for all hosts. A thin Cursor `.canvas.tsx` MAY be added under `docs/interactive/` (or documented project canvases path) as an **optional** adapter that surfaces the same guide — not required for v1 completeness. HTML remains the source of truth for command text.

**Rejected**: Requiring a committed Cursor canvas as the primary deliverable (less portable).

### D6 — Asset location (grilling Q2 → **A**)

**Decided**: Place the guide at **`docs/interactive/multi-spec-guide.html`** (plus README / wiki links). Optional companion files (CSS/JS only if needed) stay under `docs/interactive/`. Prefer inline CSS/JS in the single HTML file when practical (FR-006).

**Rejected**: Storing primary assets only under `wiki/assets/`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Learn multi-spec via interactive page (Priority: P1)

A developer has five Swagger docs on one host and is unsure whether to use `generate --spec`, `merge`, or `add-spec`. They open the interactive HTML guide, pick their situation, and copy the recommended commands.

**Why this priority**: Primary teaching surface for 012.

**Independent Test**: Open the shipped HTML file in a browser; complete the “new composite” path; copied command matches `wiki/Multi-Spec-Skills.md` / contracts.

**Acceptance Scenarios**:

1. **Given** the shipped interactive page, **When** the user selects “multiple OpenAPI files, new skill”, **Then** the page shows a valid `api2skill generate --spec …` example with `--base-url` guidance.
2. **Given** “already have separate skills”, **When** selected, **Then** the page recommends `merge --into` and states sources are not deleted.
3. **Given** “grow existing skill”, **When** selected, **Then** the page recommends `add-spec` (not merge).

---

### User Story 2 - Open the same guide in host canvases / artifacts / sites (Priority: P1)

An agent (or user) working in Cursor, Copilot, Claude, or Codex wants to present the guide in that host’s interactive surface.

**Why this priority**: User explicitly named all four hosts.

**Independent Test**: Wiki “Open in hosts” section lists concrete steps for each host; at least one Cursor-oriented adapter or instruction exists in-repo.

**Acceptance Scenarios**:

1. **Given** Cursor, **When** following wiki steps, **Then** the agent can open a Canvas (or equivalent) that presents the multi-spec guide content without inventing CLI flags.
2. **Given** Claude, **When** following wiki steps, **Then** the agent can load the HTML as an Artifact.
3. **Given** Copilot Canvas / Codex sites, **When** following wiki steps, **Then** the agent has enough instruction to present the same guide (link to file and/or paste HTML).

---

### User Story 3 - Creator skill points at Plan mode + interactive guide (Priority: P2)

An agent using `api2skill-creator` interviews for multi-spec setup.

**Why this priority**: Reinforces Plan mode + interactive learning.

**Independent Test**: Creator `SKILL.md` mentions Plan mode and links/paths to the interactive guide.

**Acceptance Scenarios**:

1. **Given** a multi-spec user request, **When** the creator skill is followed, **Then** it recommends Plan mode for the interview and offers the interactive HTML guide as an optional companion before emitting commands.

---

### Edge Cases

- Host cannot open local files → wiki shows “paste HTML into Artifact/Canvas” fallback.
- Offline → page works without network (no CDN dependency for core UI).
- Command text drifts from wiki → single source of truth: HTML content generated from or checked against wiki snippets (documented in plan; CI optional).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Repository MUST include a self-contained interactive HTML multi-spec guide at `docs/interactive/multi-spec-guide.html` (D6).
- **FR-002**: The guide MUST cover at least: generate multi-spec, merge, add-spec, update / `--spec-id`, shared base URL / `--base-url`, and secrets/auth reminders (placeholders only).
- **FR-003**: Wiki MUST document how to open the guide in Cursor Canvas, GitHub Copilot Canvas, Claude Artifacts, and Codex sites (including paste/fallback).
- **FR-004**: README and `wiki/Multi-Spec-Skills.md` MUST link to the interactive guide.
- **FR-005**: Bundled `api2skill-creator` MUST recommend Plan mode for multi-spec interviews and reference the interactive guide.
- **FR-006**: Interactive assets MUST NOT require a build step or commit real secrets.
- **FR-007**: v1 MUST NOT change default `api2skill generate` skill output to include HTML unless a separate explicit opt-in is added later.

### Key Entities

- **Interactive guide**: Self-contained HTML (+ optional small JSON) teaching multi-spec CLI flows.
- **Host adapter notes**: Per-host instructions (Cursor / Copilot / Claude / Codex) for presenting the guide.
- **Cursor canvas adapter** (optional file): `.canvas.tsx` or documented path that surfaces the same content inside Cursor.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user can produce a correct multi-spec CLI command using only the interactive page (no prior wiki reading) in under 5 minutes in a usability spot-check.
- **SC-002**: All four hosts have documented open steps; reviewers can follow each section without inventing flags.
- **SC-003**: The interactive page loads with network disabled (core UI and copy buttons still work).
- **SC-004**: Creator skill text mentions Plan mode and the interactive guide path.

## Assumptions

- Host product names/APIs evolve; wiki steps are “best effort” and may use paste-HTML fallbacks.
- First example content is multi-spec; additional interactive pages (auth, OAuth login) can be follow-ups.
- Same git branch as 012 is intentional so docs/examples ship with multi-spec work; Spec Kit feature id is **013**.

## Grilling log (resolved 2026-07-28)

| # | Topic | Choice |
|---|--------|--------|
| Q1 | Cursor adapter form | **A** — HTML + wiki steps; optional thin canvas (D5) |
| Q2 | Asset location | **A** — `docs/interactive/multi-spec-guide.html` (D6) |
