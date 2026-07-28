# Add-Spec Command

```
api2skill add-spec <skill-path> <spec-source> [--spec-id <id>] [--spec-label <text>] [--on-conflict fail|prefix]
```

Appends **one** OpenAPI/Swagger source to an existing skill and regenerates in place
(`--force` semantics). Preserves `secrets.json`, `auth.json`, and `.auth-cache.json`.

This is **not** `merge` — use [`merge`](Merge-Command.md) only when combining existing skill
directories. Full workflow context: [Multi-Spec Skills](Multi-Spec-Skills.md).

## Options

| Option | Description |
|--------|-------------|
| `--spec-id <id>` | Part id (default: filename slug); duplicate ids fail |
| `--spec-label <text>` | Optional display label |
| `--on-conflict` | `fail` (default) or `prefix` for duplicate `operationId`s |

The new part must agree with the skill's base URL (or pass/use a recorded `--base-url`).

## Example

```bash
api2skill add-spec ./skills/acme ./billing.json --spec-id billing --spec-label "Billing API"
```

See also [`generate --spec`](Generate-Command.md), [`merge`](Merge-Command.md),
[`update`](Update-Command.md).
