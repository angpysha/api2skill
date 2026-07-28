# Data Model: Interactive HTML pages

**Feature**: `013-interactive-html-pages` | **Date**: 2026-07-28

This feature is documentation/UX content — no runtime CLR entities. Conceptual model:

## InteractiveGuide

| Field | Description |
|-------|-------------|
| Path | `docs/interactive/multi-spec-guide.html` |
| Scenarios | Enum-like UI choices: `generate-multi`, `merge`, `add-spec`, `update` |
| CommandTemplates | Per-scenario shell strings (placeholders like `./public.json`) |
| Tips | Base URL, secrets placeholders, Plan mode + creator skill |
| HostHints | Short in-page note pointing to wiki “Open in hosts” |

**Validation**: No real secrets; no `fetch()`; works with network disabled.

## HostOpenGuide (wiki)

| Host | Surface | Primary open path | Fallback |
|------|---------|-------------------|----------|
| Cursor | Canvas | Open HTML in browser; optional canvas paste / thin adapter | Paste HTML into canvas |
| GitHub Copilot | Canvas | Link/path + paste HTML | Paste |
| Claude | Artifact | Paste or attach HTML as Artifact | Paste |
| Codex | Sites | Open/serve HTML as site or paste | Paste |

## CreatorSkillReference

| Field | Value |
|-------|-------|
| Plan mode | Recommended before multi-spec interview |
| Guide path | `docs/interactive/multi-spec-guide.html` |

## Relationships

```text
wiki/Multi-Spec-Skills.md ──links──► docs/interactive/multi-spec-guide.html
api2skill-creator SKILL.md ──refs──► guide path + Plan mode
wiki host section ───────────────► same HTML content (no fork)
```
