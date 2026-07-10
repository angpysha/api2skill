# api2skill

> Living project brief. Created during `.agents adapt` on a greenfield repo.
> Edit anytime — re-run `Run .agents adapt --update` to refresh pack/agent recommendations.

## Vision

**api2skill** is a .NET console application that converts an OpenAPI/Swagger specification
into a ready-to-use Claude Agent Skill. Given an API definition, it generates a `SKILL.md`
(plus any supporting request/auth-handling scripts) so a developer can drop the output
straight into `~/.claude/skills/` or a project's `.claude/skills/` and have Claude call that
REST API correctly — without hand-writing the skill wrapper.

- **Users:** developers who want to expose an existing REST API (their own or a third
  party's) as a Claude Skill with minimal manual work.
- **Problem:** writing a correct, well-documented Skill for an API by hand is repetitive —
  auth handling, endpoint/parameter documentation, and example requests all have to be
  derived from the spec anyway.
- **Success looks like:** point the tool at a valid OpenAPI/Swagger document and get back a
  Skill package that Claude can load and use to make correct calls against that API,
  including auth, without further edits for common cases.

## Stack

- Language: C# / .NET (console application)
- Dependencies: TBD during implementation (e.g. an OpenAPI parsing library)
- CI/CD: none yet

## Delivery (optional)

- Tracker: beads (`task` prefix, per pipeline manifest default)
- PR: manual
- Hosts: Cursor and Claude Code (both already installed)

## Open questions (optional)

- Target OpenAPI spec versions (2.0 / 3.0 / 3.1) to support.
- Output format details: single `SKILL.md` vs. `SKILL.md` + generated helper scripts.
- Auth schemes to support out of the box (API key, Bearer/OAuth2, Basic).
