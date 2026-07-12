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
#      .api2skill.json        # generation manifest — records options for `update`
#      .gitignore             # excludes secrets.json
```

Drop the output directory into `~/.claude/skills/` (or a project's `.claude/skills/`) and
Claude can use it immediately.

**Full documentation:** [wiki/Home.md](wiki/Home.md) — getting started, CLI reference,
authentication, and [Mermaid diagrams](wiki/Generate-Command.md). Docs live in this repo under
`wiki/`; see [wiki/README.md](wiki/README.md) for how to browse them.

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

# Custom name and output path — options are recorded in .api2skill.json
api2skill generate ./petstore.json --name my-petstore --out ./skills/my-petstore

# Refresh when the spec changes (reuses saved --script/--include/--out from manifest)
api2skill update ./skills/my-petstore ./petstore-v2.json
api2skill update ./skills/my-petstore   # re-fetch original spec source

# Rename or relocate while updating (secrets.json, auth.json, .auth-cache.json move with it)
api2skill update ./skills/my-petstore ./petstore-v2.json --name petstore-prod --out ./apis/petstore
```

Then, inside the generated skill directory:

```bash
cp secrets.example.json secrets.json   # fill in real credentials
dotnet run scripts/call.cs -- getPetById --petId 3
```

## Authentication (basics)

| Approach | When to use |
|----------|-------------|
| `--auth bearer\|basic\|custom` | Single simple profile, quick scaffold |
| `--auth-config ./auth.json` | OAuth2/Entra, script auth, multi-profile |
| `--login` | After generation — interactive OAuth for `authorization_code` profiles |

```bash
# Quick bearer token scaffold
api2skill generate ./api.json --auth bearer

# Full auth config (OAuth2, Entra, script commands, custom headers)
api2skill generate ./api.json --auth-config ./auth.json --login
```

`--auth` and `--auth-config` are mutually exclusive. See
[wiki/Authentication.md](wiki/Authentication.md) for profile types, Entra preset, and script
auth examples.

## Project layout

```
src/Api2Skill/       the generator (console app)
tests/Api2Skill.Tests/   xUnit — unit, golden, and integration tests
wiki/                in-repo documentation (start at wiki/Home.md)
specs/               feature specs (Spec Kit)
```

## Status

MVP complete: three script emitters (`cs`/`fsx`/`csx`), explicit auth (`bearer`, `basic`,
`custom`, `script`, OAuth2/Entra), `generate` and `update` commands, filtering, and atomic
output staging. See [specs/001-openapi-to-skill/spec.md](specs/001-openapi-to-skill/spec.md)
for milestone scope.
