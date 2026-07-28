# Tasks: Interactive HTML pages

**Input**: Design documents from `/specs/013-interactive-html-pages/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Manual quickstart only (plan) — no automated TDD gate for HTML MVP.

**Beads (`br`)**: Parent epic and story children — see [Beads mapping](#beads-mapping-br).

**Organization**: By user story after shared setup. Docs-only; parallelize US2/US3 after HTML exists.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallelizable (different files, no incomplete deps)
- **[Story]**: `[US1]`…`[US3]` for story phases only

---

## Beads mapping (`br`)

| Phase / story | `br` id | Title |
|---------------|---------|-------|
| Epic (parent) | `api2skill-013-interactive-html-ep-ts8` | 013: Interactive HTML pages |
| Phase 1–2 Setup | `api2skill-013-interactive-html-ep-ts8.1` | 013-P12: docs/interactive scaffold |
| Phase 3 US1 HTML guide | `api2skill-013-interactive-html-ep-ts8.2` | 013-US1: multi-spec interactive HTML |
| Phase 4 US2 Host open docs | `api2skill-013-interactive-html-ep-ts8.3` | 013-US2: host open steps (Canvas/Artifact/Sites) |
| Phase 5 US3 Creator + links | `api2skill-013-interactive-html-ep-ts8.4` | 013-US3: Plan mode + guide links |
| Phase 6 Polish | `api2skill-013-interactive-html-ep-ts8.5` | 013-PL: optional canvas + quickstart verify |

**Deps**: `.2` blocked on `.1`. `.3` and `.4` blocked on `.2` (parallel). `.5` blocked on `.2`–`.4`.

Ready: `br ready` → claim `.1` first.

---

## Phase 1: Setup

**Purpose**: Create asset directory

**Beads**: `.1`

- [x] T001 Create directory `docs/interactive/` and add a short `docs/interactive/README.md` describing the multi-spec guide purpose and offline/no-secrets rules

---

## Phase 2: Foundational

**Purpose**: Align command snippets with existing wiki before authoring HTML

**Beads**: `.1`

- [x] T002 Extract canonical CLI examples from `wiki/Multi-Spec-Skills.md` (generate ×N, merge, add-spec, update) into a checklist note at top of implementation (comments in HTML later) so FR-002 matches 012 contracts

**Checkpoint**: Path and snippet sources ready — US1 can start

---

## Phase 3: User Story 1 — Learn multi-spec via interactive page (Priority: P1) 🎯 MVP

**Goal**: Self-contained `docs/interactive/multi-spec-guide.html` with scenario picker and copyable commands

**Independent Test**: `open docs/interactive/multi-spec-guide.html` — select each scenario; commands match wiki

**Beads**: `.2` (depends on `.1`)

- [x] T003 [US1] Author `docs/interactive/multi-spec-guide.html` per `contracts/interactive-guide.md` — title, scenario picker (`generate-multi`, `merge`, `add-spec`, `update`), command panel, Copy button, tips (base URL, secrets placeholders, Plan mode)
- [x] T004 [US1] Inline CSS/JS only in `docs/interactive/multi-spec-guide.html` — no CDN required for core UX; no `fetch()`; empty-string secret placeholders only (FR-006, D4)
- [x] T005 [US1] Ensure command blocks include: `generate --spec`, `merge --into`, `add-spec`, `update` / `--spec-id`, and `--base-url` guidance matching `wiki/Multi-Spec-Skills.md`
- [x] T006 [US1] Add in-page pointer to wiki host-open docs (path/name to be finalized in US2)

**Checkpoint**: MVP — interactive page usable in any browser

---

## Phase 4: User Story 2 — Open guide in host canvases / artifacts / sites (Priority: P1)

**Goal**: Document Cursor Canvas, Copilot Canvas, Claude Artifacts, Codex sites (+ paste fallback)

**Independent Test**: Reviewer follows each host subsection without inventing CLI flags

**Beads**: `.3` (depends on `.2`; **parallel with `.4`**)

- [x] T007 [P] [US2] Create `wiki/Interactive-Guides.md` per `contracts/host-open.md` with sections for Cursor, GitHub Copilot, Claude, Codex (open path + paste-HTML fallback)
- [x] T008 [P] [US2] Link Interactive Guides from `wiki/Multi-Spec-Skills.md`, `wiki/Home.md`, and `wiki/README.md`
- [x] T009 [US2] Document that agents must copy commands from the guide/wiki and prefer Plan mode before running CLI

**Checkpoint**: All four hosts documented

---

## Phase 5: User Story 3 — Creator Plan mode + links (Priority: P2)

**Goal**: Creator skill + README point at Plan mode and the HTML guide

**Independent Test**: greps in quickstart §4 pass

**Beads**: `.4` (depends on `.2`; **parallel with `.3`**)

- [x] T010 [P] [US3] Update `src/Api2Skill/templates/api2skill-creator/SKILL.md` — Plan mode for multi-spec interviews; reference `docs/interactive/multi-spec-guide.html`
- [x] T011 [P] [US3] Link the interactive guide from `README.md` (multi-spec section) and `wiki/Multi-Spec-Skills.md`
- [x] T012 [US3] Confirm Install-Creator / Getting-Started mention Plan mode or link to Interactive-Guides if appropriate (`wiki/Install-Creator.md`, `wiki/Getting-Started.md`)

**Checkpoint**: US3 complete — agents steered to Plan mode + guide

---

## Phase 6: Polish & cross-cutting

**Beads**: `.5` (depends on `.2`–`.4`)

- [x] T013 [P] Optional Cursor canvas example — **skipped** (D5 optional; HTML is source of truth; wiki documents Canvas paste)
- [x] T014 Run `specs/013-interactive-html-pages/quickstart.md` scenarios (offline load, scenario commands, greps)
- [x] T015 [P] Smoke FR-007: `generate` does not emit HTML into skill output (quickstart §5)
- [x] T016 Mark feature ready for human review; close eligible `br` children; leave epic open for PR

---

## Dependencies & execution order

```text
.1 Setup ──► .2 US1 HTML (MVP)
              ├──► .3 US2 hosts  ∥  .4 US3 creator/links
              └──► .5 Polish
```

### Parallel opportunities

- After `.2`: `.3` ∥ `.4`
- Within phases: tasks marked **[P]**

### MVP

**Phase 1–3 (T001–T006)** — interactive HTML alone delivers US1.

---

## Implementation strategy

1. Scaffold `docs/interactive/`.
2. Ship HTML guide (MVP).
3. Parallel: host wiki + creator/README links.
4. Quickstart verify; optional canvas; human review.
