# Merge Command

```
api2skill merge --into <target-dir> <skill-path> <skill-path> [<skill-path> ...]
```

Combines **two or more** previously generated skill directories into one composite skill.
Source directories are left intact. For appending a single OpenAPI file to an existing skill,
use [`add-spec`](Add-Spec-Command.md) instead.

Full workflow context: [Multi-Spec Skills](Multi-Spec-Skills.md).

## Options

| Option | Description |
|--------|-------------|
| `--into <dir>` | Required target directory |
| `--force` / `-f` | Overwrite an existing api2skill skill at the target |
| `--on-conflict` | `fail` (default) or `prefix` for duplicate `operationId`s |

Parts must share the same base URL (or the first skill's recorded `--base-url` / servers must
agree across sources). See [Multi-Spec Skills](Multi-Spec-Skills.md#same-base-url).

## Credential union

`secrets.json` and `.auth-cache.json` keys are unioned; on conflict the **first** source wins
and a warning is printed. New targets take `auth.json` from the first source that has one;
`--force` into an existing skill preserves the target `auth.json`.

## Example

```bash
# You previously generated separate skills for the same host
api2skill merge --into ./skills/acme ./skills/acme-public ./skills/acme-admin

# Overwrite an existing composite at the target
api2skill merge --into ./skills/acme ./skills/acme-public ./skills/acme-admin --force
```

See also [`generate --spec`](Generate-Command.md), [`add-spec`](Add-Spec-Command.md),
[`update`](Update-Command.md).
