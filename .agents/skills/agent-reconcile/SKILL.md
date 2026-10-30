---
name: agent-reconcile
description: >-
  Analyze user-provided / custom agents (unknown catalog names), propose catalog
  replacements when they map cleanly, and extract unique rules into
  `.agents/instructions/` so every specialist can follow them. Invoke as
  /agent-reconcile after update WARNs or when migrating hand-rolled agents.
argument-hint: "[mode=scan|apply] [yes=true]"
disable-model-invocation: true
---

# /agent-reconcile

Maps **custom agents** (names outside the catalog core set) onto pipeline specialists,
and turns leftover project-specific rules into shared **instructions** that every agent
reads.

Catalog core agents (only these ship from the tool):

`coordinator`, `ba-analyst`, `architect`, `team-lead`, `developer`, `tester`,
`security-reviewer`, `devops`, `tech-writer`, `stack-selector`

Custom names (e.g. `flutter-dev`, `devops-engineer`) are **kept** by `agentic-tool update`
with a WARN — they are not deleted. This skill decides what to do next.

## Parameters

Parse `$ARGUMENTS` as `key=value` pairs.

| Key | Allowed | Default | Notes |
|-----|---------|---------|-------|
| `mode` | `scan` \| `apply` | `scan` | `scan` = propose only; `apply` = write after human confirm |
| `yes` | `true` \| `false` | `false` | skip confirm only when every proposal is `replace` with confidence `high` |

## Step 0 — Discover custom agents

Collect unique names from:

1. `.agents/pipeline.recommendation.json` → `agents[].name` not in the core list
2. `pipeline.manifest.json` → `agents` / `agents_extra` not in the core list
3. Files `.agents/agents/*.md` whose basename is not a core agent

For each name, if `.agents/agents/<name>.md` exists, read it (and any `## local overlay`).

Optional helper:

```powershell
pwsh .agents/skills/agent-reconcile/scripts/list-custom-agents.ps1
```

## Step 1 — Propose mapping

For each custom agent, pick **one** action:

| Action | When |
|--------|------|
| `replace` | Role clearly matches a core agent (use alias table + file content) |
| `keep` | Truly distinct persona the team wants as a separate agent file |
| `extract` | Keep or replace the name, but unique rules belong in `.agents/instructions/` |

### Alias table (start here; override from file content)

| Custom name patterns | Likely catalog agent |
|----------------------|----------------------|
| `*-dev`, `*-developer`, `flutter-dev`, `rust-dev`, `ios-dev`, `android-dev`, `frontend-dev`, `backend-dev` | `developer` |
| `devops-engineer`, `sre`, `infra`, `platform-engineer` | `devops` |
| `code-reviewer`, `reviewer`, `tl-review` | `team-lead` |
| `architecture-advisor`, `solution-architect`, `arch` | `architect` |
| `testing-specialist`, `qa`, `qa-engineer`, `test-engineer` | `tester` |
| `security-engineer`, `appsec`, `sec-review` | `security-reviewer` |
| `tech-writer-*`, `docs-writer`, `documentation` | `tech-writer` |
| `ba`, `business-analyst`, `product-analyst` | `ba-analyst` |
| `pm-coordinator`, `scrum-master` (routing/hub) | `coordinator` |

Stack-specific coding rules (BLoC, EF, SwiftLint, …) are **not** a reason to keep a
custom developer agent — prefer `replace` → `developer` + pack overlay + **instructions**.

## Step 2 — Propose instructions

When the custom agent file (or chat-provided brief) contains **project-specific rules**
that are not already in:

- the matching core agent
- an active pack overlay (`.agents/agents/<core>.md` pack sections)
- existing `.agents/instructions/`

…propose writing or updating:

```text
.agents/instructions/INDEX.md          # catalog of instruction files
.agents/instructions/<topic>.md        # one topic per concern
```

Use templates under this skill’s `templates/`. Good topic splits: stack conventions,
review checklist, security baselines, release/ops, domain language.

Every instruction file should say **who** should read it (all agents / developer+tester / …).

## Step 3 — Human confirm

Show a table:

| Custom agent | Action | Catalog target | Instructions to write/update | Confidence |
|--------------|--------|----------------|------------------------------|------------|

Stop for approval unless `yes=true` and every row is `replace` + `high`.

## Step 4 — Apply (`mode=apply` only)

1. **Instructions** — create/update `.agents/instructions/` files; refresh `INDEX.md`.
2. **Recommendation** — rebuild agents list with catalog names (drop replaced customs):

   ```bash
   agentic-tool recommend --packs <from-current> --agents <merged-core-list> \
     --hosts <hosts> --code-search <…> …   # keep other flags from current recommendation
   ```

   Or edit only via `recommend` (never hand-write invalid packs/features). Custom names
   that stay as `keep` may remain in `--agents` (update warns, does not fail).
3. **Optional merge** — for `replace`, append lasting rules into
   `.agents/agents/<target>.md` under `## local overlay` (survives catalog refresh), then
   move the old custom file to `.agents/archive/` if the human agrees.
4. **Refresh** — `agentic-tool update` so manifest `agents` matches.

## Output checklist

- [ ] Proposal table reviewed
- [ ] `.agents/instructions/INDEX.md` + topic files written when extract/replace needed
- [ ] Recommendation / manifest agents list updated for accepted replacements
- [ ] Custom agent files archived or kept per decision
- [ ] Specialists can discover instructions via `rules/project-instructions.mdc`

## Constraints

- Prefer catalog agents + instructions over long-lived duplicate personas.
- Do not invent packs/features; only remap agent names and write instructions.
- No secrets in instruction files.
- Pack overlays stay the place for **stack** defaults; instructions are for **this repo**.
