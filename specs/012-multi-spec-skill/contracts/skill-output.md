# Contract — composite skill output

Additive layout on top of `specs/001-openapi-to-skill/contracts/skill-output.md`.

## SKILL.md additions (multi-part only)

When `specSources.length > 1`, insert section **API parts** after Overview:

```markdown
## API parts

| specId | label | source | operations |
|--------|-------|--------|------------|
| public | Public API | ./openapi-public.json | 12 |
| admin | Admin API | ./openapi-admin.json | 8 |
```

### Tag headings

- Unique tag name across parts → `### Users`
- Collision → `### [admin] Users`

### Auth table

When `SecuritySchemeModel.SourceSpecId` set, add column or suffix: `scheme (part: admin)`.

## Manifest

`.api2skill.json` uses `specSources[]` per [manifest.md](./manifest.md).

## Examples

Union of all parts by `operationId`. After `--on-conflict prefix`, warn if `examples/<old-opId>/` folders no longer match.

## reference/schemas/

On component name collision across parts: `reference/schemas/{specId}.{schemaName}.json`.

## Unchanged

- Single dispatcher under `scripts/`
- `secrets.example.json` + gitignored `secrets.json`
- Progressive disclosure: detail in `reference/<tag>.md`
