---
name: api2skill-creator
description: >-
  Interview the user about an OpenAPI/Swagger API and assemble the exact api2skill
  generate or update CLI command (auth, out path, script kind, login, force, insecure).
---

# api2skill-creator

Help the user turn a REST API (OpenAPI/Swagger) into an agent skill using the **api2skill**
CLI. Ask clarifying questions, then output **exact** shell commands they can copy-paste.
Do not invent flags that are not listed here.

Project docs: repository `README.md` and `wiki/` (especially `wiki/Authentication.md`,
`wiki/Generate-Command.md`, `wiki/Update-Command.md`, `wiki/Install-Creator.md`).

## What to ask

Work through these topics (skip any the user already answered):

1. **OpenAPI / Swagger path or URL** — local file, `https://…` URL, or stdin (`-`).
2. **`--out` / `-o`** — output skill directory (default: slug of `info.title`).
3. **`--name`** — skill name override (optional).
4. **`--script`** — emitter: `cs` (default), `fsx`, or `csx`.
5. **Auth**
   - Simple one-profile: `--auth bearer|basic|custom`
   - Full / OAuth2 / Entra / script / multi-profile: `--auth-config <path-to-auth.json>`
   - `--auth` and `--auth-config` are mutually exclusive.
   - First generate with neither flag can write an inactive `auth.json` scaffold when the
     spec has security schemes — then edit and re-run with `--auth-config` + `--force`.
6. **`--login`** — after generate, run interactive OAuth for `authorization_code` profiles
   (needs `callbackUrl` in `auth.json`).
7. **OAuth fields in `auth.json`** (when relevant): `callbackUrl` (e.g.
   `http://localhost:8400/callback`), `tokenField` / token response mapping as documented
   in `wiki/Authentication.md`.
8. **`--force` / `-f`** — overwrite an existing output directory.
9. **`--insecure`** — accept untrusted TLS when fetching the spec URL (dev only; also
   affects generated dispatcher default for HTTPS).
10. **Update vs generate** — if a skill with `.api2skill.json` already exists, prefer
    `api2skill update <skill-path> [new-spec] [--name …] [--out …]`.

Also ask about optional filters when useful: `--include` / `--exclude` (`tag:…`, `path:…`,
`op:…`), `--format json|yaml`, `--base-url`.

## Commands to emit

Always print complete commands. Examples (adapt to the user's answers):

```bash
# Basic generate
api2skill generate ./openapi.json --out ./skills/my-api --name my-api --script cs

# Fetch remote spec (dev cert)
api2skill generate https://svc.local/swagger.json --insecure --out ./skills/my-api

# Bearer shorthand
api2skill generate ./openapi.json --auth bearer --out ./skills/my-api

# Explicit auth + interactive OAuth login
api2skill generate ./openapi.json --auth-config ./auth.json --login --out ./skills/my-api --force

# Refresh existing skill
api2skill update ./skills/my-api ./openapi-v2.json
api2skill update ./skills/my-api --name my-api-prod --out ./skills/my-api-prod
```

After generate, remind them:

```bash
cd <skill-dir>
cp secrets.example.json secrets.json   # fill credentials
# then run operations via the dispatcher under scripts/
```

## Auth JSON hints (for agents drafting auth.json)

When the user needs OAuth / Entra, point them at `wiki/Authentication.md` and ensure
profiles include the right `type`, endpoints, scopes, secrets placeholders (`{secret:…}`),
and for authorization-code flows a `callbackUrl`. Do not invent secret values.

## After install

This skill lives at `<skills-root>/api2skill-creator/SKILL.md`. Install or refresh it with:

```bash
api2skill install-creator --target <skills-root> [--force]
# or interactively: api2skill install-creator
```

Supported project skills roots: `.cursor/skills/`, `.claude/skills/`, `.github/skills/`,
`.agents/skills/` — see `wiki/Install-Creator.md`.
