---
name: agents-adapt
description: >-
  Legacy entry point. Use the native /pipeline-adapt skill (strict parameters) instead.
disable-model-invocation: true
---

# .agents adapt → /pipeline-adapt

Adapt moved to the native skill **`/pipeline-adapt`** (`.agents/skills/pipeline-adapt/SKILL.md`),
which takes strict `key=value` parameters and delegates every mechanical step to `agentic-tool`
(`recommend` → `apply` → `update`).

- First adapt: `/pipeline-adapt mode=init`
- Stack / PROJECT.md changed: `/pipeline-adapt mode=update`
- Tool upgraded only: `agentic-tool update` (terminal; no adapt needed)

If someone says "Run .agents adapt", run `/pipeline-adapt` with `mode` inferred from the
manifest `status` and follow that skill exactly.
