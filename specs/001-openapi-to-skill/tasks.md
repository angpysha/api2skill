---
description: "Task list — OpenAPI → Claude Skill generator (core)"
---

# Tasks: OpenAPI → Claude Skill generator (core)

**Input**: Design documents from `specs/001-openapi-to-skill/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/{cli,skill-output}.md, quickstart.md
**Tests**: INCLUDED — the constitution sets test-first as the standing default (Principle / Additional Constraints). Write tests first and ensure they FAIL before implementing.

**Organization**: by user story. The spec defines one formal story (US1, P1); it is decomposed here
into five independently-testable, incrementally-deliverable stories mapped to acceptance criteria
AC-1..AC-8. **MVP = US1 + US2** (a working, authenticated skill).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different files, no dependency on an incomplete task)
- **[Story]**: US1..US5 (setup/foundational/polish carry no story label)
- Paths are from repo root; project is single console app `src/Api2Skill` + `tests/Api2Skill.Tests` (plan.md).

---

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Create solution + projects per plan.md: `src/Api2Skill/Api2Skill.csproj` (net10.0, console) and `tests/Api2Skill.Tests/Api2Skill.Tests.csproj` (xUnit), plus an `Api2Skill.sln`.
- [ ] T002 Add NuGet deps to `src/Api2Skill/Api2Skill.csproj`: `Microsoft.OpenApi` 3.8.0 and `System.CommandLine` 2.0.x (per research.md R1/R2).
- [ ] T003 [P] Enable `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `src/Api2Skill/Api2Skill.csproj`; add repo `.editorconfig` (editorconfig-install skill).
- [ ] T004 [P] Add test fixtures: `tests/Api2Skill.Tests/fixtures/petstore.json` (Swagger Petstore) and `tests/Api2Skill.Tests/fixtures/multi-auth.yaml` (one op per apiKey/bearer/basic/oauth2), plus an empty `tests/Api2Skill.Tests/fixtures/__approved__/` dir for golden output.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: blocks all user stories.

- [ ] T005 Implement CLI skeleton with System.CommandLine `generate <spec-source>` verb + options record in `src/Api2Skill/Cli/GenerateCommand.cs` and `src/Api2Skill/Cli/GenerateOptions.cs` (surface per contracts/cli.md; uses GA `SetAction`). Wire from `src/Api2Skill/Program.cs`.
- [ ] T006 [P] Define the intermediate model records in `src/Api2Skill/Model/` — `SkillModel.cs`, `OperationModel.cs`, `ParameterModel.cs`, `RequestBodyModel.cs`, `SecuritySchemeModel.cs`, `ResponseModel.cs`, `TagGroup.cs` (fields per data-model.md).
- [ ] T007 [P] Implement `src/Api2Skill/Input/FormatSniffer.cs` (JSON vs YAML detection for buffered input, research.md R2/R3).
- [ ] T008 Implement `src/Api2Skill/Parsing/OpenApiLoader.cs` wrapping `OpenApiDocument.LoadAsync(stream, format, settings, ct)` → `ReadResult`; map diagnostics to actionable errors (FR-010). (depends T006)
- [ ] T009 Implement `src/Api2Skill/Model/SkillModelBuilder.cs` core: `OpenApiDocument` → `SkillModel` mapping, operationId synthesis from method+path (EC-3), deterministic collision disambiguation (EC-5), tag grouping with `default` bucket (EC-4). (depends T006, T008)
- [ ] T010 [P] Define `src/Api2Skill/Emit/IScriptEmitter.cs` (`Emit(SkillModel, DirectoryInfo)`) — the pluggable-emitter seam (Constitution III / FR-006).
- [ ] T011 Implement `src/Api2Skill/Output/SkillWriter.cs` base (directory layout + write orchestration; overwrite handling added in T024) and wire `GenerateCommand` → SpecSource → OpenApiLoader → SkillModelBuilder → SkillWriter. (depends T005, T009, T010)

**Checkpoint**: parse→model→write skeleton runs; user stories can begin.

---

## Phase 3: User Story 1 — File in → runnable `.cs` skill (Priority: P1) 🎯 MVP

**Goal**: `api2skill generate <file>` emits a loadable skill dir (SKILL.md + `.cs` dispatcher + per-tag reference) for the default emitter.

**Independent Test**: Run against `fixtures/petstore.json`; assert output tree matches golden and `SKILL.md` is index-only (AC-1, AC-3, SC-001, SC-003).

### Tests for User Story 1

- [ ] T012 [P] [US1] Unit tests for `SkillModelBuilder` (petstore: ops/params/tags/servers mapping, id synthesis, default tag) in `tests/Api2Skill.Tests/Parsing/SkillModelBuilderTests.cs` — write first, must FAIL.
- [ ] T013 [P] [US1] Golden test: generate `.cs` skill from `fixtures/petstore.json`, diff whole tree vs `fixtures/__approved__/petstore-cs/` in `tests/Api2Skill.Tests/Emit/CsEmitterGoldenTests.cs` — write first, must FAIL.

### Implementation for User Story 1

- [ ] T014 [US1] Implement `src/Api2Skill/Input/SpecSource.cs` file-acquisition path → `MemoryStream` (+ format via FormatSniffer). (depends T007)
- [ ] T015 [P] [US1] Implement `src/Api2Skill/Emit/SkillMdWriter.cs`: frontmatter, overview, how-to-call, tag-grouped operation index (progressive disclosure, FR-004). 
- [ ] T016 [P] [US1] Implement `src/Api2Skill/Emit/ReferenceWriter.cs`: `reference/<tag>.md` with full param/schema/response detail per tag.
- [ ] T017 [US1] Implement `src/Api2Skill/Emit/CsFileEmitter.cs`: `.cs` file-based dispatcher — operationId resolution, path/query/header/body shaping, `HttpClient` call, stdout of response (no auth yet). Uses only `System.Net.Http`+`System.Text.Json` (Constitution II).
- [ ] T018 [US1] Complete the file→`.cs` happy path end-to-end + stdout summary (path, op count, emitter); capture and approve the golden tree so T012/T013 pass.

**Checkpoint**: US1 works standalone — MVP-loadable skill from a file (AC-1/AC-3).

---

## Phase 4: User Story 2 — Authenticated calls + secrets (Priority: P1)

**Goal**: Generated dispatcher authenticates via apiKey/bearer/basic/oauth2 from a gitignored secrets file; `--force` preserves real secrets.

**Independent Test**: For each scheme in `fixtures/multi-auth.yaml`, dispatcher makes an authenticated call to a stub server; re-run with `--force` keeps a filled `secrets.json` and leaks no secret (AC-2, AC-5, SC-002, SC-005).

### Tests for User Story 2

- [ ] T019 [P] [US2] Unit tests: per-scheme auth codegen (apiKey header/query, bearer, basic, oauth2 client-credentials) in `tests/Api2Skill.Tests/Auth/AuthCodegenTests.cs` — write first, must FAIL.
- [ ] T020 [P] [US2] Integration test: run the `.cs` dispatcher against a stub HTTP server, one op per scheme, assert correct auth applied, in `tests/Api2Skill.Tests/Integration/DispatcherAuthTests.cs` — write first, must FAIL.
- [ ] T021 [P] [US2] Test: `--force` preserves a filled `secrets.json` and no generated file contains the sentinel value, in `tests/Api2Skill.Tests/Output/ForcePreservesSecretsTests.cs` — write first, must FAIL.

### Implementation for User Story 2

- [ ] T022 [US2] Extend `SkillModelBuilder` to map `securitySchemes` → `SecuritySchemeModel` (Kind, ApiKey name/location, OAuth token URL/scopes, SecretKeys; `Unsupported`→warning per EC-6) in `src/Api2Skill/Model/SkillModelBuilder.cs`. (depends T009)
- [ ] T023 [P] [US2] Implement `src/Api2Skill/Emit/SecretsScaffold.cs`: emit `secrets.example.json` (one entry per scheme id + `baseUrl` when no servers) and a `.gitignore` excluding `secrets.json` (data-model.md, FR-003b, NFR-1).
- [ ] T024 [US2] Add auth injection to `src/Api2Skill/Emit/CsFileEmitter.cs`: load `secrets.json`, apply the operation's scheme; oauth2 → POST client-credentials to token URL, cache token in-memory, apply as bearer (research.md R5).
- [ ] T025 [US2] Implement `--force` + secrets preservation and dir-exists failure (exit 3) in `src/Api2Skill/Output/SkillWriter.cs` (FR-009, EC-10).
- [ ] T026 [US2] Add auth-setup section to `SkillMdWriter` (which secrets to fill, copy-from-example, dev-only `--insecure` note).

**Checkpoint**: US1+US2 = MVP — a working, authenticated skill (AC-2/AC-5).

---

## Phase 5: User Story 3 — URL & stdin input + untrusted HTTPS (Priority: P2)

**Goal**: Accept spec from a URL or stdin; `--insecure` allows self-signed TLS for both fetch and generated calls.

**Independent Test**: Fetch a spec from a self-signed dev URL (fails without `--insecure`, succeeds with); pipe a spec via stdin (AC-6, EC-8; FR-001).

### Tests for User Story 3

- [ ] T027 [P] [US3] Integration test: URL fetch against a self-signed local server fails without `--insecure`, succeeds with, in `tests/Api2Skill.Tests/Input/UrlFetchTlsTests.cs` — write first, must FAIL.
- [ ] T028 [P] [US3] Test: stdin (non-seekable) is buffered and parsed with explicit/sniffed format, in `tests/Api2Skill.Tests/Input/StdinSourceTests.cs` — write first, must FAIL.

### Implementation for User Story 3

- [ ] T029 [US3] Extend `src/Api2Skill/Input/SpecSource.cs` with URL fetch (own `HttpClient`, optional `DangerousAcceptAnyServerCertificateValidator`) and stdin buffering → `MemoryStream` (research.md R3, fixes non-seekable #2638).
- [ ] T030 [US3] Thread `--insecure`/`API2SKILL_INSECURE` from `GenerateOptions` into spec fetch AND into the emitted dispatcher's `HttpClientHandler` (FR-007, NFR-6).

**Checkpoint**: all three input sources + dev-mode TLS work (AC-6).

---

## Phase 6: User Story 4 — `.fsx` and `.csx` emitters (Priority: P2)

**Goal**: `--script fsx|csx` emits an equivalent dispatcher runnable by `dotnet fsi` / `dotnet script`.

**Independent Test**: Generate each kind from petstore and run it with its runner successfully (AC-4, SC-004; validates the pluggable-emitter design).

### Tests for User Story 4

- [ ] T031 [P] [US4] Golden tests for `.fsx` and `.csx` output from petstore in `tests/Api2Skill.Tests/Emit/FsxCsxGoldenTests.cs` — write first, must FAIL.
- [ ] T032 [P] [US4] Integration test: run `.fsx` via `dotnet fsi` and `.csx` via `dotnet script` against the stub server in `tests/Api2Skill.Tests/Integration/EmitterRunnerTests.cs` — write first, must FAIL.

### Implementation for User Story 4

- [ ] T033 [P] [US4] Implement `src/Api2Skill/Emit/FsxEmitter.cs` (`.fsx` dispatcher, `dotnet fsi` runner) consuming only `SkillModel`.
- [ ] T034 [P] [US4] Implement `src/Api2Skill/Emit/CsxEmitter.cs` (`.csx` dispatcher, `dotnet-script` runner) consuming only `SkillModel`.
- [ ] T035 [US4] Wire `--script cs|fsx|csx` selection in `GenerateCommand`/`SkillWriter` and add the emitter-specific runner note to `SkillMdWriter` (FR-006a/FR-006b).

**Checkpoint**: three emitters all produce runnable skills (AC-4).

---

## Phase 7: User Story 5 — Filtering, error handling & edge cases (Priority: P3)

**Goal**: `--include`/`--exclude` scope large APIs; robust exit codes; empty spec handled.

**Independent Test**: Filter a large spec to a subset; assert exit codes for invalid spec (1), existing dir (3), usage error (2), fetch failure (4); empty spec → minimal skill + warning (0) (AC-7, AC-8, FR-004b, FR-010, OQ-4).

### Tests for User Story 5

- [ ] T036 [P] [US5] Unit tests: `--include`/`--exclude` selectors (tag/path/op) recompute referenced schemes, in `tests/Api2Skill.Tests/Model/FilterTests.cs` — write first, must FAIL.
- [ ] T037 [P] [US5] Tests: exit codes (invalid→1 no-partial-output, exists→3, usage→2, fetch→4) and empty-spec→minimal+0, in `tests/Api2Skill.Tests/Cli/ExitCodeTests.cs` — write first, must FAIL.

### Implementation for User Story 5

- [ ] T038 [US5] Implement filter application (tag:/path:/op: selectors, include-then-exclude, recompute schemes) in `src/Api2Skill/Model/SkillModelBuilder.cs` (FR-004b).
- [ ] T039 [US5] Implement exit-code mapping + no-partial-output guarantee (write to a temp dir, move on success) in `src/Api2Skill/Output/SkillWriter.cs` and `GenerateCommand` (FR-010, EC-1).
- [ ] T040 [US5] Handle empty/operation-less `SkillModel` → minimal SKILL.md + warning, exit 0, in `SkillMdWriter`/`GenerateCommand` (OQ-4).

**Checkpoint**: filtering + full error contract complete (AC-7/AC-8).

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T041 [P] Determinism pass: enforce stable ordering in all writers/emitters so output is byte-stable (NFR-4); add a regenerate-twice test in `tests/Api2Skill.Tests/DeterminismTests.cs`.
- [ ] T042 [P] Add edge-case unit tests: operationId collisions (EC-5), no-`servers` `baseUrl` prompt (EC-7), unsupported scheme warning (EC-6) in `tests/Api2Skill.Tests/EdgeCaseTests.cs`.
- [ ] T043 [P] Write `README.md` + `docs/usage.md` (CLI surface, emitters, secrets, dev-only `--insecure`) — tech-writer Phase 9 input.
- [ ] T044 Run all seven `quickstart.md` scenarios end-to-end and record results.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → no deps.
- **Foundational (P2)** → after Setup; **blocks all stories**.
- **US1 (P3 phase)** → after Foundational. **US2** → after Foundational (builds on US1's emitter/writer). **US3, US4, US5** → after Foundational; each independently testable.
- **Polish (P8)** → after the stories you intend to ship.

### Story dependencies

- **US1 (P1)**: after Foundational; no story deps. 🎯 MVP core.
- **US2 (P1)**: extends US1's `CsFileEmitter`/`SkillWriter`/`SkillMdWriter` — sequence US2 after US1.
- **US3 (P2)**: touches `SpecSource`/options only — independent of US2 (parallelizable with US2/US4).
- **US4 (P2)**: new emitter files — independent (parallelizable), but golden tests assume US1 writers.
- **US5 (P3)**: builder filters + writer exit codes — independent.

### Within a story

Tests first (must FAIL) → model/builder → shared writers → emitter → wiring. Commit per task.

### Parallel opportunities

- Setup: T003, T004 in parallel.
- Foundational: T006, T007, T010 in parallel (T008→T009→T011 sequential on the model).
- US1: T012/T013 (tests) parallel; T015/T016 (writers) parallel; T017 after.
- US2: T019/T020/T021 (tests) parallel; T023 parallel with T022.
- US4: T031/T032 (tests) parallel; T033/T034 (emitters) parallel.
- Cross-story: once Foundational is done, US3 and US4 can run alongside US2 (disjoint files).

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel):
Task: "Unit tests for SkillModelBuilder in tests/Api2Skill.Tests/Parsing/SkillModelBuilderTests.cs"
Task: "Golden test for .cs emitter in tests/Api2Skill.Tests/Emit/CsEmitterGoldenTests.cs"
# Then writers (parallel):
Task: "SkillMdWriter in src/Api2Skill/Emit/SkillMdWriter.cs"
Task: "ReferenceWriter in src/Api2Skill/Emit/ReferenceWriter.cs"
```

---

## Implementation Strategy

### MVP first (US1 + US2)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. US1 (file→.cs skill) → **validate AC-1/AC-3** →
4. US2 (auth + secrets) → **validate AC-2/AC-5**. That is a shippable MVP: a working, authenticated
skill from a file.

### Incremental delivery

US3 (URL/stdin + insecure) → US4 (fsx/csx) → US5 (filters + error contract) → Polish. Each adds
value without breaking prior stories; validate the mapped acceptance criteria at each checkpoint.

---

## Notes

- [P] = different files, no incomplete-task dependency. [US*] traces a task to its story.
- Tests are written first and must fail before implementation (constitution test-first).
- `no-partial-output` (T039) is a hard requirement (FR-010) — generate into a temp dir, atomically
  move on success.
- Security-sensitive tasks (T023/T024/T025/T029/T030) feed the Phase 8.5 security review.
- Total: **44 tasks** across Setup(4) · Foundational(7) · US1(7) · US2(8) · US3(4) · US4(5) · US5(5) · Polish(4).
