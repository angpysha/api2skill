# Getting Started

## Prerequisites

- **.NET 10 SDK** (this repo pins the version via `global.json`)

## Install

```bash
dotnet tool install --global api2skill
```

Or build from source:

```bash
dotnet build --configuration Release
dotnet run --project src/Api2Skill -- generate <spec> [options]
```

## Your first skill

```bash
# From a local OpenAPI file
api2skill generate ./petstore.json
# -> ./swagger-petstore/   (default name is a slug of info.title)

# From a running service (self-signed dev cert: add --insecure)
api2skill generate https://svc.local/swagger.json --insecure

# Piped from stdin
curl -s https://api.example.com/openapi.yaml | api2skill generate - --format yaml
```

The output directory contains:

| File / folder | Purpose |
|---------------|---------|
| `SKILL.md` | Compact overview, auth setup, operation index |
| `reference/<tag>.md` | Full per-operation detail, loaded on demand |
| `scripts/call.<ext>` | Dispatcher — `call <operationId> --<param> <value> ...` |
| `secrets.example.json` | Template — copy to `secrets.json` and fill in credentials |
| `.api2skill.json` | Generation manifest for `update` |
| `.gitignore` | Excludes `secrets.json` and `.auth-cache.json` |

## Use the dispatcher

```bash
cd swagger-petstore
cp secrets.example.json secrets.json   # fill in real credentials
dotnet run scripts/call.cs -- getPetById --petId 3
```

Script kind depends on `--script` at generation time (`cs` default, or `fsx` / `csx`). See
[Generate Command](Generate-Command.md#script-kinds).

## Install for Claude

Copy or symlink the generated directory into:

- `~/.claude/skills/` — user-wide skills, or
- `<project>/.claude/skills/` — project-scoped skills

Claude reads `SKILL.md` and can invoke operations through the dispatcher.

## Next steps

- [Generate Command](Generate-Command.md) — filtering, custom name/path, `--force`
- [Update Command](Update-Command.md) — refresh when the spec changes
- [Install Creator](Install-Creator.md) — install `api2skill-creator` into agent skill roots
- [Authentication](Authentication.md) — bearer, basic, custom, OAuth2/Entra, script auth
