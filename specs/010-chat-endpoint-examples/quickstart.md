# Quickstart & Validation: Chat-authored endpoint examples

From repo root after build.

```bash
A2S="dotnet run --project src/Api2Skill --"
OUT=./out-examples-demo
```

## Scenario A — Add + link (US1)

```bash
$A2S generate tests/Api2Skill.Tests/fixtures/petstore.json --out "$OUT" --force
$A2S example add --skill "$OUT" --op addPet --name happy --request /dev/stdin <<'JSON'
{"name":"doggie","status":"available"}
JSON
test -f "$OUT/examples/addPet/happy/request.json"
grep -q 'examples/addPet/happy/request.json' "$OUT/reference/pet.md"
```

## Scenario B — Prefer guidance present (US2)

```bash
grep -q 'authored example' "$OUT/SKILL.md" || grep -qi 'Prefer' "$OUT/SKILL.md"
grep -qi 'explicitly approve\|human approval\|ask the user' "$OUT/SKILL.md"
```

## Scenario C — Preserve on force (US3)

```bash
$A2S generate tests/Api2Skill.Tests/fixtures/petstore.json --out "$OUT" --force
test -f "$OUT/examples/addPet/happy/request.json"
grep -q 'examples/addPet/happy/request.json' "$OUT/reference/pet.md"
```

## Scenario D — List / remove (US5)

```bash
$A2S example list --skill "$OUT"
$A2S example remove --skill "$OUT" --op addPet --name happy
test ! -d "$OUT/examples/addPet/happy"
```

## Scenario E — Failure protocol is documentation-only

Confirm SKILL.md contains ask → propose → await approval; no CLI auto-rewrites example files from HTTP failures.
