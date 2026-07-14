# Feature Specification: Chat-authored endpoint examples

**Feature Branch**: `010-chat-endpoint-examples` (spec drafted alongside 009; implement after 009 lands or on a dedicated branch)

**Created**: 2026-07-14

**Updated**: 2026-07-14

**Status**: Planned — see [plan.md](./plan.md); next `/speckit.tasks`

**Input**: Allow humans and chat agents to add **request/response examples** for endpoints into a generated skill so LLMs use real payloads when calling/testing APIs — not invented JSON.

## Problem

Schema-derived placeholders (`"string"`, `0`) are weak for real calls. Users and agents need to capture working (or intentional) payloads as **separate files**, linked from `reference/<tag>.md`, keep them across regenerate, reuse them later for testing, and **never silently mutate** examples or contracts when a call fails — the human approves any change.

## Grill decisions (locked)

| Topic | Decision |
|-------|----------|
| Storage | Separate files; **references** from `reference/<tag>.md` (not sole storage inlined; not OpenAPI-only) |
| Layout | `examples/<operationId>/<name>/request.json` and/or `response.json` (e.g. `examples/addPet/happy/request.json`) |
| Authorship | **C** — SKILL.md chat instructions **and** `api2skill example …` CLI (same files + link patching) |
| Default | **No examples** until the user/agent adds them |
| Cardinality | **Multiple named** examples per operation allowed |
| Regenerate / update | **A** — always **preserve** `examples/`; regenerate tag MD but **re-attach** links to surviving example files |
| On failed execution using an example | Agent MUST **ask the user** that the example should be updated; MAY **propose** a contract/example change; MUST **not** apply changes without **explicit human approval** |

## User Scenarios & Testing

### User Story 1 - Add examples via chat or CLI (Priority: P1)

User or agent adds one or more named request/response example files for an operation; tag MD gains links.

**Why this priority**: Enables non-guessed payloads.

**Independent Test**: After add, files exist under `examples/<op>/<name>/` and `reference/<tag>.md` links them.

**Acceptance Scenarios**:

1. **Given** a skill with no examples, **When** `api2skill example add …` (or chat following SKILL.md) adds `addPet/happy/request.json`, **Then** the file exists and tag MD links to it.
2. **Given** an operation with one example, **When** a second named example is added, **Then** both remain listed in tag MD.

### User Story 2 - Agent prefers examples when calling/testing (Priority: P1)

Agent uses an authored example body when invoking the dispatcher for that operation.

**Independent Test**: SKILL.md / reference state that authored examples are preferred over inventing JSON.

**Acceptance Scenarios**:

1. **Given** `examples/addPet/happy/request.json`, **When** the agent is told to call `addPet`, **Then** guidance directs it to use that example (or ask which named example if several).

### User Story 3 - Preserve across generate/update (Priority: P1)

`--force` regenerate / `update` keeps `examples/` and restores links in regenerated tag MD.

**Independent Test**: Add example → regenerate with `--force` → examples directory intact and links present again.

### User Story 4 - Failed call → ask + propose, never auto-apply (Priority: P1)

When an agent uses an example and the endpoint fails, it asks the user to update; may propose example and/or contract edits; waits for approval.

**Independent Test**: SKILL.md documents this failure protocol; no tool auto-overwrites examples without a confirmed human decision.

**Acceptance Scenarios**:

1. **Given** a failed call that used `happy/request.json`, **When** the agent reacts, **Then** it asks whether to update the example (and optionally proposes a patch), **and** does not write until the user approves.
2. **Given** the agent suspects the OpenAPI/contract is wrong, **When** it proposes a contract change, **Then** it presents the proposal for approval and does not apply it unilaterally.

### User Story 5 - List / remove examples (Priority: P2)

CLI (and chat instructions) support listing and removing named examples and cleaning tag MD links.

## Edge Cases

- Unknown `operationId` on add → clear error
- Stale example (op removed after update) → preserve file; warn or orphan note in sync (plan)
- Secrets in example payloads → document never commit secrets; no auto-redaction in v1 beyond guidance
- Schema mismatch → still allow store (examples may be intentional); failure path uses ask/propose
- Multiple examples → agent asks which name to use if ambiguous

## Requirements

- **FR-001**: Examples MUST be keyed by `operationId` (plus optional `name` slug).
- **FR-002**: Files MUST live at `examples/<operationId>/<name>/request.json` and/or `response.json`; `reference/<tag>.md` MUST link them (relative paths).
- **FR-003**: `generate --force` / `update` MUST **preserve** `examples/` and MUST **re-link** surviving examples into regenerated tag markdown.
- **FR-004**: MUST support authorship via chat (SKILL.md instructions) **and** `api2skill example` CLI.
- **FR-005**: Skills MUST ship with **zero** examples until authored; multiple names allowed.
- **FR-006**: SKILL.md MUST instruct agents to prefer authored examples when calling/testing endpoints.
- **FR-007**: On execution failure after using an example, SKILL.md (and any agent-facing docs) MUST require: **ask the user** to update; agent MAY **propose** example and/or contract changes; agent MUST **not** apply those changes without **explicit human approval**.
- **FR-008**: CLI MUST NOT silently overwrite examples without an explicit flag (e.g. `--force`) documented in plan.

## Success Criteria

- **SC-001**: User can add multiple named request/response examples and see links in tag MD.
- **SC-002**: Regenerating the skill does not destroy `examples/`; links are restored.
- **SC-003**: Skill docs encode fail → ask → propose → await approval (no silent mutation).

## Grill status

All blocking grill questions answered. Residual (exact CLI verbs, orphan cleanup UX) deferred to `/speckit.plan`.

| # | Topic | Decision |
|---|-------|----------|
| 1 | Storage | Separate files + tag MD references |
| 2 | Layout | `examples/<opId>/<name>/request\|response.json` |
| 3 | Via chat | SKILL.md + CLI |
| 4 | Cardinality | None by default; multiple named |
| 5 | Preserve | Always keep `examples/` + re-link |
| 6 | Failures | Ask user; propose OK; **never apply without approval** |
