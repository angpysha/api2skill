# Quickstart: Auth Template Scaffold & Script Working Directory

**Feature**: `006-auth-template-scaffold`

## Prerequisites

- Built `api2skill` CLI from repo root: `dotnet build src/Api2Skill`
- Fixture: `tests/Api2Skill.Tests/fixtures/multi-auth.yaml`

## 1. Auto-scaffold on first generate

```bash
dotnet run --project src/Api2Skill -- generate \
  tests/Api2Skill.Tests/fixtures/multi-auth.yaml \
  --name multi-auth-scaffold-test \
  --out /tmp/multi-auth-scaffold-test
```

**Expected**:

- Exit `0`
- `/tmp/multi-auth-scaffold-test/auth.json` exists
- Active `profiles[].name` match scheme IDs from the spec
- Top-level `_guidance` and (if tags differ) `_tagAttachExamples` present
- `SKILL.md` contains **Auth profile names** section
- Dispatcher still uses spec-derived auth (no `--auth-config` yet)

**Validate JSON**:

```bash
# profiles-only subset must load (manual check or unit test)
grep -q '"profiles"' /tmp/multi-auth-scaffold-test/auth.json
```

## 2. No scaffold when auth-less spec

```bash
dotnet run --project src/Api2Skill -- generate \
  tests/Api2Skill.Tests/fixtures/petstore.json \
  --name petstore-no-scaffold \
  --out /tmp/petstore-no-scaffold
```

**Expected**: no `auth.json` (petstore has no security schemes in filtered ops, or none at all).

## 3. Activate explicit auth after edit

```bash
cp /tmp/multi-auth-scaffold-test/secrets.example.json /tmp/multi-auth-scaffold-test/secrets.json
# fill secrets…

dotnet run --project src/Api2Skill -- generate \
  tests/Api2Skill.Tests/fixtures/multi-auth.yaml \
  --auth-config /tmp/multi-auth-scaffold-test/auth.json \
  --force \
  --out /tmp/multi-auth-scaffold-test
```

**Expected**: exit `0`; `SKILL.md` shows **Explicit auth profiles** table; calls use explicit auth.

## 4. `--force` preserves existing auth.json

```bash
# second generate without --auth-config
dotnet run --project src/Api2Skill -- generate \
  tests/Api2Skill.Tests/fixtures/multi-auth.yaml \
  --force \
  --out /tmp/multi-auth-scaffold-test
```

**Expected**: `auth.json` bytes unchanged from step 3 (or 1 if step 3 skipped).

## 5. Script auth working directory

Generate a skill with a `script` profile whose command writes a sentinel file:

```bash
touch /tmp/skill-root/sentinel-test.sh  # after manual skill setup with script auth
# command: sh -c 'echo ok > .script-cwd-sentinel'

# invoke dispatcher from a different cwd
cd /tmp && dotnet run --project /tmp/skill-root/scripts/call.cs -- <operationId>
```

**Expected**: `.script-cwd-sentinel` appears in skill root, not `/tmp`.

(Integration test automates this — see tasks.md T019–T021.)

## 6. Tests

```bash
dotnet test tests/Api2Skill.Tests --filter "FullyQualifiedName~AuthScaffold|FullyQualifiedName~ScriptAuth"
```

All tests green before merge.
