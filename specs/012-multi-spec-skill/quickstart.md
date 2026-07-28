# Quickstart — Multi-Spec Skill Composition

Validation guide for feature `012-multi-spec-skill`. Run from repository root after implementation.

## Prerequisites

- .NET 10 SDK
- `dotnet build src/Api2Skill/Api2Skill.csproj`
- Test fixtures under `tests/Api2Skill.Tests/fixtures/` (add `multi-spec/` as part of implementation)

## 1. Creator self-update (D11 — already shipped)

```bash
dotnet run --project src/Api2Skill -- creator check-update
# Expect: table of four skill roots; exit 0 or 8

dotnet run --project src/Api2Skill -- install-creator --target .cursor/skills --force
# Expect: SKILL.md + .creator-version.json with package version
```

## 2. Generate composite skill (US1)

```bash
dotnet run --project src/Api2Skill -- generate \
  --spec tests/Api2Skill.Tests/fixtures/multi-spec/public.json --spec-id public \
  --spec tests/Api2Skill.Tests/fixtures/multi-spec/admin.json --spec-id admin \
  --base-url https://api.example.com \
  --out /tmp/acme-composite

test -f /tmp/acme-composite/.api2skill.json
jq '.specSources | length' /tmp/acme-composite/.api2skill.json   # expect 2
grep -q "## API parts" /tmp/acme-composite/SKILL.md
```

**Expect**: Both operation sets in one `SKILL.md`; auth schemes from both parts; staged write on failure (no partial dir).

## 3. Base URL mismatch (negative)

```bash
dotnet run --project src/Api2Skill -- generate \
  --spec <fixture-a-with-different-servers> --spec-id a \
  --spec <fixture-b> --spec-id b \
  --out /tmp/should-fail
# Expect: exit 2, message listing conflicting URLs per specId, no output dir
```

## 4. Merge existing skills (US2)

```bash
# Generate two single-spec skills sharing base URL, then:
dotnet run --project src/Api2Skill -- merge --into /tmp/acme-merged \
  /tmp/acme-public /tmp/acme-admin

# Sources unchanged on disk
test -d /tmp/acme-public && test -d /tmp/acme-admin
jq '.specSources | length' /tmp/acme-merged/.api2skill.json   # expect 2
```

## 5. Add-spec (US3)

```bash
dotnet run --project src/Api2Skill -- add-spec /tmp/acme-composite \
  tests/Api2Skill.Tests/fixtures/multi-spec/billing.json --spec-id billing

jq '.specSources | length' /tmp/acme-composite/.api2skill.json   # expect 3
# secrets.json, auth.json, .auth-cache.json byte-identical if pre-seeded
```

## 6. Update all parts (US4)

```bash
dotnet run --project src/Api2Skill -- update /tmp/acme-composite
# Expect: all recorded sources re-read; manifest unchanged except timestamps N/A

dotnet run --project src/Api2Skill -- update /tmp/acme-composite \
  ./new-admin.json --spec-id admin
# Expect: admin source replaced; public refreshed from manifest
```

## 7. Legacy manifest (SC-005)

```bash
# Skill with only "specSource" in .api2skill.json
dotnet run --project src/Api2Skill -- update ./legacy-skill
# Expect: success without manual migration
```

## 8. Unit / integration tests

```bash
dotnet test tests/Api2Skill.Tests/Api2Skill.Tests.csproj \
  --filter "FullyQualifiedName~Composite|FullyQualifiedName~Merge|FullyQualifiedName~AddSpec|FullyQualifiedName~CreatorSkill"
```

## 9. Creator skill content (US5)

```bash
grep -q "creator check-update" src/Api2Skill/templates/api2skill-creator/SKILL.md
grep -q "secrets.example.json" src/Api2Skill/templates/api2skill-creator/SKILL.md
grep -q "base URL" src/Api2Skill/templates/api2skill-creator/SKILL.md
```

Human check: agent interview flow in spec § Creator skill contract.
