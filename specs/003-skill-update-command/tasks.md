# Tasks: Skill Update Command

**Input**: Design documents from `specs/003-skill-update-command/`

**Tests**: Included — constitution test-first default applies.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [x] T001 [P] Create `tests/Api2Skill.Tests/Output/` directory (if absent) for manifest unit tests

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The manifest type and its wiring into `generate` — both user stories need it.

- [x] T002 [P] Define `SkillManifest` record (`Name`, `SpecSource`, `ScriptKind`, `Include`, `Exclude`, `Format`, `BaseUrl`, `Insecure`) in `src/Api2Skill/Output/SkillManifest.cs`
- [x] T003 Implement `SkillManifestIo.Serialize(SkillManifest)` and `SkillManifestIo.TryLoad(string skillDirectory)` (via `System.Text.Json.Nodes.JsonObject`, matching `SecretsScaffold`'s style; `TryLoad` returns `null` on missing file or malformed JSON) in `src/Api2Skill/Output/SkillManifest.cs` (depends on T002)
- [x] T004 Extend `SkillWriter.Write` with an optional `manifestJson` parameter, written into staging as `.api2skill.json` whenever supplied (always overwritten — no preservation logic, unlike `auth.json`) in `src/Api2Skill/Output/SkillWriter.cs` (depends on T003)
- [x] T005 In `GenerateCommand.RunAsync`, build a `SkillManifest` from the resolved name and `GenerateOptions` right before calling `SkillWriter.Write`, and pass its serialized form through (depends on T002, T004)
- [x] T006 [P] Unit tests: `SkillManifestIo` serialize → parse round-trip; missing file → `null`; malformed JSON → `null`; manifest JSON contains no secret-shaped values in `tests/Api2Skill.Tests/Output/SkillManifestTests.cs` (depends on T003)
- [x] T007 [P] Golden/CLI test: a `generate` run writes `.api2skill.json` with the expected fields in `tests/Api2Skill.Tests/Cli/ExitCodeTests.cs` (extend)

**Checkpoint**: Every `generate` now writes a correct manifest. `update` can be built.

---

## Phase 3: User Story 1 - Update a skill from a new spec without retyping options (Priority: P1) 🎯 MVP

**Goal**: `api2skill update <path> [<new-spec>]` regenerates a skill using its manifest's recorded
options, without the caller re-supplying them.

**Independent Test**: generate with `--script fsx --include tag:pet`; run `update <path> <new-spec>`;
confirm the result is still `.fsx`, still filtered to `tag:pet`, but reflects the new spec's content.

### Tests for User Story 1 ⚠️

- [x] T008 [P] [US1] Integration test: `update` with a new spec source regenerates honoring the original `--script`/`--include`/`--exclude`/`--base-url`/`--insecure`, none of which are passed to `update`, in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs`
- [x] T009 [P] [US1] Integration test: `update` preserves `secrets.json`, `auth.json`, and `.auth-cache.json` exactly as `--force` already does, in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs` (extend)
- [x] T010 [P] [US1] Integration test: `update <path>` with **no** new spec source re-resolves the manifest's original source (e.g. re-reads the same file path) in `tests/Api2Skill.Tests/Integration/UpdateCommandIntegrationTests.cs` (extend)
- [x] T011 [P] [US1] Unit/CLI test: after a successful `update` with a new spec source, the rewritten manifest records that new source in `tests/Api2Skill.Tests/Cli/UpdateCommandTests.cs`

### Implementation for User Story 1

- [x] T012 [US1] Implement `UpdateCommand.Create()` (`skill-path` argument, optional `spec-source` argument) and `UpdateCommand.RunAsync`: load the manifest, build a `GenerateOptions` (`OutputDirectory = skillPath`, `Force = true`, `AuthConfigPath = null`, `AuthShorthand = null`, `Login = false`, the rest from the manifest, `SpecSource = newSpecSource ?? manifest.SpecSource`), and delegate to `GenerateCommand.RunAsync` in `src/Api2Skill/Cli/UpdateCommand.cs` (depends on T003)
- [x] T013 [US1] Register `update` on the root command in `src/Api2Skill/Program.cs` (depends on T012)
- [x] T014 [US1] Run quickstart-equivalent manual validation: generate with non-default options, edit the local spec fixture, run `update`, confirm output — manual validation

**Checkpoint**: User Story 1 fully functional and independently testable (MVP).

---

## Phase 4: User Story 2 - Clear failure when the target isn't an api2skill skill (Priority: P2)

**Goal**: `update` against a directory with no (or corrupt) manifest fails clearly and changes nothing.

### Tests for User Story 2 ⚠️

- [x] T015 [P] [US2] CLI test: `update` against a directory with no `.api2skill.json` fails with a clear message naming the missing manifest, writes/modifies nothing, in `tests/Api2Skill.Tests/Cli/UpdateCommandTests.cs` (extend)
- [x] T016 [P] [US2] CLI test: `update` against a directory with a malformed `.api2skill.json` fails the same way, in `tests/Api2Skill.Tests/Cli/UpdateCommandTests.cs` (extend)

### Implementation for User Story 2

- [x] T017 [US2] Wire the "no/invalid manifest" case in `UpdateCommand.RunAsync` to a clear error message and usage-error exit code (depends on T012)

**Checkpoint**: Both user stories independently functional.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [x] T018 [P] Regenerate the approved golden fixtures (`petstore-cs`/`petstore-fsx`/`petstore-csx`) to include `.api2skill.json`, and update `CsEmitterGoldenTests`/`FsxCsxGoldenTests` file-list expectations if needed
- [x] T019 [P] Update `README.md` with an `update` usage example
- [x] T020 File a beads issue for the explicitly deferred follow-up: supporting a skill rename (changing `--name`/output path) during `update` without orphaning secrets/auth state (beads: api2skill-4zx)
- [x] T021 Run the full test suite and confirm green

---

## Dependencies & Execution Order

- Setup → Foundational (blocks both stories) → US1 (MVP) → US2 → Polish.
- US2 depends only on Foundational + US1's `UpdateCommand` skeleton (T012), not on US1's tests passing first, but is naturally implemented right after.

## Implementation Strategy

MVP = Setup + Foundational + US1. US2's failure-mode polish and the Polish phase follow
immediately given the feature's small size — no reason to ship US1 alone here.
