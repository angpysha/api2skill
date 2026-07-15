# Contract — `reference/<tag>.md` operation detail (011)

Extends progressive-disclosure rules from `specs/001-openapi-to-skill/contracts/skill-output.md`.

## Per-operation sections (required order)

1. Heading `## {operationId}`
2. `` `{METHOD} {path}` ``
3. Summary / description (when present)
4. **Parameters** (omit table if none)
5. **Request body** or `**Request body**: none`
6. **Auth**
7. **Responses** (per status `### \`{code}\``)
8. Authored examples block (feature 010 linker — after op content, before `---`)
9. `---` separator

## Parameters table

Columns: `name | in | required | type | enum | description`

- `type` includes format when present: `Integer (int64)`
- `enum` lists backtick-quoted values when present
- After the table: optional `- \`{name}\` example: \`…\`` lines
- Object/array parameters: optional `**\`{name}\` ({In}) schema**` subsection with same schema detail rules as bodies

## Request body (JSON media)

```markdown
**Request body**

- Content-Type: `application/json` (required|optional)
- Schema: [`PetInput`](schemas/PetInput.json)   # when named
- Shape: object { … }

| property | type | required | enum | description |
|---|---|---|---|---|
| … |

Example:

```json
{ … indented pasteable document … }
```
```

If nesting truncated at depth 4:

```markdown
_Nested fields truncated at depth 4 — see [`PetInput`](schemas/PetInput.json) for the full schema._
```

## Request body (non-JSON media)

- Content-Type line + property metadata if any
- Explicit note: no pasteable body emitted for this content type
- Must not invent a fake JSON fence

## Responses

For each status:

- `### \`200\`: description`
- Content-Type when present
- Schema name link when named
- Shape + property table + Example fence for JSON
- Or: `- Body: none documented in the OpenAPI response`

## oneOf / anyOf

When present, after Shape (or instead of a single merged table when exclusive):

```markdown
**Variants** (use one of):

1. Shape: …
2. Schema: [`Alt`](schemas/Alt.json) — …
```

Pasteable Example uses the first variant with a one-line note.

## SKILL.md (unchanged contract)

MUST NOT include parameter tables, property tables, or pasteable JSON bodies. Index + link to `reference/<tag>.md` only.
