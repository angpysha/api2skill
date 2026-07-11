# Tasks: Explicit Auth Configuration

**Input**: Design documents from `specs/002-explicit-auth-config/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included — the project constitution ("Test-first is the standing default") applies and
this spec does not opt out. Tests are written before their corresponding implementation task.

**Organization**: Grouped by user story (spec.md priorities P1–P3) so each is independently
implementable, testable, and shippable as an increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task in this batch)
- **[Story]**: US1–US4, matching spec.md
- All auth-engine implementation tasks touch the same two files
  (`AuthEngine.Cs.cs`/`AuthEngine.Fsx.cs`) within a story and are therefore **sequential**, not
  `[P]`, even across stories that edit them.

---

## Phase 1: Setup

**Purpose**: Scaffolding for the new auth-config domain. No new NuGet packages (generator gains
only a `System.Text.Json` source-gen context; generated dispatcher stays BCL-only — Constitution
II).

- [x] T001 Create `src/Api2Skill/Auth/` directory for the auth-config domain (plan.md Project Structure)
- [x] T002 [P] Create `tests/Api2Skill.Tests/Auth/` directory for auth-config unit tests

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Config domain, model additions, CLI plumbing, and the emitters' fixed auth-engine
*skeleton* (dispatch loop + secret resolution + request wiring) that every user story's type
handlers plug into.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T003 [P] Define `AuthConfig` root record (`Profiles` list) in `src/Api2Skill/Auth/AuthConfig.cs` (data-model.md §1)
- [x] T004 [P] Define `AuthType` enum, `AuthProfile` record, `Attachment`/`AttachScope`, and per-type settings records (`BearerSettings`, `BasicSettings`, `CustomSettings`, `HeaderEntry`, `ScriptSettings`, `OAuthSettings`) in `src/Api2Skill/Auth/AuthProfile.cs` (data-model.md §1)
- [x] T005 Add a `JsonSerializerContext` source-gen context for the auth-config types in `src/Api2Skill/Auth/AuthConfigJsonContext.cs` (research.md R9 — AOT-safe; depends on T003, T004)
- [x] T006 [P] Implement `EntraPreset` tenant → authUrl/tokenUrl/`offline_access` expansion in `src/Api2Skill/Auth/EntraPreset.cs` (contracts/auth-config.md)
- [x] T007 Implement `AuthConfigLoader`: parse `auth.json`, validate (unique names, known `type`, type-block match, oauth2 required fields per grant, apply Entra preset) in `src/Api2Skill/Auth/AuthConfigLoader.cs` (contracts/auth-config.md validation rules; depends on T003–T006)
- [x] T008 Implement `--auth <type>` shorthand scaffolding (`bearer`/`basic`/`custom` only; `oauth2`/`entra` → usage error) in `src/Api2Skill/Auth/AuthConfigLoader.cs` (contracts/cli.md; depends on T007)
- [x] T009 [P] Implement `SecretRefScanner`: collect distinct `{secret:NAME}` references across profiles in `src/Api2Skill/Auth/SecretRefScanner.cs`
- [x] T010 Implement `AttachmentResolver`: per-operation applicable profile list (global ∪ matching tags), unused-tag warning (FR-021), duplicate-header hard error (FR-021a) in `src/Api2Skill/Auth/AttachmentResolver.cs` (data-model.md §2 collision invariant; depends on T003, T004)
- [x] T011 [P] Extend `OperationModel` with `AuthProfileNames` in `src/Api2Skill/Model/OperationModel.cs`
- [x] T012 [P] Extend `SkillModel` with `AuthConfig?` in `src/Api2Skill/Model/SkillModel.cs`
- [x] T013 Wire `AttachmentResolver` output into `SkillModelBuilder.Build` — attach `AuthProfileNames` per operation, explicit profiles override spec-derived `SecuritySchemeIds` for covered operations (FR-006) in `src/Api2Skill/Model/SkillModelBuilder.cs` (depends on T010, T011, T012)
- [x] T014 Add `--auth-config`, `--auth`, `--login` CLI options and `AuthConfigPath`/`AuthShorthand`/`Login` fields in `src/Api2Skill/Cli/GenerateOptions.cs` and `src/Api2Skill/Cli/GenerateCommand.cs` (contracts/cli.md; depends on T007, T008)
- [x] T015 Add exit code `5` (`AuthConfigError`) and wire it to `AuthConfigLoader` failures; missing `--auth-config` file → exit `4`; `--auth oauth2`/`--auth entra` → exit `2` in `src/Api2Skill/Cli/GenerateCommand.cs` (contracts/cli.md; depends on T014)
- [x] T016 Extend `SecretsScaffold` to add auth-derived secret keys (from `SecretRefScanner`) in `src/Api2Skill/Emit/SecretsScaffold.cs` (depends on T009)
- [x] T017 Extend `SkillWriter`: copy `auth.json` verbatim into staging; on `--force`, preserve an existing `auth.json` unless a new one is supplied this run, always preserve `.auth-cache.json`(+`.lock`); add `.auth-cache.json` to the generated `.gitignore` in `src/Api2Skill/Output/SkillWriter.cs` (contracts/cli.md `--force` policy; depends on T012)
- [x] T018 [P] Create the fixed C# auth-engine skeleton — secret-reference resolution, per-operation profile dispatch loop, header/query collection wired into request building — in `src/Api2Skill/Emit/AuthEngine.Cs.cs` (shared text for `.cs` and `.csx`)
- [x] T019 [P] Create the fixed F# auth-engine skeleton with equivalent behavior in `src/Api2Skill/Emit/AuthEngine.Fsx.cs`
- [x] T020 Wire `CsFileEmitter` to emit the `AuthEngine.Cs` text plus each operation's `AuthProfileNames` table, replacing the always-spec-derived `ApplyAuthAsync` path with "explicit profiles first, spec-derived fallback for uncovered operations" in `src/Api2Skill/Emit/CsFileEmitter.cs` (depends on T013, T018)
- [x] T021 [P] Wire `CsxEmitter` to reuse the `AuthEngine.Cs` text in `src/Api2Skill/Emit/CsxEmitter.cs` (depends on T018)
- [x] T022 [P] Wire `FsxEmitter` to emit the `AuthEngine.Fsx` text in `src/Api2Skill/Emit/FsxEmitter.cs` (depends on T013, T019)
- [x] T023 [P] Add a compact per-profile auth-setup section to the `SKILL.md` writer in `src/Api2Skill/Emit/SkillMdWriter.cs` (FR-022, Constitution V)
- [x] T024 [P] Unit tests: `AuthConfigLoader` parse/validate — valid file, malformed JSON, unknown `type`, duplicate profile name, missing oauth2 required fields — in `tests/Api2Skill.Tests/Auth/AuthConfigLoaderTests.cs`
- [x] T025 [P] Unit tests: `AttachmentResolver` — global/tag resolution, unused-tag warning, duplicate-header hard error — in `tests/Api2Skill.Tests/Auth/AttachmentResolverTests.cs`
- [x] T026 [P] Unit tests: `EntraPreset` — tenant fill, `offline_access` added, explicit field overrides preset — in `tests/Api2Skill.Tests/Auth/EntraPresetTests.cs`
- [x] T027 [P] Unit tests: `SecretRefScanner` and resulting `secrets.example.json` keys in `tests/Api2Skill.Tests/Auth/SecretRefScannerTests.cs`
- [x] T028 [P] CLI exit-code tests: `--auth-config` missing file (4), invalid `auth.json` (5), `--auth oauth2` usage error (2) in `tests/Api2Skill.Tests/Cli/ExitCodeTests.cs`

**Checkpoint**: Foundation ready — config domain, model, CLI, writer, and engine skeletons exist.
User story implementation (type handlers) can now begin.

---

## Phase 3: User Story 1 - Declare a simple explicit credential (Priority: P1) 🎯 MVP

**Goal**: `bearer`, `basic`, and `custom` profile types apply at call time and override
spec-derived auth for the operations they cover.

**Independent Test**: `api2skill generate ./petstore.json --auth bearer`, fill the scaffolded
secret, run an operation, observe `Authorization: Bearer <token>` even though the spec declared a
different/no scheme (quickstart.md Scenario A).

### Tests for User Story 1 ⚠️

- [x] T029 [P] [US1] Golden test: `cs` emitter auth-engine snapshot covering bearer/basic/custom in `tests/Api2Skill.Tests/Emit/CsEmitterAuthGoldenTests.cs`
- [x] T030 [P] [US1] Golden test: `fsx`/`csx` emitter parity for bearer/basic/custom in `tests/Api2Skill.Tests/Emit/FsxCsxAuthGoldenTests.cs`
- [x] T031 [P] [US1] Integration test: bearer prefix rule — added once when absent, unchanged when already present (any casing) — against a stub API in `tests/Api2Skill.Tests/Integration/DispatcherBearerAuthTests.cs`
- [x] T032 [P] [US1] Integration test: basic auth `Authorization: Basic base64(user:pass)` against a stub API in `tests/Api2Skill.Tests/Integration/DispatcherBasicAuthTests.cs`
- [x] T033 [P] [US1] Integration test: custom profile sends multiple distinct headers (`Authorization` + `ApiKey`) against a stub API in `tests/Api2Skill.Tests/Integration/DispatcherCustomAuthTests.cs`
- [x] T034 [P] [US1] Integration test: an explicit profile overrides the spec-derived scheme for the operations it covers; uncovered operations keep spec-derived behavior in `tests/Api2Skill.Tests/Integration/DispatcherAuthOverrideTests.cs`

### Implementation for User Story 1

- [x] T035 [US1] Implement the `bearer` type handler (prefix rule) in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T018, T019)
- [x] T036 [US1] Implement the `basic` type handler (Base64) in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T035)
- [x] T037 [US1] Implement the `custom` type handler (ordered multi-header) in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T036)
- [x] T038 [US1] Wire secret-reference resolution and the "missing secret fails the call, names the key" behavior in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (contracts/dispatcher-auth.md step 3; depends on T037)
- [x] T039 [US1] Confirm override semantics end-to-end: operations covered by an explicit profile skip the spec-derived auth path in `src/Api2Skill/Emit/CsFileEmitter.cs`, `CsxEmitter.cs`, `FsxEmitter.cs` (depends on T020–T022, T038)
- [x] T040 [US1] Update `SKILL.md` auth-setup docs for bearer/basic/custom in `src/Api2Skill/Emit/SkillMdWriter.cs` (depends on T023)
- [x] T041 [US1] Run quickstart.md Scenarios A, B, F for bearer/basic/custom across all three emitters — manual validation

**Checkpoint**: User Story 1 is fully functional and independently testable (MVP).

---

## Phase 4: User Story 2 - One credential set for some operations, another for others (Priority: P2)

**Goal**: Profiles attach globally or to specific tags; multiple attached profiles all apply;
same-header collisions fail generation; an attachment to an unused tag warns but succeeds.

**Independent Test**: two profiles — one global, one tag-scoped — generate, confirm tagged
operations carry both while others carry only the global one (quickstart.md Scenario C).

### Tests for User Story 2 ⚠️

- [ ] T042 [P] [US2] Integration test: a tag-scoped profile applies only to operations in that tag; a global profile applies to all in `tests/Api2Skill.Tests/Integration/DispatcherAuthTagScopeTests.cs`
- [ ] T043 [P] [US2] Unit test: two attached profiles setting the same header on one operation → generation fails with exit `5`, naming the header and both profiles, in `tests/Api2Skill.Tests/Cli/ExitCodeTests.cs` (extend)
- [ ] T044 [P] [US2] Unit test: an attachment to a tag with no operations produces a warning and generation still succeeds in `tests/Api2Skill.Tests/Auth/AttachmentResolverTests.cs` (extend)

### Implementation for User Story 2

- [ ] T045 [US2] Complete `AttachmentResolver` stacking semantics for three or more profiles applying to one operation (depends on T010; driven to green by T042–T044)
- [ ] T046 [US2] Surface collision errors and unused-tag warnings through `GenerateCommand` console output in `src/Api2Skill/Cli/GenerateCommand.cs` (depends on T015, T045)
- [ ] T047 [US2] Run quickstart.md Scenario C — manual validation

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Interactive OAuth login via browser (Priority: P2)

**Goal**: `oauth2` profiles support `authorization_code` + PKCE + `state` (public-client capable)
and `client_credentials`; an `entra` preset; a `login <profile>` subcommand that opens a browser
and listens on a loopback callback; a locked, refreshing token cache.

**Independent Test**: generate with an `oauth2`/`entra` profile, run `login`, complete browser
sign-in, then invoke an operation using the acquired token with no further browser prompt
(quickstart.md Scenario D).

### Tests for User Story 3 ⚠️

- [ ] T048 [P] [US3] Unit test: oauth2 grant validation — `authorization_code` requires `authUrl`+`callbackUrl`, `client_credentials` requires `tokenUrl`, `login` on a `client_credentials` profile is flagged not-applicable — in `tests/Api2Skill.Tests/Auth/AuthConfigLoaderTests.cs` (extend)
- [ ] T049 [P] [US3] Integration test: stub IdP + generated dispatcher `login <profile>` completes a full PKCE + `state` authorization-code exchange and writes `.auth-cache.json` in `tests/Api2Skill.Tests/Integration/DispatcherOAuthLoginTests.cs`
- [ ] T050 [P] [US3] Integration test: a `state` mismatch on the callback is rejected and no token is stored in `tests/Api2Skill.Tests/Integration/DispatcherOAuthLoginTests.cs` (extend)
- [ ] T051 [P] [US3] Integration test: a valid cached token is used without a browser or network call; an expired token with a refresh token refreshes silently; an expired token with no usable refresh token fails the call with a re-login message and launches no browser in `tests/Api2Skill.Tests/Integration/DispatcherOAuthTokenLifecycleTests.cs`
- [ ] T052 [P] [US3] Integration test: `client_credentials` tokens are fetched on demand at call time and cached in `tests/Api2Skill.Tests/Integration/DispatcherOAuthClientCredentialsTests.cs`
- [ ] T053 [P] [US3] Integration test: concurrent operation calls racing an expired token cause exactly one refresh and no cache corruption (file lock + post-lock re-check) in `tests/Api2Skill.Tests/Integration/DispatcherOAuthConcurrencyTests.cs`
- [ ] T054 [P] [US3] Integration test: headless login (no launchable browser) prints the authorize URL and still completes on redirect in `tests/Api2Skill.Tests/Integration/DispatcherOAuthLoginTests.cs` (extend)
- [ ] T055 [P] [US3] Integration test: an occupied callback port reports the conflict with guidance to change `callbackUrl` — including the case where **two configured interactive profiles share the same callback port** (spec edge case) — in `tests/Api2Skill.Tests/Integration/DispatcherOAuthLoginTests.cs` (extend)

### Implementation for User Story 3

- [ ] T056 [US3] Implement PKCE (`code_verifier`/`code_challenge` S256) and `state` generation helpers in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T018, T019)
- [ ] T057 [US3] Implement the `HttpListener`-based loopback callback plus browser launch, headless URL-print fallback, and port-conflict reporting in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T056)
- [ ] T058 [US3] Implement authorize-request construction (URL, `authorizeRequest` custom headers/body, `state`, PKCE challenge) in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T057)
- [ ] T059 [US3] Implement token-exchange and refresh POST requests (`clientAuth` body/basic, `tokenRequest` custom headers/body) in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T058)
- [ ] T060 [US3] Implement `.auth-cache.json` read/write with `0600` permission (POSIX) and atomic temp-file-then-move in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T059)
- [ ] T061 [US3] Implement the inter-process file lock (`.auth-cache.json.lock`, `FileShare.None`, bounded retry) with post-lock validity re-check in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (FR-019a; depends on T060)
- [ ] T062 [US3] Implement the `login <profile>` subcommand dispatch, including the "not applicable" message for `client_credentials` profiles and unknown-profile listing, in `src/Api2Skill/Emit/CsFileEmitter.cs`, `CsxEmitter.cs`, `FsxEmitter.cs` (depends on T057)
- [ ] T063 [US3] Implement per-call oauth2 token resolution — valid/refresh/fail-with-relogin-message, never launching a browser — wired into the auth dispatch loop in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T061)
- [ ] T064 [US3] Add the opt-in generator `--login` flag: after a successful write, run login for each `authorization_code` profile in `src/Api2Skill/Cli/GenerateCommand.cs` (depends on T014, T062)
- [ ] T065 [US3] Update `SKILL.md` auth-setup docs for `oauth2` + the `entra` preset + login instructions in `src/Api2Skill/Emit/SkillMdWriter.cs` (depends on T023)
- [ ] T066 [US3] Run quickstart.md Scenario D — manual validation against a stub or real Entra app registration

**Checkpoint**: User Stories 1, 2, and 3 all work independently.

---

## Phase 6: User Story 4 - Token obtained from an external command (Priority: P3)

**Goal**: `script` profiles run a configured command fresh on each call and use its trimmed stdout
as a header value.

**Independent Test**: a `script` profile whose command echoes a token; invoke an operation;
confirm the request carries the trimmed stdout as the configured header (quickstart.md Scenario
E).

### Tests for User Story 4 ⚠️

- [ ] T067 [P] [US4] Integration test: script command stdout becomes the configured header value (default `Authorization`) in `tests/Api2Skill.Tests/Integration/DispatcherScriptAuthTests.cs`
- [ ] T068 [P] [US4] Integration test: `bearerPrefix` prepends `Bearer ` exactly once when absent in `tests/Api2Skill.Tests/Integration/DispatcherScriptAuthTests.cs` (extend)
- [ ] T069 [P] [US4] Integration test: a non-zero command exit fails the call and surfaces stderr in `tests/Api2Skill.Tests/Integration/DispatcherScriptAuthTests.cs` (extend)

### Implementation for User Story 4

- [ ] T070 [US4] Implement the `script` type handler (process exec, trim stdout, `bearerPrefix`, non-zero-exit failure surfacing stderr) in `AuthEngine.Cs.cs` and `AuthEngine.Fsx.cs` (depends on T018, T019)
- [ ] T071 [US4] Update `SKILL.md` docs noting `script` executes a user-provided local command (FR-022) in `src/Api2Skill/Emit/SkillMdWriter.cs` (depends on T023)
- [ ] T072 [US4] Run quickstart.md Scenario E — manual validation

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T073 [P] Update `README.md` with `auth.json`/`--auth`/`--auth-config`/`--login` usage examples
- [ ] T074 [P] Author ADR-0004 (runtime-read `auth.json` + API-independent engine), ADR-0005 (loopback + PKCE + `state`), ADR-0006 (file-locked token cache), linked from `plan.md`
- [ ] T075 Cross-emitter parity sweep: run quickstart.md Scenario F across `cs`/`fsx`/`csx` for all five auth types
- [ ] T076 Run the full test suite (`dotnet-unit-tests.ps1`) and confirm green
- [ ] T077 [P] Review `AGENTS.md`/pipeline docs for mention of `auth.json` as a new generated-artifact type, update if needed
- [x] T078 [P] Add a negative-assertion test verifying no configured secret value appears in any **committed** generated artifact (`auth.json`, `SKILL.md`, `reference/*.md`, `secrets.example.json`, golden-fixture files) across every auth type in `tests/Api2Skill.Tests/Auth/NoSecretLeakageTests.cs` (closes analyze finding G1 — Constitution IV / FR-002 / FR-025 / SC-006)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup — **BLOCKS all user stories**.
- **User Stories (Phase 3–6)**: all depend on Foundational completion.
  - US1 (P1) has no dependency on US2–US4.
  - US2 (P2) reuses US1's type handlers to demonstrate stacking but only requires the
    Foundational `AttachmentResolver` to be independently testable (it can stack two `bearer`/
    `custom` profiles without US3/US4 existing).
  - US3 (P2) and US4 (P3) depend only on Foundational; independent of each other and of US1/US2.
- **Polish (Phase 7)**: depends on all delivered user stories.

### Within Each User Story

- Tests are written first and must fail before implementation (constitution test-first default).
- All `AuthEngine.Cs.cs`/`AuthEngine.Fsx.cs` implementation tasks within and across stories are
  **sequential** (same two files) — respect the numeric order given.
- Emitter-wiring tasks (`CsFileEmitter`/`CsxEmitter`/`FsxEmitter`) follow their story's engine
  tasks.

### Parallel Opportunities

- T003, T004, T006, T009, T011, T012 (Foundational, distinct files) — parallel.
- T024–T028 (Foundational unit/CLI tests, distinct files) — parallel.
- T029–T034 (US1 tests, distinct files) — parallel.
- T042–T044 (US2 tests, distinct files) — parallel.
- T048–T055 (US3 tests, distinct files except T049/T050/T054/T055 which extend the same file
  sequentially within that group) — the four distinct test files (T049/50/54/55 combined,
  T051, T052, T053) can run in parallel across files.
- T067–T069 (US4 tests, same file, sequential extensions) — not parallel with each other.
- Once Foundational is done, **US1, US3, and US4 implementation can proceed in parallel** by
  different developers (all edit `AuthEngine.*` though, so true file-level parallelism requires
  coordinating merge order — see note below).

**Note on `AuthEngine.*` contention**: because every type handler lives in the same two files,
parallel *development* across stories is possible but must be **merged and tested sequentially**
(T035→T036→T037→T038→…→T056→…→T070 is the safe integration order). Treat the `[US#]`
implementation tasks as a single ordered queue against those two files even when stories are
staffed in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests (distinct files):
Task: "Golden test: cs emitter auth-engine snapshot in tests/Api2Skill.Tests/Emit/CsEmitterAuthGoldenTests.cs"
Task: "Golden test: fsx/csx emitter parity in tests/Api2Skill.Tests/Emit/FsxCsxAuthGoldenTests.cs"
Task: "Integration test: bearer prefix rule in tests/Api2Skill.Tests/Integration/DispatcherBearerAuthTests.cs"
Task: "Integration test: basic auth in tests/Api2Skill.Tests/Integration/DispatcherBasicAuthTests.cs"
Task: "Integration test: custom multi-header in tests/Api2Skill.Tests/Integration/DispatcherCustomAuthTests.cs"
Task: "Integration test: override semantics in tests/Api2Skill.Tests/Integration/DispatcherAuthOverrideTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (Setup) + Phase 2 (Foundational — CRITICAL, blocks everything).
2. Complete Phase 3 (US1: bearer/basic/custom + override).
3. **STOP and VALIDATE**: run quickstart.md Scenarios A, B, F independently.
4. This alone satisfies SC-001 and SC-006 and is shippable.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 → validate → MVP.
3. US2 → validate (stacking, collisions, tag scoping) → ship.
4. US3 → validate (interactive OAuth, Entra, refresh, concurrency) → ship — the marquee capability.
5. US4 → validate (script auth) → ship.
6. Polish.

### Parallel Team Strategy

After Foundational:
- Developer A: US1 (bearer/basic/custom).
- Developer B: US4 (script) — smallest, fully independent.
- Developer C: US3 (OAuth) — largest, start early given its size.
- US2 follows US1 closely (same developer, since it exercises US1's types) or is picked up by
  whoever finishes first, respecting the `AuthEngine.*` sequential-merge note above.
