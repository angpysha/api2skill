# Contract — CLI surface (012 multi-spec)

Additive changes to `api2skill` CLI. Existing commands retain prior behavior for single-spec invocations.

## `generate` (extended)

```
api2skill generate [<spec-source>] [options]
```

### New / changed options

| Option | Type | Default | Behavior |
|--------|------|---------|----------|
| `--spec <source>` | repeatable | — | Additional spec source; with first `--spec`, positional optional |
| `--spec-id <id>` | string | slug of source | Applies to the **most recent** `--spec` |
| `--spec-label <text>` | string | `id` | Applies to the **most recent** `--spec` |
| `--on-conflict <mode>` | `fail` \| `prefix` | `fail` | `operationId` collision across parts (D3) |

When multiple parts: all resolved base URLs must match unless `--base-url` supplied.

### Examples

```bash
api2skill generate \
  --spec ./public.json --spec-id public \
  --spec ./admin.json --spec-id admin \
  --base-url https://api.example.com \
  --out ./skills/acme

api2skill generate ./single.json --out ./skills/single   # unchanged
```

## `add-spec` (new)

```
api2skill add-spec <skill-path> <spec-source> [options]
```

| Option | Behavior |
|--------|----------|
| `--spec-id <id>` | Required if `id` slug would collide; else defaults to filename slug |
| `--spec-label <text>` | Optional display label |
| `--on-conflict` | Same as generate |

Regenerates `<skill-path>` in place (`--force` semantics). Preserves `secrets.json`, `auth.json`, `.auth-cache.json`.

**Exit codes**: same as `update` / `generate --force`.

## `merge` (new)

```
api2skill merge --into <target-dir> <skill-path> [<skill-path> ...]
```

| Option | Behavior |
|--------|----------|
| `--into <dir>` | Required target directory |
| `--force` | Overwrite existing api2skill skill at target |
| `--on-conflict` | Same as generate |

Requires **≥2** source skill paths. Does not delete sources. Unions `secrets.json` and `.auth-cache.json` (first wins on key conflict + warning; no silent deletion). `auth.json`: first source that has one for a new target; preserve target on `--force` into existing. Fail if any source `scriptKind` differs from canonical (existing target when force into composite; else all sources must match).

**Not** for appending one OpenAPI file to an existing skill — use `add-spec`.

## `update` (extended)

```
api2skill update <skill-path> [<spec-source>] [--spec-id <id>] [--name …] [--out …]
```

| Case | Behavior |
|------|----------|
| No positional | Re-acquire **all** `specSources` from manifest |
| Positional + 1 part | Replace sole source (unchanged) |
| Positional + N>1 parts | **Requires** `--spec-id`; replaces that part only; then refresh all |

## `creator check-update` (shipped)

```
api2skill creator check-update [--working-dir <dir>]
```

Scans four standard project skill roots. Exit **0** = up to date or not installed; exit **8** = at least one installation older than bundled.

## `install-creator` (extended, shipped)

When overwriting on TTY and bundled version > installed: prompts `Update available: X → Y. Install update? [y/N]`.

Writes `.creator-version.json` beside `SKILL.md` on success.

## Exit codes (additions)

| Code | Command | Meaning |
|------|---------|---------|
| 8 | `creator check-update` | Update available for installed creator skill |

Composition errors (base URL mismatch, duplicate `specId`, `operationId` conflict without `--on-conflict prefix`) → **2** (`UsageError`).
