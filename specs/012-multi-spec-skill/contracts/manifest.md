# Contract — `.api2skill.json` (composite manifest)

**File**: `<skill-dir>/.api2skill.json`  
**Secret-free**: safe to commit.

## v2 shape (multi-spec)

```json
{
  "name": "acme-api",
  "specSources": [
    {
      "id": "public",
      "source": "./openapi-public.json",
      "label": "Public API"
    },
    {
      "id": "admin",
      "source": "https://api.example.com/swagger/admin/swagger.json",
      "label": "Admin API"
    }
  ],
  "scriptKind": "cs",
  "include": [],
  "exclude": [],
  "format": null,
  "baseUrl": "https://api.example.com",
  "insecure": false
}
```

## Legacy v1 (single-spec)

```json
{
  "name": "petstore",
  "specSource": "./petstore.json",
  "scriptKind": "cs",
  "include": [],
  "exclude": [],
  "format": null,
  "baseUrl": null,
  "insecure": false
}
```

**Loader rule**: If `specSources` is absent and `specSource` is present → synthesize:

```json
{ "id": "<slug-of-name-or-filename>", "source": "<specSource>", "label": "<id>" }
```

## Field rules

| Field | Rule |
|-------|------|
| `specSources[].id` | Unique within array; stable across `update` |
| `specSources[].source` | Updated when user passes new source for that `id` |
| `baseUrl` | Canonical API root after composition; may be null if taken from spec |
| `name` | Skill identity; rename via `update --name` (feature 004) |

## Not stored

- Real `secrets.json` values
- `auth.json` contents
- OAuth tokens / `.auth-cache.json`
