---
name: pipeline-adapt
description: >-
  Adapt the AI dev pipeline to THIS repository with strict, validated parameters: project brief,
  code-search exploration, pack/agent recommendation written by `agentic-tool recommend`, apply,
  Spec Kit, sync to every IDE host, verify. Invoke as /pipeline-adapt after `agentic-tool install`.
argument-hint: "mode=init|update [hosts=cursor,claude] [packs=a,b] [features=pack:f1+f2] [code_search=serena|codesearch|mcp-codebase-search|none] [lane=full] [yes=true]"
disable-model-invocation: true
---

# /pipeline-adapt

Native skill (Cursor + Claude Code read the same `SKILL.md` via the `.agents/skills` link).
Everything mechanical is done by **`agentic-tool`**; this skill only gathers parameter values,
asks the human where the table says so, and runs the commands below **verbatim**.

## Parameters (strict)

Parse `$ARGUMENTS` as `key=value` pairs. **Unknown keys or values outside the allowed set →
stop, print this table, ask.** Never invent values.

| Key | Allowed values | Default | Source when omitted |
|-----|----------------|---------|---------------------|
| `mode` | `init` \| `update` | `init` if manifest `status` is `needs_adapt`, else `update` | manifest |
| `hosts` | comma list of `cursor`, `claude`, `windsurf`, `codex` | manifest `hosts` | manifest |
| `packs` | names from `agentic-tool recommend --list` | — | Step 2 detection → human confirm |
| `features` | `pack:f1+f2` (repeatable, comma-separated) | none | Step 2 detection |
| `code_search` | `serena` \| `codesearch` \| `mcp-codebase-search` \| `none` | manifest `code_search.primary` (`serena`) | manifest |
| `lane` | `spike` \| `micro` \| `short` \| `fast` \| `full` | manifest `project.default_lane` | manifest |
| `yes` | `true` \| `false` | `false` | — skip human confirm only when `confidence=high` |

`mode=update` = re-detect stack after PROJECT.md / stack changes. A tool upgrade alone needs
**no** adapt — run `agentic-tool update` in a terminal instead.

## Step 0 — Project brief

```powershell
pwsh .agents/skills/project-intake/scripts/ensure-project-md.ps1
```

| Exit | Action |
|------|--------|
| `0` / `3` | continue (`3` = greenfield: PROJECT.md is the primary stack signal) |
| `1` / `2` | **stop** → run the `project-intake` skill, then re-run this step |

## Step 1 — Explore (code-search priority)

Follow `.agents/skills/code-search/SKILL.md`: **Serena** symbol tools → alternate semantic MCP
(codesearch / mcp-codebase-search) → Grep/Glob/Read. Record which source you used.

## Step 2 — Detect (skip when `packs=` was given)

```bash
agentic-tool recommend --list        # the ONLY valid pack / feature / agent names
```

Score packs with the `stack-selector` agent rules (registry signals vs. MCP results, filesystem,
PROJECT.md). Pick one runtime pack + add-on packs with score ≥ 2. Set `confidence`.

## Step 3 — Human confirm

Show: packs, features, hosts, build/test/format commands, confidence, signals.
Skip only when `yes=true` **and** confidence is `high`. Low confidence → always ask.

## Step 4 — Write the recommendation (strict CLI; never hand-write the JSON)

```bash
agentic-tool recommend --packs <packs> [--features <pack:f1+f2>] --hosts <hosts> \
  --code-search <code_search> [--lane <lane>] --confidence <high|medium|low> \
  [--build "<cmd>"] [--test "<cmd>"] [--format "<cmd>"] --signal "<signal>" ...
```

Non-zero exit → show the `ERROR` lines, fix the values with the human, retry. Omit
`--build/--test/--format` to take the pack defaults.

## Step 5 — Apply + Spec Kit + sync + verify

```bash
agentic-tool update --host <hosts> --code-search <code_search>
```

One command: settings backup → apply (manifest merge, user overlays preserved) → Spec Kit
integration per host → code-search MCP (add-only) → restore user settings → relative links →
verify.

`mode=init` only: if Spec Kit was just initialized, run `/speckit.constitution` seeded from
`PROJECT.md`. If `specify` is unavailable, set `spec_kit.enabled: false` → SPEC-KIT DEGRADED.

If `agentic-tool update` prints `WARN recommendation agents: unknown agent …`, run
`/agent-reconcile` (skill `.agents/skills/agent-reconcile/SKILL.md`) to propose catalog
replacements and/or extract rules into `.agents/instructions/`.

## Output checklist

- [ ] PROJECT.md passes Step 0
- [ ] `.agents/pipeline.recommendation.json` written by `agentic-tool recommend` (exit 0)
- [ ] `agentic-tool update` exit 0 (`verify` PASS)
- [ ] manifest `status: ready`, `hosts` = requested hosts, `catalog_version` = `agentic-tool version`
- [ ] `.agents/install-report.md` has a Detection table (packs, scores, signals, source)

## Constraints

- No values outside the parameter table; no hand-edited recommendation JSON.
- Never overwrite user settings by hand — `agentic-tool` preserves them (backup in `.agents/.backup/`).
- No secrets in manifest, recommendation, or reports.
