# Contract: CLI `api2skill example`

## Commands

### `example add`

| Option | Required | Description |
|--------|----------|-------------|
| `--skill` | yes | Skill root |
| `--op` | yes | `operationId` |
| `--name` | no | Default `default` |
| `--request` | * | Path or `-` for stdin JSON |
| `--response` | * | Path or `-` for stdin JSON |
| `--force` | no | Overwrite existing files |

\* At least one of `--request` / `--response`.

Writes files under `examples/<op>/<name>/` and patches `reference/<tag>.md` links (via shared linker). Exit `2` if op unknown (unless `--force-unknown` deferred — v1: unknown op = error). Exit `2` if exists without `--force`.

### `example list`

| Option | Required | Description |
|--------|----------|-------------|
| `--skill` | yes | |
| `--op` | no | Filter |

Prints table: operationId, name, hasRequest, hasResponse.

### `example remove`

| Option | Required | Description |
|--------|----------|-------------|
| `--skill` | yes | |
| `--op` | yes | |
| `--name` | yes | |

Deletes `examples/<op>/<name>/` and re-runs link sync for affected tag(s).

### `example sync`

| Option | Required | Description |
|--------|----------|-------------|
| `--skill` | yes | |

Re-scans `examples/` and rewrites authored-example sections in all `reference/*.md`. Warns orphans on stderr.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `2` | Usage / validation |
| `4` | Skill path missing |
