# Feature Specification: Python script emitter and language migrate

**Feature Branch**: `feature/015-python-script-emitter`

**Created**: 2026-10-30

**Status**: Draft

**Input**: User description: "Add Python as a generated API client language. Migration of an existing skill to another script kind is a separate command. OAuth token acquisition stays in the api2skill CLI. Generated dispatchers only apply a token the CLI already stored. Prefer the Python standard library; if a dependency is required, the skill installs it with pip."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a Python skill (Priority: P1)

A developer points api2skill at an OpenAPI document and asks for a Python dispatcher. The skill runs with `python3` and calls the API the same way the C# dispatcher does: operation id, parameters, and an optional JSON body.

**Why this priority**: This is the new language. Without it there is nothing to migrate to.

**Independent Test**: `api2skill generate <spec> --script py --out ./skills/demo` writes `scripts/call.py`. `python3 scripts/call.py -- <operationId>` performs the HTTP call.

**Acceptance Scenarios**:

1. **Given** a valid OpenAPI document, **When** the user runs generate with `--script py`, **Then** the skill contains `scripts/call.py`, `scripts/requirements.txt`, and a manifest whose script kind is `py`.
2. **Given** a generated Python skill and apiKey, bearer, or basic credentials in `secrets.json`, **When** the dispatcher runs an operation, **Then** it sends those credentials and does not require a third-party package.
3. **Given** `requirements.txt` lists a package, **When** `call.py` starts, **Then** it installs that file once into `scripts/.pydeps` and imports from there on later runs.

---

### User Story 2 - Switch an existing skill's language (Priority: P1)

A developer already has a generated skill and wants the dispatcher in another language without retyping generate flags or losing credentials.

**Why this priority**: The user asked for a separate migrate command, not a flag on update.

**Independent Test**: Generate with `--script cs`, then `api2skill migrate <skill> --script py`. Secrets, auth, and the token cache remain. The dispatcher file matches the new language.

**Acceptance Scenarios**:

1. **Given** a skill with a readable `.api2skill.json`, **When** the user runs `migrate --script py`, **Then** the skill is regenerated from the recorded spec sources and options, with script kind `py`, and `secrets.json`, `auth.json`, `.auth-cache.json`, and `examples/` are kept.
2. **Given** the skill is already the requested script kind, **When** the user runs migrate, **Then** the command exits successfully and does not rewrite the skill.
3. **Given** the path is not a generated skill, or `--script` is unknown, **When** the user runs migrate, **Then** the command exits with a usage error.
4. **Given** a multi-part skill, **When** the user migrates it, **Then** the whole skill uses one script kind.

---

### User Story 3 - OAuth tokens come from the CLI (Priority: P2)

An operator obtains and refreshes OAuth tokens with `api2skill login`. Generated dispatchers, including Python and the existing C# and F# scripts, only read `.auth-cache.json` and attach the access token.

**Why this priority**: Python should not reimplement the OAuth browser, PKCE, and certificate flow. The CLI already owns that flow. The same rule applies to every dispatcher so a migrated skill behaves the same.

**Independent Test**: `api2skill login --skill <dir> --profile <name>` writes `.auth-cache.json` for client-credentials and for a refreshable authorization-code session. The dispatcher sends `Authorization: Bearer` from that file and does not call the token endpoint.

**Acceptance Scenarios**:

1. **Given** a client-credentials profile and empty cache, **When** the user runs `api2skill login`, **Then** the CLI fetches a token and writes `.auth-cache.json`.
2. **Given** a cached access token that is still valid, **When** the user runs login, **Then** the CLI leaves the cache unchanged and does not open a browser.
3. **Given** an authorization-code profile whose access token is expired and whose cache has a refresh token, **When** the user runs login, **Then** the CLI refreshes the token without a browser.
4. **Given** an authorization-code profile with nothing usable in the cache, **When** the user runs login, **Then** the existing browser flow runs.
5. **Given** a dispatcher and an oauth2 profile, **When** the cache has no usable access token, **Then** the dispatcher exits with an error that tells the user to run `api2skill login --skill <root> --profile <name>`.

---

### Edge Cases

- `migrate` uses the spec sources recorded in the manifest. A remote source that cannot be fetched fails the same way `update` does.
- An empty `requirements.txt` (comments and blank lines only) does not invoke pip.
- `update` keeps the recorded script kind. It does not change language.
- Scheme-level OpenAPI oauth2 on a dispatcher does not POST to a token URL. The operator configures an oauth2 profile and runs login.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `generate --script` accepts `py` in addition to `cs`, `fsx`, and `csx`. `py` writes `scripts/call.py` and documents the runner as `python3 scripts/call.py --`.
- **FR-002**: The Python dispatcher implements the same call surface as the other dispatchers: operation id, `--<param>`, `--body`, base URL, insecure mode, apiKey, bearer, basic, custom headers, and script profiles.
- **FR-003**: The Python dispatcher uses the standard library for HTTP. It ships `scripts/requirements.txt` and, when that file lists requirements, installs them once into `scripts/.pydeps`.
- **FR-004**: `api2skill migrate <skill-path> --script <kind>` regenerates that skill with the new script kind and preserves secrets, auth configuration, the token cache, and examples.
- **FR-005**: `migrate` does not accept a new spec source. `update` does not change script kind.
- **FR-006**: `api2skill login` obtains client-credentials tokens, reuses a valid cached access token, and refreshes an authorization-code token when a refresh token is present.
- **FR-007**: Generated dispatchers do not perform OAuth login, PKCE, loopback capture, refresh, or client-credentials token requests. They read a usable access token from `.auth-cache.json`.
- **FR-008**: `generate --login` runs the CLI login path directly.

### Key Entities

- **Script kind**: `cs`, `fsx`, `csx`, or `py`, stored in `.api2skill.json`.
- **Token cache**: `.auth-cache.json` in the skill root, keyed by auth profile name, holding `access_token`, `expires_at`, and optionally `refresh_token`.
- **Python requirements**: `scripts/requirements.txt`. Empty means no pip install.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A petstore fixture generated with `--script py` runs `python3 -m py_compile` on `scripts/call.py` with no syntax error.
- **SC-002**: Migrating a generated skill from `cs` to `py` leaves `secrets.json` byte-for-byte unchanged and records script kind `py`.
- **SC-003**: Login against a client-credentials profile with a stub token endpoint writes `access_token` into `.auth-cache.json` without a browser.
- **SC-004**: A generated Python dispatcher contains no token-endpoint grant request and no interactive login command.

## Assumptions

- Operators who want OAuth have `api2skill` on PATH. Dispatchers do not invoke the CLI themselves.
- Python 3.11 or newer is available as `python3` when a `py` skill runs. pip is required only when `requirements.txt` lists packages.
- v1's Python emitter lists no third-party packages. The install hook is still present.
- Removing OAuth acquisition from the existing `cs`, `fsx`, and `csx` emitters is part of this feature and lands with the Python emitter so every language follows FR-007.
- A language switch re-fetches the spec sources stored in the manifest, matching `update`.
