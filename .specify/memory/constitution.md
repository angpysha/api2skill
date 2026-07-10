<!--
Sync Impact Report
- Version change: [TEMPLATE] → 1.0.0 (initial ratification)
- Modified principles: n/a (first fill from template placeholders)
- Added sections: Core Principles I–V, Additional Constraints, Development Workflow, Governance
- Removed sections: none
- Templates requiring updates:
  - .specify/templates/plan-template.md ⚠ pending (generic template has no repo-specific
    Constitution Check section yet; review when Phase 5 plan is first drafted for
    specs/001-openapi-to-skill)
  - .specify/templates/spec-template.md ✅ no changes needed (generic, already used successfully)
  - .specify/templates/tasks-template.md ⚠ pending (review task categories against Principle IV
    — Secrets Never Committed — when tasks.md is first generated)
  - .specify/templates/checklist-template.md ✅ no changes needed (generic)
- Follow-up TODOs: none — all fields derived from PROJECT.md and specs/001-openapi-to-skill/spec.md
  (decisions D1–D7)
-->

# api2skill Constitution

## Core Principles

### I. Scripts, Not Compiled Clients
Every generated Claude Skill MUST consist of a `SKILL.md` plus runnable scripts — never a
compiled or typed client library. Calls MUST be deterministic and MUST NOT require a build step
at call time. Any change that would force a generated skill to be compiled before use is a
constitutional violation and requires an amendment, not a workaround.

**Rationale**: The core value proposition is "drop a skill in and use it immediately." A
compiled artifact reintroduces the build/runtime dependency this project exists to avoid
(spec.md D1, D2).

### II. .NET-Native, Zero Unnecessary Dependencies
The generator and every emitted script stay on .NET. Generated scripts perform HTTP exclusively
via `System.Net.Http.HttpClient` and `System.Text.Json` — no third-party HTTP client
dependency (e.g. Refit, Flurl, RestSharp) MAY ship in emitted code by default. Third-party
clients are permitted only as an explicitly opted-in, non-default emitter.

**Rationale**: Plain `HttpClient` is the only client class of tool proven to work inside
`.csx`/`.fsx`/file-based `.cs` scripts without a source-generator build step (Refit was
evaluated and rejected for this reason — spec.md D2). Zero-dependency scripts are also the
most portable across machines.

### III. Pluggable Emitters
The generator pipeline MUST be structured as parse → intermediate model → emit, and new
emitters (additional script kinds, or future output types) MUST be addable without modifying
the parser or the intermediate model. The three MVP emitters (`.cs` file-based, `.fsx`, `.csx`)
are concrete instances of this abstraction, not special-cased into the core.

**Rationale**: Validates the extensibility the product promises from day one — bash, Python, or
a compiled-client emitter must be addable later purely as new emitters (spec.md D2, D3, FR-006).

### IV. Secrets Never Committed
The generator MUST NOT read, embed, or hardcode any real credential during generation. Every
generated skill ships only a template (e.g. `secrets.example.*`) plus a `.gitignore` entry
excluding the real per-skill secrets file. Regenerating an existing skill (`--force`) MUST
preserve any real secrets file already present — it is never overwritten or deleted.

**Rationale**: api2skill generates artifacts meant to be committed and shared; a generator that
could leak a real credential into a generated file defeats the tool's purpose (spec.md D4,
FR-003b, FR-009).

### V. Progressive Disclosure for Scale
`SKILL.md` MUST stay compact: an API overview, auth setup, and a tag-grouped operation index.
Full parameter and schema detail MUST live in on-demand `reference/<tag>.md` files, never
inlined into `SKILL.md`. A generated skill MUST remain usable (within a workable token budget)
against APIs with hundreds of operations, using `--include`/`--exclude` filters where needed.

**Rationale**: A skill's primary consumer is an LLM context window; an always-loaded document
that scales linearly with API size breaks the product on any non-trivial real-world API
(spec.md D6).

## Additional Constraints

- **Untrusted HTTPS is opt-in only.** Generated scripts and spec-fetching MAY accept
  self-signed/invalid TLS certificates, but only behind an explicit, off-by-default flag or
  env var, and the generated `SKILL.md` MUST label it as dev-only (spec.md D2, FR-007).
- **Parsing library**: OpenAPI/Swagger documents are parsed via Microsoft.OpenApi. Officially
  supported and tested versions are 3.0 and 3.1; 2.0 (Swagger) is accepted; 3.2 is best-effort
  (spec.md D5).
- **Default delivery lane is `full`** (`pipeline.manifest.json` → `project.default_lane`) — all
  SDLC phases (requirements, design, tasks, dev, test) apply to this project; no phase is
  skipped by default.
- **Test-first is the standing default.** Unless a specific feature spec states otherwise, the
  developer agent follows TDD: tests written and failing before implementation.

## Development Workflow

- Every feature is driven through Spec Kit: `spec.md` (business, via `/speckit.specify` /
  `/speckit.clarify`) → `plan.md` (technical, via `/speckit.plan`) → `tasks.md`
  (`/speckit.tasks`) → implementation (`/speckit.implement`). These artifacts are canonical;
  session SDLC docs (REQ/SDD/TDD/TEST, ADRs) supplement and reference them, never contradict
  them.
- Hard-to-reverse technical forks (e.g. the scripts-vs-compiled-client choice, or the
  single-dispatcher-script design) are recorded as ADRs linked from `plan.md`.
- Codebase-search (MCP/graphify/filesystem, per `rules/code-search.mdc`) is the only valid
  source for "does X already exist?" claims; no new file/module/function is added without a
  Dedupe Ticket (`reuse | extend | new`).

## Governance

This constitution supersedes ad-hoc practices for this repository. Amendments require: (1) a
documented rationale for the change, (2) a version bump per semantic versioning (MAJOR for
backward-incompatible principle removal/redefinition, MINOR for new/materially-expanded
principles, PATCH for clarifications), and (3) a Sync Impact Report noting which templates and
dependent artifacts were reviewed.

All plans (`plan.md`) and PR/code reviews MUST verify compliance with the Core Principles above.
Any deviation MUST be justified in the plan's Complexity Tracking / equivalent section, not
silently introduced. Use `AGENTS.md` and `.agents/SKILL.md` for day-to-day pipeline operating
guidance; this document governs product-level principles, not tooling mechanics.

**Version**: 1.0.0 | **Ratified**: 2026-07-10 | **Last Amended**: 2026-07-10
