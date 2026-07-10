# Quickstart — validation guide

Runnable scenarios that prove the feature works end-to-end. References
[contracts/cli.md](./contracts/cli.md), [contracts/skill-output.md](./contracts/skill-output.md),
and [data-model.md](./data-model.md) rather than duplicating them.

## Prerequisites

- .NET 10 SDK (`dotnet --version` ≥ 10). For the `csx` scenario: `dotnet tool install -g dotnet-script`.
- Build: `dotnet build src/Api2Skill --configuration Release`.
- Fixtures: `tests/Api2Skill.Tests/fixtures/petstore.json` (Swagger Petstore),
  `.../multi-auth.yaml` (one op per auth scheme).

## Scenario 1 — Generate from a file (AC-1, SC-001)

```bash
dotnet run --project src/Api2Skill -- generate tests/Api2Skill.Tests/fixtures/petstore.json -o ./out
```

**Expect**: `./out/swagger-petstore/` containing `SKILL.md`, `scripts/call.cs`, `reference/*.md`,
`secrets.example.json`, `.gitignore`. Exit 0. `SKILL.md` lists operations by tag (index only).

## Scenario 2 — Authenticated call for each scheme (AC-2, SC-002)

```bash
cd ./out/swagger-petstore
cp secrets.example.json secrets.json     # then fill in real values (or point at a stub server)
dotnet run scripts/call.cs -- getPetById --petId 3
```

**Expect**: correct request to `<baseUrl>/pet/3` with the operation's auth applied; response body on
stdout. Repeat against `multi-auth` fixture to exercise apiKey / bearer / basic / oauth2 (oauth2
fetches a client-credentials token first). Validate against a stub HTTP server in integration tests.

## Scenario 3 — Three emitters produce runnable scripts (AC-4, SC-004)

```bash
for k in cs fsx csx; do
  dotnet run --project src/Api2Skill -- generate tests/.../petstore.json -o ./out-$k --script $k
done
# run each with its runner (dotnet run / dotnet fsi / dotnet script) — see contracts/skill-output.md
```

**Expect**: each `./out-$k/swagger-petstore/scripts/call.<ext>` runs successfully with its documented
runner.

## Scenario 4 — Secrets never committed; --force preserves them (AC-5, SC-005, NFR-1)

```bash
cd ./out/swagger-petstore && cp secrets.example.json secrets.json   # fill with a sentinel value
cd - && dotnet run --project src/Api2Skill -- generate tests/.../petstore.json -o ./out --force
grep -R "SENTINEL" ./out/swagger-petstore/SKILL.md ./out/swagger-petstore/scripts || echo "OK: no secret leaked"
cat ./out/swagger-petstore/secrets.json   # sentinel still present
```

**Expect**: generated files refreshed; `secrets.json` untouched; sentinel appears in no generated
file; `.gitignore` excludes `secrets.json`.

## Scenario 5 — URL + untrusted HTTPS (AC-6, EC-8)

```bash
dotnet run --project src/Api2Skill -- generate https://localhost:5001/swagger.json          # fails: TLS (exit 4)
dotnet run --project src/Api2Skill -- generate https://localhost:5001/swagger.json --insecure # succeeds
```

## Scenario 6 — Error handling (AC-7, AC-8)

```bash
echo "{ not valid openapi" | dotnet run --project src/Api2Skill -- generate - --format json   # exit 1, no output
dotnet run --project src/Api2Skill -- generate tests/.../petstore.json -o ./out                # exit 3 (exists, no --force)
```

## Scenario 7 — Large-API compactness (AC-3, SC-003)

Generate from a large fixture (e.g. a 100+ op spec) and assert `SKILL.md` size stays within budget
while per-tag detail lives in `reference/` — checked by a golden/size test.

## Done when

All seven scenarios pass in `tests/Api2Skill.Tests` (golden + integration). Maps to acceptance
criteria AC-1..AC-8 in [spec.md](./spec.md).
