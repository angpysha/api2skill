# Contract — Interactive multi-spec guide (HTML)

**Path**: `docs/interactive/multi-spec-guide.html`  
**Format**: Single HTML5 document; inline `<style>` and `<script>`; UTF-8.

## Required UI sections

1. **Title** — Multi-spec / same base URL skill composition  
2. **Scenario picker** — at least:
   - Multiple OpenAPI docs → new skill (`generate --spec`)
   - Existing separate skills → `merge --into`
   - Grow existing skill → `add-spec`
   - Refresh / replace part → `update` / `--spec-id`
3. **Command panel** — monospace block + **Copy** control for the selected scenario  
4. **Tips** — `--base-url` when servers disagree; `cp secrets.example.json secrets.json`; no real secrets; prefer Plan mode + `api2skill-creator`  
5. **Hosts** — short pointer to wiki page for Cursor / Copilot / Claude / Codex open steps  

## Command content rules

- Flags and verbs MUST match `wiki/Multi-Spec-Skills.md` / `specs/012-multi-spec-skill/contracts/cli.md`.
- Placeholders only (`./public.json`, `https://api.example.com`, `./skills/acme`).
- MUST NOT invent undocumented flags.

## Forbidden

- External script/CSS CDN required for core UX  
- `fetch()` to APIs  
- Real credentials  
- Build/bundler step  

## Optional

- Thin Cursor canvas example under `docs/interactive/` that embeds or summarizes the same scenarios (not source of truth for commands).
