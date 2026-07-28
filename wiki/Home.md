# api2skill Wiki

**api2skill** converts an OpenAPI/Swagger document into a self-contained **Claude Agent Skill** —
`SKILL.md`, reference docs, a runnable dispatcher script, and credential scaffolding — so an
existing REST API becomes something Claude can call correctly, with authentication.

## Quick links

| Page | What you'll find |
|------|------------------|
| [Getting Started](Getting-Started.md) | Install, first skill, where to put it |
| [Multi-Spec Skills](Multi-Spec-Skills.md) | Several Swagger docs / same base URL → one skill |
| [Interactive Guides](Interactive-Guides.md) | HTML walkthrough + open in Canvas / Artifact / Sites |
| [Generate Command](Generate-Command.md) | Full `generate` reference, filtering, output layout |
| [Update Command](Update-Command.md) | Refresh a skill from a new spec via `.api2skill.json` |
| [Merge Command](Merge-Command.md) | Combine two or more existing skills into one composite |
| [Add-Spec Command](Add-Spec-Command.md) | Append one OpenAPI source to an existing skill |
| [Install Creator](Install-Creator.md) | Install `api2skill-creator`; Plan mode for multi-spec |
| [Authentication](Authentication.md) | `auth.json`, `--auth`, OAuth2/Entra, script auth |
| [Examples](Examples.md) | Authored request/response examples (`example` CLI) |
| [Releasing](Releasing.md) | Version bumps, tags, package release notes |
| [Roadmap](../ROADMAP.md) | Near / medium / long-term directions (docs only) |
| [How to Read These Docs](README.md) | Browse on GitHub or locally — no separate wiki repo |

## Commands at a glance

```bash
# Create a skill from a spec
api2skill generate ./petstore.json

# Several docs, same base URL → one skill
api2skill generate --spec ./public.json --spec-id public \
  --spec ./admin.json --spec-id admin --base-url https://api.example.com --out ./skills/acme

# Refresh an existing skill (reuses saved options from .api2skill.json)
api2skill update ./my-petstore ./petstore-v2.json

# Merge previously separate skills / append one OpenAPI file
api2skill merge --into ./skills/acme ./skills/acme-public ./skills/acme-admin
api2skill add-spec ./skills/acme ./billing.json --spec-id billing

# Install the api2skill-creator helper skill into project agent roots
api2skill install-creator
api2skill creator check-update
```

For multi-doc APIs, start with [Multi-Spec Skills](Multi-Spec-Skills.md). Prefer
**Plan mode** with the [creator skill](Install-Creator.md) before running commands.

## What gets generated

```mermaid
flowchart LR
    Spec[OpenAPI spec] --> CLI[api2skill generate]
    CLI --> Skill[Skill directory]
    Skill --> MD[SKILL.md]
    Skill --> Ref[reference/]
    Skill --> Script[scripts/call.*]
    Skill --> Secrets[secrets.example.json]
    Skill --> Auth[auth.json optional]
    Skill --> Manifest[.api2skill.json]
```

Each generated skill is a drop-in folder for `~/.claude/skills/` or a project's
`.claude/skills/`.

## Source of truth

This `wiki/` folder in the main repository is the **authoritative documentation**. Edit pages
here and commit with the rest of the project — see [How to Read These Docs](README.md) for
browsing and contribution workflow.

For design contracts and feature specs, see `specs/` in the repository.
