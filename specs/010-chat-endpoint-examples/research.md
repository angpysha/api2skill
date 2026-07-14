# Research: Chat-authored endpoint examples

**Feature**: `010-chat-endpoint-examples`  
**Date**: 2026-07-14  
**Discovery**: **DEGRADED** — no codebase-search MCP; Grep/Read used. `SkillWriter` already preserves `secrets.json`, `auth.json`, `.auth-cache.json` across `--force` staging; `examples/` preservation should follow the same copy-into-staging pattern (entire directory tree).

## Decisions

### Decision: File layout

**Choice**: `examples/<operationId>/<name>/request.json` and optional `response.json`.

**Rationale**: Matches grill; request/response independent; multi-name without flat-file collisions.

**Alternatives**: Single root `request.json` (rejected after multi-name requirement); monolithic `examples.json` (rejected).

### Decision: Link injection

**Choice**: After writing schema-based `reference/<tag>.md`, scan `examples/` for ops in that tag and append an **Examples** section with relative links. Also run on CLI `example add/remove/sync`.

**Rationale**: Tag MD is regenerated often; links must be re-derived from filesystem (FR-003), not manually merged fragile middle cuts.

### Decision: CLI shape

**Choice**:

```text
api2skill example add    --skill <dir> --op <operationId> --name <slug> [--request <file|->] [--response <file|->] [--force]
api2skill example list   --skill <dir> [--op <operationId>]
api2skill example remove --skill <dir> --op <operationId> --name <slug>
api2skill example sync   --skill <dir>   # re-link all examples into reference/*.md
```

**Rationale**: Explicit verbs; `--force` for overwrite (FR-008); `sync` recovers links after manual file drops.

### Decision: Fail → ask → propose → approve

**Choice**: Documented in **SKILL.md** only for v1 (no automated watcher that mutates files). CLI never auto-rewrites examples from failed HTTP calls.

**Rationale**: Spec forbids unilateral apply; agents already operate from SKILL.md.

### Decision: Orphans

**Choice**: Preserve orphan example dirs (op removed from spec); `example sync` / generate emits a one-line warning listing orphans; still linked under a trailing “Orphan examples” note in `reference/default.md` or skill root `examples/README` pointer — prefer warning on stderr during generate + keep files; optional `reference/_orphans.md` in plan if needed. **v1**: warn on generate/sync; do not delete.

### Decision: Version

**Choice**: Next user-facing ship after 009 → bump per rule at PR time (**0.6.1** if 009 already published as 0.6.0, or fold into 0.6.0 if 009 unreleased). Coordinate with open 009 PR.

## Decision log

| Decision | Choice | Date |
|----------|--------|------|
| Storage | Separate files + tag MD links | 2026-07-14 |
| Layout | `examples/<op>/<name>/request\|response.json` | 2026-07-14 |
| Authorship | SKILL.md + CLI | 2026-07-14 |
| Preserve | Copy `examples/` like secrets/auth; re-link on write | 2026-07-14 |
| Failures | Docs protocol only; no auto-apply | 2026-07-14 |
| Orphans | Keep + warn | 2026-07-14 |
