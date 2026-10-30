---
name: serena-install
description: >-
  Install Serena (oraios/serena) — the PRIMARY code-search MCP for this pipeline (symbol-level
  navigation via language servers) — and wire it into every configured host (Cursor, Claude Code,
  Windsurf). Add-only MCP config. Use during install/update or when code-search is DEGRADED.
argument-hint: "[hosts=cursor,claude] [index=true|false]"
disable-model-invocation: false
---

# serena-install

[Serena](https://github.com/oraios/serena) gives agents IDE-like **symbolic** tools
(`find_symbol`, `find_referencing_symbols`, `get_symbols_overview`, `search_for_pattern`, …)
backed by language servers. It is **Priority 1** in [`code-search`](../code-search/SKILL.md).
Alternates stay supported: [`codesearch-install`](../codesearch-install/SKILL.md) (semantic,
offline) and mcp-codebase-search.

`agentic-tool install` / `update` run this automatically when
`pipeline.manifest.json` → `code_search.primary` is `serena` (the default).

## Parameters (strict)

| Param | Values | Default |
|-------|--------|---------|
| `hosts` | comma list of `cursor`, `claude`, `windsurf` | manifest `hosts` |
| `index` | `true` \| `false` | `false` |

Any other value → stop and ask; do not guess.

## Run

```powershell
$S = '.agents/skills/serena-install/scripts/install-serena.ps1'
pwsh $S -DryRun -ConfigureMcp -Hosts cursor,claude   # preview
pwsh $S -ConfigureMcp -Hosts cursor,claude           # install + wire MCP
pwsh $S -ConfigureMcp -Hosts claude -Index           # also pre-index the project
```

Requires [uv](https://docs.astral.sh/uv/) (`uv tool install -p 3.13 serena-agent`).

## MCP entries (added only when no `serena` entry exists)

| Host | File | Args |
|------|------|------|
| Cursor | `.cursor/mcp.json` | `start-mcp-server --context ide --project ${workspaceFolder}` |
| Claude Code | `.mcp.json` | `start-mcp-server --context claude-code --project-from-cwd` |
| Windsurf | `.windsurf/mcp.json` | `start-mcp-server --context ide --project-from-cwd` |

An existing `serena` entry is **user-owned** and never changed (custom args/env survive updates).

## Outputs

- `.agents/serena-install-report.json` — installed, version, per-host `added|kept`, indexed
- Exit codes: `0` ok, `1` uv missing, `2` install/config failed
