# api2skill

Convert an OpenAPI/Swagger document into a self-contained **Claude Agent Skill** — a
`SKILL.md` plus a runnable dispatcher script — so an existing REST API becomes something
Claude can call correctly, with authentication, without you hand-writing the wrapper.

```bash
api2skill generate ./petstore.json
# -> ./swagger-petstore/
#      SKILL.md               # compact overview + auth setup + operation index
#      reference/<tag>.md     # full per-operation detail, loaded on demand
#      scripts/call.cs        # the dispatcher (.cs by default; --script fsx|csx also available)
#      secrets.example.json   # template — copy to secrets.json and fill in real credentials
#      .gitignore             # excludes secrets.json
```

Drop the output directory into `~/.claude/skills/` (or a project's `.claude/skills/`) and
Claude can use it immediately.

## Why

Writing a correct, well-documented Claude Skill for an API by hand is repetitive: endpoint and
parameter docs, auth handling, and example requests all have to be derived from the API's own
OpenAPI spec anyway. api2skill automates that derivation.

## Install

Requires the **.NET 10 SDK**.

```bash
dotnet tool install --global api2skill
```

Or build from source (this repo pins the SDK version via `global.json`):

```bash
dotnet build --configuration Release
dotnet run --project src/Api2Skill -- generate <spec> [options]
```

## Quickstart

```bash
# From a local file
api2skill generate ./petstore.json

# From a running service (self-signed dev cert: add --insecure)
api2skill generate https://svc.local/swagger.json --insecure

# Piped from stdin
curl -s https://api.example.com/openapi.yaml | api2skill generate - --format yaml

# Scope a large API down to what you need
api2skill generate ./stripe.json --include tag:Charges

# Pick a different script kind
api2skill generate ./petstore.json --script fsx   # or csx
```

Then, inside the generated skill directory:

```bash
cp secrets.example.json secrets.json   # fill in real credentials
dotnet run scripts/call.cs -- getPetById --petId 3
```

Full CLI reference, the generated-skill layout, auth details, and the untrusted-HTTPS
opt-in: **[docs/usage.md](docs/usage.md)**.

## What gets generated

- **`SKILL.md`** stays compact — an overview, auth setup, and a tag-grouped operation index
  only. Full parameter/schema detail lives in `reference/<tag>.md`, loaded on demand, so the
  skill stays usable even on large APIs (hundreds of operations).
- **The dispatcher** (`scripts/call.<ext>`) is a single script per skill —
  `call <operationId> --<param> <value> ...` — that owns base-URL resolution, request shaping,
  and auth for every operation, generated as **plain `HttpClient` / `System.Text.Json`** with
  no third-party dependency in the emitted code.
- **Auth**: `apiKey`, HTTP `bearer`, HTTP `basic`, and OAuth2 client-credentials are all
  generated for real — the dispatcher reads credentials from a gitignored `secrets.json` (never
  the spec, never embedded) and, for OAuth2, performs the token exchange itself.
- **Three script kinds**, selectable via `--script`: `.cs` (default, `dotnet run`, no extra
  install), `.fsx` (`dotnet fsi`, ships with the SDK), `.csx` (`dotnet script`, needs
  `dotnet tool install -g dotnet-script`). All three are full implementations, not a default
  plus stubs.

## Project layout

```
src/Api2Skill/       the generator (console app)
  Input/              spec acquisition: file, URL, stdin
  Parsing/            Microsoft.OpenApi wrapper
  Model/              the emitter-agnostic SkillModel + the OpenAPI -> SkillModel mapping
  Emit/               SKILL.md/reference/secrets writers + the three script emitters
  Output/             directory write orchestration (safe --force, no partial output)
  Cli/                the `generate` command

tests/Api2Skill.Tests/   xUnit — unit, golden/snapshot, and real subprocess integration tests

specs/001-openapi-to-skill/   the feature's spec/plan/research (Spec Kit)
```

## Status

MVP complete: all three emitters, all four auth schemes, all three input sources, filtering,
and the full exit-code contract are implemented and tested (unit + golden + real subprocess
integration against a live local server). See
[specs/001-openapi-to-skill/spec.md](specs/001-openapi-to-skill/spec.md) for what's explicitly
out of scope for this milestone (MCP-server output, compiled-client output, OAuth2 grants
beyond client-credentials, a `--install` convenience).
