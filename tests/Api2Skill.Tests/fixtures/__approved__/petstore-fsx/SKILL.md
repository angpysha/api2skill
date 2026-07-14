---
name: "petstore"
description: "Call the Swagger Petstore API. A sample API that uses a petstore as an example to demonstrate api2skill."
---

# Swagger Petstore

A sample API that uses a petstore as an example to demonstrate api2skill.

## Overview

Base URL: `https://petstore.example.com/v2`

## Setup

Runner: `dotnet fsi scripts/call.fsx --`

Copy `secrets.example.json` to `secrets.json` and fill in real credentials before calling authenticated operations. `secrets.json` is gitignored — never commit it.

Untrusted HTTPS (self-signed/invalid certificates) is **off by default**. Set `API2SKILL_INSECURE=1` to accept them — **dev/local use only**, never in production.

## Auth (from the spec)

| scheme | kind | secrets.json keys |
|---|---|---|
| `api_key` | ApiKey | apiKey |
| `petstore_auth` | OAuth2 | clientId, clientSecret, tokenUrl |

## How to call

```
dotnet fsi scripts/call.fsx -- <operationId> [--<param> <value> ...] [--body <json|@file>]
```

## Authored examples

Optional request/response payloads live under `examples/<operationId>/<name>/` (`request.json` and/or `response.json`). Tag markdown in `reference/` links them. Skills ship with **no** examples until you add them.

### Prefer authored examples

When calling or testing an operation:

1. Look under `examples/<operationId>/` for named examples.
2. If one name → use its `request.json` as `--body` (or equivalent).
3. If several → ask the user which `name` to use (or pick `default` if present and the user did not specify).
4. Do **not** invent a JSON body when an authored request example exists, unless the user explicitly overrides.

### Failure protocol (mandatory)

If a call that used an authored example fails (non-success HTTP, auth error, unexpected body):

1. **Ask** the user whether the example should be updated.
2. Optionally **propose** a concrete patch to `request.json` / `response.json` and/or a contract (OpenAPI / auth) change.
3. **Do not apply** any write to examples or contracts until the user **explicitly approves**.
4. Never “fix forward” by silently overwriting examples.

### Adding examples (chat or CLI)

1. Create `examples/<operationId>/<name>/request.json` and/or `response.json` (secret-free payloads only).
2. Run `api2skill example sync --skill .` (or `api2skill example add --skill . --op <operationId> --name <name> --request …`).
3. Confirm `reference/<tag>.md` lists the example under **Authored examples**.

## Operations

### pet

Everything about your Pets

| operationId | method | path | summary | reference |
|---|---|---|---|---|
| `getPetById` | GET | `/pet/{petId}` | Find pet by ID | [reference/pet.md](reference/pet.md) |
| `addPet` | POST | `/pet` | Add a new pet to the store | [reference/pet.md](reference/pet.md) |

### store

Access to Petstore orders

| operationId | method | path | summary | reference |
|---|---|---|---|---|
| `get_store_inventory` | GET | `/store/inventory` | Returns pet inventories by status | [reference/store.md](reference/store.md) |

### default

| operationId | method | path | summary | reference |
|---|---|---|---|---|
| `get_health` | GET | `/health` | Health check (no tag, no auth) | [reference/default.md](reference/default.md) |

