# Tasks: App-owned OAuth redirect capture (multi-mode)

**Input**: Design documents from `/specs/009-oauth-https-callback/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Included — constitution **test-first** default (plan Constitution Check).

**Beads (`br`)**: Parent epic and phase/story children created with this file. See
[Beads mapping](#beads-mapping-br) below. Prefer claiming a `br` child for the current
phase, then work checklist items `Tnnn` inside it.

**Organization**: By user story (US1–US4) after shared foundation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: `[US1]`…`[US4]` for story phases only
- Include exact file paths in descriptions

---

## Beads mapping (`br`)

| Phase / story | `br` id | Title |
|---------------|---------|-------|
| Epic (parent) | `api2skill-009-oauth-capture-ol0` | 009: OAuth multi-mode redirect capture |
| Phase 1–2 Setup + Foundation | `api2skill-009-oauth-capture-ol0.1` | 009-P12: OAuth capture foundation (T001–T011) |
| Phase 3 US1 HTTP | `api2skill-009-oauth-capture-ol0.2` | 009-US1: HTTP loopback capture (T012–T016) |
| Phase 4 US2 HTTPS | `api2skill-009-oauth-capture-ol0.3` | 009-US2: HTTPS loopback + cert (T017–T022) |
| Phase 5 US3 Scheme | `api2skill-009-oauth-capture-ol0.4` | 009-US3: Custom scheme + register-protocol (T023–T029) |
| Phase 6 US4 Hosted | `api2skill-009-oauth-capture-ol0.5` | 009-US4: Hosted OAuth relay (T030–T035) |
| Phase 7 Handoff | `api2skill-009-oauth-capture-ol0.6` | 009-HO: Emitter login + `login --skill` (T036–T042) |
| Phase 8 Polish | `api2skill-009-oauth-capture-ol0.7` | 009-PL: Docs, version 0.6.0, quickstart (T043–T047) |

Ready work now: `br ready` → claim `.1` (foundation) first. Story children are blocked on `.1`; handoff blocked on `.2`; polish blocked on `.6`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold namespaces and placeholders for the capture engine

**Beads**: `api2skill-009-oauth-capture-ol0.1` (shared with Phase 2)

- [x] T001 Create `src/Api2Skill/OAuth/` directory and empty namespace files listed in plan.md (`CaptureMode.cs`, `CaptureOptions.cs`, `CaptureResult.cs`, `IRedirectCapture.cs` stubs)
- [x] T002 [P] Add colored console helper in `src/Api2Skill/Cli/ConsoleColorWriter.cs` (honor `NO_COLOR`)
- [x] T003 [P] Add stub folder `hosting/oauth-relay/README.md` describing deploy intent per `contracts/hosted-relay.md`
- [x] T004 [P] Document exit codes 6/7 in a short comment block near new CLI (or `contracts/cli.md` already — verify linked from `wiki/Authentication.md` stub section heading only)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared models, mode routing, cert material, `oauth-capture` command skeleton — **blocks all stories**

**⚠️ CRITICAL**: No user story work until this phase completes

**Beads**: `api2skill-009-oauth-capture-ol0.1`

### Tests (write first — must FAIL)

- [x] T005 [P] Add unit tests for capture mode inference in `tests/Api2Skill.Tests/OAuth/CaptureModeInferenceTests.cs`
- [x] T006 [P] Add unit tests for `CaptureResult` JSON round-trip in `tests/Api2Skill.Tests/OAuth/CaptureResultJsonTests.cs`

### Implementation

- [x] T007 Implement `CaptureMode`, `CaptureOptions`, `CaptureResult` in `src/Api2Skill/OAuth/` per `data-model.md`
- [x] T008 Implement mode inference (`auto`) from callback URL in `src/Api2Skill/OAuth/CaptureModeResolver.cs`
- [x] T009 Implement `CertMaterial` load (PFX + PEM/key) and TTY password prompt in `src/Api2Skill/OAuth/CertMaterial.cs` using `ConsoleColorWriter`
- [x] T010 Implement `OAuthCaptureCommand` skeleton (parse flags per `contracts/cli.md`, return exit 2 on validation) in `src/Api2Skill/Cli/OAuthCaptureCommand.cs`
- [x] T011 Register `oauth-capture` on root command in `src/Api2Skill/Program.cs`

**Checkpoint**: `dotnet test` compiles; mode/JSON tests green; `oauth-capture --help` works; HTTPS without cert on non-TTY fails validation (may soft-stub until US2)

---

## Phase 3: User Story 1 — HTTP callback (Priority: P1) 🎯 MVP

**Goal**: App-owned HTTP loopback capture works via `api2skill oauth-capture`

**Independent Test**: Quickstart Scenario A — redirect to `http://127.0.0.1:…/callback` yields JSON `ok:true`, `mode:HttpLoopback`

**Beads**: `api2skill-009-oauth-capture-ol0.2`

### Tests (FAIL first)

- [x] T012 [P] [US1] Add HTTP loopback capture tests in `tests/Api2Skill.Tests/OAuth/LoopbackHttpCaptureTests.cs`
- [x] T013 [P] [US1] Add CLI integration test invoking `oauth-capture` HTTP path in `tests/Api2Skill.Tests/Cli/OAuthCaptureHttpCliTests.cs`

### Implementation

- [x] T014 [US1] Implement `LoopbackHttpCapture` in `src/Api2Skill/OAuth/LoopbackHttpCapture.cs` (dual localhost/127.0.0.1 prefixes; ignore favicon; start listen before return)
- [x] T015 [US1] Wire HTTP mode into `OAuthCaptureCommand` in `src/Api2Skill/Cli/OAuthCaptureCommand.cs` (stdout JSON per `contracts/oauth-capture.md`)
- [x] T016 [US1] Map capture timeout / IdP `error=` to exit codes 6/7 in `src/Api2Skill/Cli/OAuthCaptureCommand.cs`

**Checkpoint**: Scenario A passes; MVP capturable over HTTP

---

## Phase 4: User Story 2 — HTTPS callback (Priority: P1)

**Goal**: Local HTTPS loopback with `--cert` / PEM and colored interactive ask

**Independent Test**: Quickstart Scenario B — HTTPS callback + PFX; non-TTY without cert → exit 2, no hang

**Beads**: `api2skill-009-oauth-capture-ol0.3`

### Tests (FAIL first)

- [x] T017 [P] [US2] Add HTTPS cert validation / non-TTY fail tests in `tests/Api2Skill.Tests/OAuth/CertMaterialTests.cs`
- [x] T018 [P] [US2] Add HTTPS loopback capture tests (test cert) in `tests/Api2Skill.Tests/OAuth/LoopbackHttpsCaptureTests.cs`

### Implementation

- [x] T019 [US2] Add app-only HTTPS hosting dependency if needed in `src/Api2Skill/Api2Skill.csproj` (Kestrel/minimal hosting — **not** emitted to skills)
- [x] T020 [US2] Implement `LoopbackHttpsCapture` in `src/Api2Skill/OAuth/LoopbackHttpsCapture.cs`
- [x] T021 [US2] Wire `--cert`, `--cert-password`, `--cert-pem`, `--cert-key` + colored prompt into `OAuthCaptureCommand` in `src/Api2Skill/Cli/OAuthCaptureCommand.cs`
- [x] T022 [US2] Ensure non-TTY missing cert fails fast (exit 2) with red stderr in `src/Api2Skill/Cli/OAuthCaptureCommand.cs`

**Checkpoint**: Scenario B passes

---

## Phase 5: User Story 3 — Our custom scheme (Priority: P1)

**Goal**: Explicit `register-protocol` / `unregister-protocol` + `api2skill://` capture

**Independent Test**: Quickstart Scenario C — register → capture; unregistered → exit 6 + colored hint

**Beads**: `api2skill-009-oauth-capture-ol0.4`

### Tests (FAIL first)

- [x] T023 [P] [US3] Add protocol registration unit/OS-conditional tests in `tests/Api2Skill.Tests/OAuth/ProtocolRegistrationTests.cs`
- [x] T024 [P] [US3] Add custom-scheme capture tests (handoff simulation) in `tests/Api2Skill.Tests/OAuth/CustomSchemeCaptureTests.cs`

### Implementation

- [x] T025 [US3] Implement `ProtocolRegistration` for macOS/Windows/Linux in `src/Api2Skill/OAuth/ProtocolRegistration.cs`
- [x] T026 [US3] Implement `RegisterProtocolCommand` + `UnregisterProtocolCommand` in `src/Api2Skill/Cli/RegisterProtocolCommand.cs`
- [x] T027 [US3] Register both commands in `src/Api2Skill/Program.cs`
- [x] T028 [US3] Implement `CustomSchemeCapture` in `src/Api2Skill/OAuth/CustomSchemeCapture.cs` (wait for OS invocation / second-instance handoff)
- [x] T029 [US3] Wire `CustomScheme` mode in `src/Api2Skill/Cli/OAuthCaptureCommand.cs`; unregistered → colored exit 6

**Checkpoint**: Scenario C passes on at least one supported OS; others document best-effort

---

## Phase 6: User Story 4 — Hosted capture + custom address (Priority: P1)

**Goal**: Hosted relay (Postman-style) + poll client in the tool

**Independent Test**: Quickstart Scenario D against local stub / `API2SKILL_OAUTH_RELAY_BASE`

**Beads**: `api2skill-009-oauth-capture-ol0.5`

### Tests (FAIL first)

- [x] T030 [P] [US4] Add hosted session/poll client tests with in-process stub in `tests/Api2Skill.Tests/OAuth/HostedRelayCaptureTests.cs`
- [x] T031 [P] [US4] Add relay contract stub tests matching `contracts/hosted-relay.md` in `tests/Api2Skill.Tests/OAuth/HostedRelayStubTests.cs`

### Implementation

- [x] T032 [US4] Implement in-repo relay (Worker or Azure Function — pick one) under `hosting/oauth-relay/` per `contracts/hosted-relay.md` (session/callback/poll; TTL ≤5m; code/error only)
- [x] T033 [US4] Implement `HostedRelayCapture` client in `src/Api2Skill/OAuth/HostedRelayCapture.cs` (`--relay-base` / `API2SKILL_OAUTH_RELAY_BASE`)
- [x] T034 [US4] Wire `Hosted` mode + unsupported non-loopback URL errors in `src/Api2Skill/Cli/OAuthCaptureCommand.cs`
- [x] T035 [US4] Add test stub host usable without cloud deploy in `tests/Api2Skill.Tests/OAuth/TestHostedRelayServer.cs`

**Checkpoint**: Scenario D passes locally via stub

---

## Phase 7: Skill handoff + `login --skill` (FR-008 cross-cutting)

**Goal**: Generated `login` shells to `oauth-capture`; `api2skill login --skill` end-to-end

**Independent Test**: Quickstart Scenario E; HTTP skill login matches prior `.auth-cache.json` behavior

**Beads**: `api2skill-009-oauth-capture-ol0.6`

### Tests (FAIL first)

- [x] T036 [P] [US1] Extend auth golden/integration expectations for Process handoff markers in `tests/Api2Skill.Tests/Emit/AuthEngineGoldenTests.cs` (or new `OAuthCaptureHandoffTests.cs`)
- [x] T037 [P] Add `login --skill` CLI tests in `tests/Api2Skill.Tests/Cli/LoginCommandTests.cs`

### Implementation

- [x] T038 [P] Update C# file emitter login to prefer `api2skill oauth-capture` in `src/Api2Skill/Emit/CsFileEmitter.cs` per `contracts/dispatcher-login.md` (HTTP in-script fallback only)
- [x] T039 [P] Mirror handoff in `src/Api2Skill/Emit/CsxEmitter.cs`
- [x] T040 [P] Mirror handoff in `src/Api2Skill/Emit/FsxEmitter.cs`
- [x] T041 Implement `LoginCommand` (`--skill`, `--profile`, cert/relay flags) in `src/Api2Skill/Cli/LoginCommand.cs` — reuse `AuthConfigLoader`; write `.auth-cache.json`
- [x] T042 Register `login` in `src/Api2Skill/Program.cs`

**Checkpoint**: Scenarios A + E pass with tool on PATH

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Docs, versioning, full quickstart

**Beads**: `api2skill-009-oauth-capture-ol0.7`

- [x] T043 [P] Update OAuth / callback modes in `wiki/Authentication.md` (HTTP/HTTPS/scheme/hosted, cert flags, register-protocol)
- [x] T044 [P] Add compact SKILL.md auth pointers via `src/Api2Skill/Emit/SkillMdWriter.cs`
- [x] T045 Bump package version to **0.6.0** in `src/Api2Skill/Api2Skill.csproj` (+ any mirrored version files per `version-bump` rule)
- [x] T046 Run full `quickstart.md` scenarios A–E; record results in PR description
- [x] T047 Close eligible `br` children + epic when human accepts (`br close …`); keep `.beads/issues.jsonl` staged with code

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → blocks stories
- **US1 (3)** MVP — start after Phase 2
- **US2 (4)**, **US3 (5)**, **US4 (6)** — after Phase 2; can proceed in parallel after US1 if staffed (US2/3/4 independent of each other)
- **Handoff (7)** — after US1 at minimum (HTTP); ideally after US2–US4 mode wiring so emitters cover all modes
- **Polish (8)** — after desired stories + handoff

### User Story Dependencies

- **US1**: After Phase 2 only
- **US2**: After Phase 2; uses `CertMaterial` from Phase 2
- **US3**: After Phase 2
- **US4**: After Phase 2
- **Handoff**: Depends on US1 capture path; extend as other modes land

### Parallel Opportunities

- T002–T004; T005–T006; T012–T013; T017–T018; T023–T024; T030–T031; T036–T037; T038–T040; T043–T044

---

## Parallel Example: User Story 1

```bash
# After Phase 2 green:
# Parallel tests:
Task: T012 LoopbackHttpCaptureTests.cs
Task: T013 OAuthCaptureHttpCliTests.cs
# Then sequential impl: T014 → T015 → T016
```

---

## Parallel Example: After foundation (multi-story)

```bash
# Different agents/devs:
# Dev A: 009-US2 (T017–T022)
# Dev B: 009-US3 (T023–T029)
# Dev C: 009-US4 (T030–T035)
# Then 009-HO emitters in parallel (T038–T040)
```

---

## Implementation Strategy

### MVP First (US1 + foundation + minimal handoff)

1. Phase 1–2
2. Phase 3 US1
3. Phase 7 enough for HTTP (`T038` + `T041`/`T042`)
4. **STOP** — validate Scenario A (+ E HTTP)
5. Human review checkpoint

### Incremental Delivery

1. US2 HTTPS → Scenario B  
2. US3 scheme → Scenario C  
3. US4 hosted → Scenario D  
4. Finish emitters for all modes + Polish → 0.6.0

### Notes

- Capture logic stays in **app**; skills shell out (Constitution II)
- No silent protocol registration (FR-009)
- Do not commit secrets from `debug/entra-oauth/`
- Claim matching `br` issue before coding; close with proof (test names / quickstart)
