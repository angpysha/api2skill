<!-- agentic-pipeline:begin -->
# Agent index

Universal agent-orchestrated SDLC pipeline for this repository.

## Bootstrap

If `pipeline.manifest.json` shows `"status": "needs_adapt"`, run the native skill:

```text
/pipeline-adapt mode=init
```

Skill: [.agents/skills/pipeline-adapt/SKILL.md](.agents/skills/pipeline-adapt/SKILL.md) (strict
`key=value` parameters; mechanical steps run through `agentic-tool recommend` / `update`).

**Greenfield / empty repo:** adapt runs **project-intake** first — ensures `PROJECT.md`
(Vision + Stack) exists before recommending packs and agents.

## Layout (one copy, many IDEs)

All skills, agents and rules live once in `.agents/`. Each IDE folder in `pipeline.manifest.json`
→ `hosts` (`.cursor/`, `.claude/`, `.windsurf/`) holds **relative symlinks** into `.agents/`.
Edit the `.agents/` file (or add a `## local overlay` section) — never a host copy.

## Spec-Driven Development (Spec Kit)

This pipeline runs on **[GitHub Spec Kit](https://github.github.com/spec-kit/)**. Every phase
drives the matching `/speckit.*` command; `spec.md`/`plan.md`/`tasks.md` are canonical.
Install once and initialize per repo:

```text
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git
specify init . --integration <host>        # agentic-tool install/update does this per host
```

All agents follow `rules/spec-kit.mdc`. Playbook: `skills/spec-kit/SKILL.md`. If Spec Kit is
unavailable, the pipeline runs in **SPEC-KIT DEGRADED** mode (hand-written SDLC docs, same gates).

## After adapt (`status: ready`)

```text
Run SDLC for: {your feature}
```

Coordinator playbook: `.agents/skills/sdlc-orchestrator/SKILL.md`

## Specialist agents

| Agent | Role |
|-------|------|
| coordinator | Hub — routing, gates, checkpoints, closeout |
| ba-analyst | Business grilling + requirements |
| architect | SDD, TDD, ADR |
| team-lead | Planning + TL code review |
| developer | Implementation (one task at a time) |
| tester | Adversarial testing + report |
| security-reviewer | Auth/token/storage review (conditional) |
| devops | Infra planning + IaC (conditional) |
| tech-writer | Docs sync (Phase 9) |
| stack-selector | Stack detection during adapt only |

**Project instructions:** [.agents/instructions/](.agents/instructions/) (when present) —
every agent reads `INDEX.md` + matching topic files. Rule: `rules/project-instructions.mdc`.
Custom / unknown agents from adapt: run `/agent-reconcile` to map catalog replacements or
extract rules into instructions.

Contract: [pipeline.manifest.json](pipeline.manifest.json)

## Updating (keep settings)

After any `agentic-tool` upgrade:

```bash
agentic-tool update
```

Backs up settings to `.agents/.backup/<ts>/`, refreshes the catalog, keeps user values in
`pipeline.manifest.json` and IDE/MCP settings, keeps user skills/agents/hooks/rules, re-links
hosts and verifies. Rule: `rules/catalog-refresh.mdc`.

## Codebase Dedupe Protocol

### Goal

Prevent duplicate implementations and "wrong file" edits by making the **code-search MCP** the
*only valid source* for claims about what already exists in this repo during this session.

### Tools you MUST use for codebase discovery

1. **Serena MCP** (primary): `get_symbols_overview`, `find_symbol`, `find_referencing_symbols`,
   `search_for_pattern`, `find_file`, `list_dir`
2. **Alternates**: codesearch, or mcp-codebase-search (`search_codebases`, `get_chunk_content`,
   `list_files`, `update_codebase_scan` after edits)

### Hard rule: No creation without a Dedupe Ticket

Before adding a new file, module, class, function, or helper, produce a **Dedupe Ticket**:

- **Intent signature** — one sentence describing what you are about to add
- **Queries** — 2–4 searches you will run
- **Top matches** — up to 5 file paths returned by the tool
- **Decision** — `reuse | extend | new`
- **Rationale** — why

### Graceful degradation

If the MCP server is unavailable: state **DEGRADED MODE** and stop before making changes.
Full priority chain (semantic MCP → default tools): `rules/code-search.mdc` +
`skills/code-search/SKILL.md`.

## Beads issue tracker (`br` only)

Local planning and issue tracking use **beads_rust**. The CLI is **`br`** — **never `bd`**
(legacy CLI). Triage via `bv --robot-triage`. Full rule: `rules/beads.mdc`.

**Client install:** `agentic-tool install` copies `beads.mdc` into `.agents/rules/`;
`update` refreshes all core rules + every enabled agent (each agent persona includes the
same hard lock) and links them into every host (`.cursor/`, `.claude/`, …).
Every specialist in this repo MUST follow **`br` only**.
<!-- agentic-pipeline:end -->
