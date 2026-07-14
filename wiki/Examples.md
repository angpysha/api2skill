# Examples (authored request/response)

Skills can store **optional** named request/response payloads under:

```text
examples/<operationId>/<name>/request.json
examples/<operationId>/<name>/response.json
```

`reference/<tag>.md` links them in an **Authored examples** table. Skills ship with **zero** examples until you add them. Never put secrets in example files.

## CLI

```bash
api2skill example add --skill ./my-skill --op addPet --name happy --request ./body.json
api2skill example list --skill ./my-skill
api2skill example remove --skill ./my-skill --op addPet --name happy
api2skill example sync --skill ./my-skill
```

`--force` on `add` overwrites existing files. Unknown `operationId` fails (exit 2).

## Regenerate

`generate --force` and `update` **preserve** the entire `examples/` tree and re-link surviving files into regenerated tag markdown (same lifecycle as `auth.json`).

## Agent guidance

`SKILL.md` instructs agents to prefer authored examples over inventing JSON, and on failure to **ask → propose → await explicit human approval** before changing examples or contracts.
