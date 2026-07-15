# Quickstart — Full request/response schema in reference docs

Validate that generated skills expose complete input/output models and raw schema files.

## Prerequisites

- Built `api2skill` from this branch (`dotnet build src/Api2Skill`)
- Fixture: `tests/Api2Skill.Tests/fixtures/petstore.json` (must include `$ref` components)

## Scenario A — Generate and inspect reference

```bash
OUT=$(mktemp -d)
dotnet run --project src/Api2Skill -- generate \
  tests/Api2Skill.Tests/fixtures/petstore.json \
  -o "$OUT/petstore" --name petstore --force

# Parameters + enums
grep -q 'findPetsByStatus' "$OUT/petstore/reference/pet.md"
grep -q '`available`' "$OUT/petstore/reference/pet.md"

# Pasteable request body + property table
grep -q '**Request body**' "$OUT/petstore/reference/pet.md"
grep -q '| property | type | required | enum | description |' "$OUT/petstore/reference/pet.md"
grep -q '```json' "$OUT/petstore/reference/pet.md"

# Named schema link + raw file
test -f "$OUT/petstore/reference/schemas/Pet.json"
test -f "$OUT/petstore/reference/schemas/PetInput.json"
grep -q 'schemas/PetInput.json' "$OUT/petstore/reference/pet.md"

# Progressive disclosure
! grep -q 'ID of pet to return' "$OUT/petstore/SKILL.md"
grep -q 'ID of pet to return' "$OUT/petstore/reference/pet.md"

# Raw schema retains structure (not a one-field stub)
grep -q '"properties"' "$OUT/petstore/reference/schemas/Pet.json"
```

**Expect**: Exit 0 from greps/`test`; skill usable without the original OpenAPI path.

## Scenario B — Goldens

```bash
dotnet test tests/Api2Skill.Tests --filter "FullyQualifiedName~CsEmitterGolden|FullyQualifiedName~SchemaDetail"
```

**Expect**: Approved trees under `fixtures/__approved__/petstore-*/reference/` include updated `pet.md` and `schemas/*.json`.

## Scenario C — Update refreshes schemas

```bash
# After generating once, change a component description in a copy of the fixture,
# then:
api2skill update "$OUT/petstore" /path/to/updated-petstore.json
grep -q 'UPDATED_MARKER' "$OUT/petstore/reference/schemas/Pet.json"
```

**Expect**: Schema files and tag MD refresh; `examples/` (if any) preserved.

## See also

- [contracts/skill-reference.md](./contracts/skill-reference.md)
- [contracts/schemas-layout.md](./contracts/schemas-layout.md)
- [data-model.md](./data-model.md)
