---
name: api2skill-creator
description: >-
  Interview the user about an OpenAPI/Swagger API (including multi-spec, base URL, and
  authorization), assemble exact api2skill CLI commands, guide secrets.json setup, and migrate
  existing generated skills after api2skill upgrades (update / regenerate / merge). Prefer Plan
  mode for multi-spec interviews and migrations.
---

# api2skill-creator

Help the user turn a REST API (OpenAPI/Swagger) into an agent skill using the **api2skill**
CLI. Ask clarifying questions, emit **exact** shell commands, then walk through **base URL**,
**authorization**, and **secrets** file setup. Do not invent flags, URLs, or credential values.

Project docs: `README.md`, `wiki/` (`wiki/Authentication.md`, `wiki/Generate-Command.md`,
`wiki/Update-Command.md`, `wiki/Multi-Spec-Skills.md`, `wiki/Interactive-Guides.md`,
`wiki/Install-Creator.md`). Interactive multi-spec walkthrough:
`docs/interactive/multi-spec-guide.html`.

## Prefer Plan mode (multi-spec, first setup, and migrations)

When the user has **multiple** OpenAPI/Swagger docs, existing separate skills to merge, an
unclear auth/base-URL setup, or **existing skills to migrate** after an api2skill upgrade:

1. Prefer **Cursor Plan mode** (or any plan-first agent workflow) **before** emitting
   `generate` / `merge` / `add-spec` / `update` commands.
2. In the plan, list the interview topics below (auth, base URL, secrets placement, which
   workflow — including migrate vs fresh generate) and get answers — then switch to executing
   the agreed commands.
3. Optionally open or present `docs/interactive/multi-spec-guide.html` (browser, Canvas,
   Artifact, or Codex site — see `wiki/Interactive-Guides.md`) so the user can click a
   scenario and copy the same commands you will emit.
4. Do **not** invent credentials or run destructive `--force` / overwrite merges without
   explicit user approval.

Single-spec, already-answered setups can go straight to a concrete command.

## Interview order

Work through these topics (skip any the user already answered). Ask **one topic at a time**
when the user has not provided a batch answer.

### 1. OpenAPI / Swagger source(s)

- **Single doc**: local file, `https://…` URL, or stdin (`-`).
- **Multiple docs, same API host**: list each path/URL; optional `--spec-id` / `--spec-label`
  per part.
- **Workflow**:
  - New composite → `generate --spec … --spec …`
  - Existing separate skills → `merge --into <target> <skill-dir> …`
  - Add one doc to existing skill → `add-spec <skill-path> <spec> --spec-id …` (not `merge`)
  - Refresh → `update <skill-path>` or `update <path> <new-spec> --spec-id …` when N>1 parts

### 2. Base URL (required — always ask)

Ask explicitly:

1. What **base URL** should API calls use? (e.g. `https://api.example.com/v1`)
2. Do the spec(s) define the same `servers` URL, or none?
3. Dev/staging with self-signed TLS? → may need `--insecure` (dev only).

**Emit `--base-url <url>` when**:

- Spec has no `servers` entry.
- Multiple parts disagree on URL but user wants one skill.
- User's canonical URL differs from the spec.

The value is recorded in `.api2skill.json` (safe to commit). If the spec had no servers, the
user may also set top-level `baseUrl` in `secrets.json` at call time.

### 3. Authorization (required — always ask)

For **each spec part** in a composite, ask:

1. Public or authenticated operations?
2. Auth type: API key, Bearer, Basic, OAuth2, Entra, custom headers, script, or mixed?
3. One credential set or several (e.g. public key + admin OAuth)?

**Generate flags**:

- Simple single global profile: `--auth bearer|basic|custom`
- OAuth2 / Entra / multi-profile / per-tag: `--auth-config <path-to-auth.json>`
- `--auth` and `--auth-config` are **mutually exclusive**.
- Scaffold path: generate without auth flags → edit inactive `auth.json` →
  `generate … --auth-config ./auth.json --force`
- OAuth authorization-code: `--login` after generate (needs `callbackUrl` in `auth.json`)

Multi-part skills: scheme IDs may be prefixed on collision (`{specId}_{schemeId}`). Prefer
**naming each `auth.json` profile after its OpenAPI security scheme id** so unscoped Bearer /
OAuth / script profiles do not collide on the `Authorization` header (api2skill ≥ 0.7.1).
Use `attach.scope: tags` when one profile must apply only to some tags. After merge, check
`auth.json` → `_guidance.schemes` for every **final** (possibly prefixed) scheme id.
See `wiki/Authentication.md`. **Never invent** client IDs, secrets, or tokens.

### 4. Output and tooling

- **`--out` / `-o`** — skill directory (default: slug of `info.title`).
- **`--name`** — skill name override (optional).
- **`--script`** — `cs` (default), `fsx`, or `csx`.
- **`--force` / `-f`** — overwrite existing output.
- Optional: `--include` / `--exclude`, `--format json|yaml`, filters.

### 5. Update vs generate vs migrate

If `.api2skill.json` exists, prefer `update` unless merging or adding specs.

If the user has **skills generated by an older api2skill** (especially pre-0.7.1) or reports
any of: tag `/` crashes, empty `AuthProfileNames`, `file://` baseUrl, useless `login`, or
merge re-fetching remotes — follow **[Migrate existing skills](#migrate-existing-skills-071)**
before inventing new flags.

## Commands to emit

Print complete commands first. Examples (adapt to answers):

```bash
# Single spec
api2skill generate ./openapi.json --out ./skills/my-api --name my-api --script cs

# Multi-spec + shared base URL
api2skill generate \
  --spec ./public.json --spec-id public \
  --spec ./admin.json --spec-id admin \
  --base-url https://api.example.com \
  --out ./skills/acme-api

# Remote spec (dev cert)
api2skill generate https://svc.local/swagger.json --insecure --base-url https://svc.local --out ./skills/my-api

# Bearer shorthand
api2skill generate ./openapi.json --auth bearer --out ./skills/my-api

# Explicit auth + OAuth login
api2skill generate ./openapi.json --auth-config ./auth.json --login --out ./skills/my-api --force

# Merge existing skills
api2skill merge --into ./skills/acme ./skills/acme-public ./skills/acme-admin

# Append to existing skill
api2skill add-spec ./skills/acme ./billing.json --spec-id billing

# Refresh (same tool version / options already in .api2skill.json)
api2skill update ./skills/my-api
api2skill update ./skills/acme ./new-admin.json --spec-id admin
```

## Migrate existing skills (0.7.1+)

Use this when the user already has generated skill directories and needs them to pick up fixes
from **api2skill ≥ 0.7.1** (tag path sanitization, spec cache for merge, HTTP(S) baseUrl,
`AuthProfileNames` wiring, scheme-scoped auth profiles, composite `_guidance.schemes`).

**Old skills do not auto-heal** — regenerate or update with the new CLI. `--force` / `update`
preserves `secrets.json`, `.auth-cache.json`, and (unless a new `--auth-config` is supplied)
existing `auth.json` contents, then refreshes `_guidance` for final scheme ids.

### When to migrate (symptoms → action)

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Crash writing `reference/…` with `/` in a tag | Unsanitized tag file path | Regenerate / `update` on 0.7.1+ |
| `merge` re-fetches live URLs / fails offline | No `.api2skill/specs/` cache | `update` each part skill, then `merge` |
| Composite `.api2skill.json` has `"baseUrl": "file://"` | Server-less / file part poisoned base | Regenerate/merge with `--base-url https://…` |
| `login` works but calls still 401 / empty `AuthProfileNames` | Profiles not baked into dispatcher | Regenerate with `--auth-config` (see below) |
| `generate --auth-config` rejects two `Authorization` profiles | Unscoped globals colliding | Rename profiles to match scheme ids, regenerate |

### Interview before migrating

Ask (skip answered):

1. Single skill or multi-part / merged composite?
2. Spec sources still reachable (URLs/files)?
3. Path to their `auth.json` (if any) and whether profile **names** match OpenAPI scheme ids?
4. Canonical `--base-url` (required if any part lacks HTTP(S) `servers`)?
5. Confirm overwrite: `update` or `generate … --force` / `merge … --force`?

### Commands to emit (pick one workflow)

**A — Single-spec skill (preferred: bake auth)**

```bash
dotnet tool update -g api2skill   # or install the 0.7.1+ package the user uses
api2skill creator check-update    # then install-creator if they approve

cd ./skills/my-api
api2skill generate <spec-url-or-file> \
  --auth-config ./auth.json \
  --base-url https://svc.example.com \
  --out . --name my-api --script cs --force
# secrets.json and .auth-cache.json are preserved
```

If they have no `auth.json` yet: `update` first (gets cache + sanitized references), then
scaffold/edit `auth.json`, then the `generate … --auth-config … --force` pass above.

```bash
api2skill update ./skills/my-api
```

**B — Multi-spec composite (one-shot regenerate)**

```bash
api2skill generate \
  --spec https://svc.local/swagger/Api1/swagger.json --spec-id api1 \
  --spec https://svc.local/swagger/Api2/swagger.json --spec-id api2 \
  --on-conflict prefix \
  --base-url https://svc.local \
  --auth-config ./auth.json \
  --out ./skills/composite-api --name composite-api --script cs --force
```

**C — Separate part skills → merge (offline-friendly after 0.7.1+ refresh)**

```bash
api2skill update ./skills/api1
api2skill update ./skills/api2
# … each part; confirms .api2skill/specs/<id>.json exists
api2skill merge --into ./skills/composite-api --on-conflict prefix --force \
  ./skills/api1 ./skills/api2
# merge prefers cached specs under each source skill; still pass --base-url via regenerating
# with generate if the composite baseUrl was file:// — or fix .api2skill.json after merge
```

If the composite still shows `file://` after merge, re-run workflow **B** with `--base-url`, or
edit `.api2skill.json` `baseUrl` to the HTTPS root and regenerate once so the dispatcher embeds
it.

### Auth.json migration tips (emit to the user)

1. Name profiles after scheme ids (`oauth2`, `api_key`, or prefixed `api2_oauth2` from
   `_guidance.schemes` after merge).
2. Two Bearer/OAuth/script profiles that both set `Authorization` are OK when each name matches
   a different scheme id — do **not** leave two true-global profiles with unrelated names.
3. After migrate, open `scripts/call.cs` (or `.csx`/`.fsx`) and confirm target operations have
   **non-empty** `AuthProfileNames` (not `[]`). Then:
   ```bash
   api2skill login --skill . --profile <name>
   ```
   `<name>` must appear in some operation’s `AuthProfileNames`.

### Post-migrate checklist (always walk through)

1. `reference/` writes succeed; tags with `/` become sanitized files (e.g. `Group-Item.md`).
2. `.api2skill.json` → `baseUrl` is `https://…` (never `file://`).
3. `.api2skill/specs/<spec-id>.json` (or `.yaml`) exists after generate/update.
4. Dispatcher `AuthProfileNames` non-empty for authenticated ops when `--auth-config` was used
   (or after scaffold bake on 0.7.1+).
5. Composite `auth.json` → `_guidance.schemes` lists every final scheme id (including prefixes).
6. `secrets.json` / `.auth-cache.json` still present; re-login if tokens expired.
7. Remind: never commit `secrets.json` or `.auth-cache.json`.

## After generate — secrets placement (required)

**Always** walk through this after the user runs generate/merge/add-spec:

1. `cd <skill-dir>`
2. The generator wrote **`secrets.example.json`** (committed template, placeholders only).
3. Create the real file (gitignored), either:
   - `cp secrets.example.json secrets.json` then fill real values, **or**
   - Create `secrets.json` with the **same keys and structure** as `secrets.example.json`
     (empty strings as placeholders) so the user only replaces values locally.
4. Explain the shape:
   - Nested objects keyed by **security scheme ID** (from OpenAPI), e.g. `api_key.apiKey`.
   - Top-level keys may come from `auth.json` `{secret:NAME}` references.
5. **OAuth**: put `clientId` / `clientSecret` in `secrets.json`; run
   `api2skill login --skill . --profile <name>` for access tokens (stored in
   `.auth-cache.json`, not `secrets.json`).
6. **Never commit** `secrets.json` or `.auth-cache.json`.

Example placeholder `secrets.json` (agent may draft — user fills real values):

```json
{
  "$comment": "Replace empty strings with real credentials. Never commit this file.",
  "api_key": { "apiKey": "" },
  "admin_oauth": {
    "clientId": "",
    "clientSecret": "",
    "tokenUrl": "https://api.example.com/oauth/token",
    "scopes": ["api.read"]
  }
}
```

If the user needs `auth.json`, point to `wiki/Authentication.md` and use `{secret:KEY}` placeholders
that match keys you help them add to `secrets.json`.

After merge, warn that duplicate keys in source `secrets.json` files keep the **first source's**
value; check warnings in CLI output.

## Auth JSON hints (for agents drafting auth.json)

Profiles need `type`, endpoints, scopes, `{secret:…}` placeholders, and `callbackUrl` for
authorization-code flows. Do not invent secret values.

## Self-update (session start — always ask the user)

Before interviewing about the user's API:

1. From the **repository root**, run:
   ```bash
   api2skill creator check-update
   ```
2. If any line shows **`update available`**, tell the user which skill roots are outdated
   (bundled version vs installed). **Ask explicitly** whether to update — **never** run
   `install-creator` without their approval.
3. If they agree, run (adapt the skills root from the check output):
   ```bash
   api2skill install-creator --target .cursor/skills
   ```
   On a TTY, `install-creator` prompts again: *"Update available … Install update? [y/N]"*
   — wait for the user to confirm there too unless they passed `--force` themselves.
4. If they decline, continue with the current installed skill.
5. **Never auto-update silently** — not in chat, not via `--force` unless the user explicitly
   requested that flag.
6. After the creator skill itself is updated to **≥ 0.7.1**, ask whether they also want to
   **migrate existing generated API skills** (see [Migrate existing skills](#migrate-existing-skills-071)).

Installed version is recorded in `<skills-root>/api2skill-creator/.creator-version.json`
(written on install; value matches the api2skill package `<Version>`). Legacy installs without
that file are treated as oldest and offered an update.

## After install / manual refresh

```bash
api2skill install-creator --target <skills-root> [--force]
```

Roots: `.cursor/skills/`, `.claude/skills/`, `.github/skills/`, `.agents/skills/` —
see `wiki/Install-Creator.md`.
