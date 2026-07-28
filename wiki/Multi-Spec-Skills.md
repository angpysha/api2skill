# Multi-Spec Skills

When several OpenAPI/Swagger documents share the **same API base URL** (for example
public, admin, and billing surfaces), api2skill can compose them into **one** skill:
single `SKILL.md`, one dispatcher, one `secrets.json` scaffold.

Feature contract: [specs/012-multi-spec-skill/spec.md](../specs/012-multi-spec-skill/spec.md).

## Choose a workflow

| Situation | Command |
|-----------|---------|
| New skill from multiple docs | [`generate --spec …`](Generate-Command.md) (repeatable) |
| You already have separate skills for the same API | [`merge --into`](Merge-Command.md) |
| Grow an existing skill with one more OpenAPI file | [`add-spec`](Add-Spec-Command.md) (not `merge`) |
| Refresh all parts, or replace one part's source | [`update`](Update-Command.md) / `update … --spec-id` |

## Same base URL

All parts must resolve to the **same** base URL after normalization (scheme, host, port,
path prefix). If the documents disagree — or have no `servers` — pass a canonical override:

```bash
api2skill generate \
  --spec ./public.json --spec-id public \
  --spec ./admin.json --spec-id admin \
  --base-url https://api.example.com \
  --out ./skills/acme
```

Without `--base-url`, conflicting servers fail with a clear per-`specId` list.

## Generate (compose docs → one skill)

```bash
api2skill generate \
  --spec ./public.json --spec-id public --spec-label "Public API" \
  --spec ./admin.json --spec-id admin --spec-label "Admin API" \
  --base-url https://api.example.com \
  --out ./skills/acme
```

- `--spec-id` / `--spec-label` apply to the **most recent** `--spec`.
- Duplicate `operationId` across parts → fail (default) or `--on-conflict prefix`.
- Colliding security scheme IDs are namespaced as `{specId}_{schemeId}`.

## Merge (combine existing skills)

Sources are **not** deleted. Credential keys are unioned; on conflict the **first** source
wins (CLI warns).

```bash
api2skill merge --into ./skills/acme ./skills/acme-public ./skills/acme-admin
```

Use `--force` only to overwrite an existing api2skill skill at `--into`. To append one
OpenAPI file to a skill you already have, use [`add-spec`](Add-Spec-Command.md).

## Add-spec (append one document)

```bash
api2skill add-spec ./skills/acme ./billing.json --spec-id billing
```

Regenerates in place (`--force` semantics). Preserves `secrets.json`, `auth.json`, and
`.auth-cache.json`.

## Update (multi-part)

```bash
# Refresh every recorded part from its stored source
api2skill update ./skills/acme

# Point one part at a new file/URL, then refresh all
api2skill update ./skills/acme ./new-admin.json --spec-id admin
```

When the skill has more than one `specSources` entry, a positional new source **requires**
`--spec-id`.

## Auth when docs differ

Different documents often expose different `securitySchemes`. Prefer an explicit
[`auth.json`](Authentication.md) with **global or tag-level** profiles rather than one
`--auth` shorthand for the whole composite. Scheme IDs may be prefixed on collision; keep
`{secret:…}` keys aligned with `secrets.json`.

Auto-scaffold (first generate without `--auth` / `--auth-config`) still writes an inactive
template you can edit and re-activate with `--auth-config`.

## Creator skill + Plan mode

Install the helper skill, then prefer **Cursor Plan mode** (or any plan-first agent workflow)
before running generate/merge/add-spec — so the agent plans questions (auth, base URL,
secrets placement, merge vs generate) before emitting commands.

**Interactive walkthrough:** open
[`docs/interactive/multi-spec-guide.html`](../docs/interactive/multi-spec-guide.html)
in a browser (pick a scenario → copy command). Host Canvas / Artifact / Sites steps:
[Interactive Guides](Interactive-Guides.md).

```bash
api2skill install-creator                          # TTY: pick host roots
api2skill creator check-update                     # exit 8 if outdated
api2skill install-creator --target .cursor/skills  # after you approve an update
```

Details: [Install Creator](Install-Creator.md). Bundled creator version matches the
api2skill package `<Version>` (recorded in `.creator-version.json` on install).
