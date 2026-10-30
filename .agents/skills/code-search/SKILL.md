---
name: code-search
description: >-
  Codebase discovery priority for all agents: (1) Serena MCP symbolic tools (primary),
  (2) alternate semantic MCP — codesearch or mcp-codebase-search, (3) default tools
  (Grep/Glob/Read). Use before claiming code exists, during /pipeline-adapt stack detection,
  and when grilling/domain-modeling cross-checks the repo.
disable-model-invocation: false
---

# Code Search — discovery priority

**Always follow this order** when exploring a repo, answering "does X exist?", stack detection,
or cross-checking user claims against code. The configured backend is
`pipeline.manifest.json` → `code_search.primary` (default `serena`); `code_search.alternates`
lists the other MCP sources this repo may have.

| Priority | Source | Use it for |
|----------|--------|-----------|
| **1** | **Serena MCP** (primary) | symbols, definitions, references, file/module overviews, pattern search |
| **2** | **Alternate semantic MCP** — `codesearch` or `mcp-codebase-search` | fuzzy "where is the code that does X?" when no symbol name is known |
| **3** | **Default tools** — Grep, Glob, Read | when no MCP is available |

State **DEGRADED MODE** in chat when neither priority 1 nor 2 is available, before making
structural claims about the codebase.

---

## Priority 1 — Serena (primary)

Symbol-level tools backed by language servers (install: `skills/serena-install`).

| Need | Serena tool |
|------|-------------|
| Outline of a file / module | `get_symbols_overview` |
| Find a class/function/method by name path | `find_symbol` (`include_body` only when needed) |
| Who uses it? (dedupe, impact) | `find_referencing_symbols` |
| Text / regex across the repo | `search_for_pattern` |
| Locate files | `find_file`, `list_dir` |

Workflow: overview → `find_symbol` → read only the bodies you need → `find_referencing_symbols`
before changing a signature. Cite **file paths + symbol names**.

## Priority 2 — alternate semantic MCP

Use when the concept is known but the symbol name is not, or Serena is unavailable.

### codesearch (offline, vector + BM25)

```bash
codesearch index .                  # see skills/codesearch-install
```

Tools: semantic search, symbol nav (`codesearch mcp`).

### mcp-codebase-search

```bash
npm install -g @teknologika/mcp-codebase-search
mcp-codebase-ingest --path . --name <project-name>
```

Tools: `list_codebases`, `search_codebases`, `get_codebase_stats`, `get_chunk_content`,
`get_file_content`, `get_adjacent_chunks`, `list_files`, `update_codebase_scan` (refresh after edits).

Switching the primary: `agentic-tool update --code-search codesearch` (or `mcp-codebase-search`).

## Priority 3 — default tools

Glob (names) → Grep (exact symbols/strings) → Read (narrowed files).

---

## Dedupe Ticket — hard rule

Before adding **any** new file, module, class, function, or helper, produce a **Dedupe Ticket**
(intent, 2–4 queries, top ≤5 matches with paths, decision `reuse | extend | new`, rationale).
With Serena: `find_symbol` for candidate names + `search_for_pattern` for the concept +
`find_referencing_symbols` on the best match.

## Adapt / stack-selector

During `/pipeline-adapt` (after `PROJECT.md` intake):

1. **Serena** — `list_dir` / `get_symbols_overview` on top-level modules for languages + layout
2. **Alternate MCP** — stats / semantic hits
3. **Filesystem globs** — `*.csproj`, `pubspec.yaml`, `Package.swift`, …
4. **`PROJECT.md` Stack** — primary on greenfield

Record the source used: `serena` | `codesearch` | `mcp-codebase-search` | `filesystem` | `project-brief`.

## Constraints

- Cite paths; do not dump large files into chat.
- All MCP backends run locally — no code leaves the machine.
