# Contract: Examples filesystem layout

## Paths

```text
<skill>/
  examples/
    <operationId>/
      <name>/
        request.json     # optional
        response.json    # optional
  reference/
    <tag>.md             # links into ../examples/...
```

Rules:

- `<name>` and `<operationId>` path segments MUST be filesystem-safe (no `..`, no separators).
- Empty `examples/` is valid (default).
- At least one of `request.json` / `response.json` SHOULD exist per named example folder; empty folders MAY be pruned by `example remove` / `sync --prune-empty` (optional; v1: `remove` deletes the name directory).

## Content

- Prefer `application/json` text, UTF-8.
- MUST NOT document real secrets; SKILL.md warns humans/agents.
