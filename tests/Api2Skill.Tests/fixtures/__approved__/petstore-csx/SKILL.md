---
name: "petstore"
description: "Call the Swagger Petstore API. A sample API that uses a petstore as an example to demonstrate api2skill."
---

# Swagger Petstore

A sample API that uses a petstore as an example to demonstrate api2skill.

## Overview

Base URL: `https://petstore.example.com/v2`

## Setup

Runner: `dotnet script scripts/call.csx --`

Copy `secrets.example.json` to `secrets.json` and fill in real credentials before calling authenticated operations. `secrets.json` is gitignored — never commit it.

Untrusted HTTPS (self-signed/invalid certificates) is **off by default**. Set `API2SKILL_INSECURE=1` to accept them — **dev/local use only**, never in production.

## Auth

| scheme | kind | secrets.json keys |
|---|---|---|
| `api_key` | ApiKey | apiKey |
| `petstore_auth` | OAuth2 | clientId, clientSecret, tokenUrl |

## How to call

```
dotnet script scripts/call.csx -- <operationId> [--<param> <value> ...] [--body <json|@file>]
```

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

