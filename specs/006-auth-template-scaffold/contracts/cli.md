# Contract: CLI changes (006)

Extends [002 cli.md](../../002-explicit-auth-config/contracts/cli.md). No new flags.

## Auto-scaffold behavior on `generate`

When **all** of:

- `--auth` is not set
- `--auth-config` is not set
- the filtered model references ≥1 security scheme

…and **no** `auth.json` is preserved from the target (see `--force` policy below), the generator
writes a scaffold `auth.json` per [auth-scaffold.md](./auth-scaffold.md).

The scaffold is **inactive** on that run: `SkillModel.AuthConfig` remains null; dispatchers use
spec-derived auth until the user re-generates with `--auth-config`.

## `--force` interaction

| Existing `auth.json` | `--auth` / `--auth-config` | Result |
|---------------------|---------------------------|--------|
| absent | neither | scaffold written |
| present | neither | **preserved** (no scaffold) |
| present | `--auth-config` | replaced with supplied file |
| present | `--auth` | replaced with shorthand scaffold |

Unchanged from 002 for explicit auth supplied; FR-001a adds: no auto-scaffold when preserving.

## Exit codes

No new exit codes. Invalid user-supplied `--auth-config` still exits `5`.

## `update`

Unchanged — never supplies auth; preserves existing `auth.json`. Does not re-scaffold.

## Script auth runtime (all emitters)

`script` profile commands run with `WorkingDirectory` = skill root (parent of `scripts/`).
Documented in [dispatcher-script-auth.md](./dispatcher-script-auth.md).
