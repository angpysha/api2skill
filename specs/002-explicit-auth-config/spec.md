# Feature Specification: Explicit Auth Configuration

**Feature Branch**: `feature/002-explicit-auth-config`

**Created**: 2026-07-11

**Status**: Draft

**Input**: User description: "Extend api2skill: set the output folder explicitly, and set the authentication type explicitly. Support manual bearer token entry, value-from-script, OAuth2 (authorization-code with browser/localhost callback, including an Entra/Azure AD preset, overridable callback URL, client id/secret, custom headers and body on the authorize and token requests — Postman-parity — plus client-credentials), basic, and custom header(s), including multiple headers such as Authorization and ApiKey together. Use a console-app callback + browser for interactive OAuth."

## Overview

Today api2skill **derives** authentication solely from the OpenAPI document's declared
security schemes, and only supports four fixed shapes (apiKey, bearer, basic, and OAuth2
*client-credentials*). Real APIs frequently declare incomplete or inaccurate security, use
interactive (user-delegated) OAuth, require a token obtained from an external tool, or need
more than one credential per request. This feature lets the user **explicitly declare** how
the generated skill authenticates — independent of, and overriding, what the spec says — with
a small committed configuration file and a set of first-class auth types, including an
interactive browser-based OAuth login.

The existing `--out`/`-o` option already lets the user set the output folder explicitly; that
behavior is unchanged and is called out here only to confirm it is in scope-as-satisfied.

## Clarifications

### Session 2026-07-11

- Q: Besides PKCE, should the authorization-code callback use an anti-CSRF `state` parameter? → A: Yes — login generates a random `state`, sends it on the authorize request, and rejects the callback (stores no token) if the returned value doesn't match.
- Q: When two attached profiles would set the same header name on the same operation, what happens? → A: Hard error at generation naming the colliding header and profiles (any same-name collision, global or tag-scoped, is unresolvable and fails generation).
- Q: What does `--auth <type>` do without `--auth-config`? → A: It is a shorthand for the structure-free types only (`bearer`, `basic`, `custom`); `oauth2`/`entra` require `--auth-config`.
- Q: How should the token cache behave under concurrent operation calls? → A: Serialize read-modify-write with an inter-process lock and re-check token validity after acquiring the lock (reuse a token another process just refreshed; never double-refresh or clobber a rotated refresh token).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare a simple explicit credential (Priority: P1)

A user has an OpenAPI spec whose declared security is missing or wrong. They want the generated
skill to send a specific credential (a manually pasted bearer token, HTTP basic, an API key, or
one or more custom headers) regardless of what the spec declares.

**Why this priority**: This is the smallest end-to-end slice that delivers the core value —
"decide auth yourself instead of trusting the spec" — and unblocks the majority of everyday
APIs. It requires the auth-config file, secret references, and non-interactive auth types, but
none of the OAuth browser machinery.

**Independent Test**: Generate a skill with an auth config declaring a manual bearer profile;
fill the token in `secrets.json`; run an operation; observe the request carries
`Authorization: Bearer <token>` even though the spec declared a different (or no) scheme.

**Acceptance Scenarios**:

1. **Given** an auth config with one `bearer` profile and a token in `secrets.json` that lacks
   the `Bearer ` prefix, **When** an operation is invoked, **Then** the request sends
   `Authorization: Bearer <token>` (prefix added exactly once).
2. **Given** a token that already begins with `Bearer ` (any casing), **When** an operation is
   invoked, **Then** the value is sent unchanged (no double prefix).
3. **Given** a `basic` profile with username and password in `secrets.json`, **When** an
   operation is invoked, **Then** the request sends `Authorization: Basic <base64(user:pass)>`.
4. **Given** a `custom` profile declaring two headers (e.g. `Authorization` and `ApiKey`),
   **When** an operation is invoked, **Then** both headers are present with their resolved
   values.
5. **Given** an explicit profile attached to operations the spec marked as secured, **When**
   the skill is generated, **Then** the explicit profile is applied and the spec-derived scheme
   for those operations is not.

---

### User Story 2 - One credential set for some operations, another for others (Priority: P2)

A user's API needs different credentials for different groups of operations (tags): e.g. an
OAuth token for most endpoints, but a static gateway API key header on every request, and a
separate credential for one admin tag.

**Why this priority**: Scoping and stacking are what make explicit auth usable on real,
multi-audience APIs; they build directly on P1's profile mechanism.

**Independent Test**: Declare two profiles — one attached globally, one attached to a single
tag — generate, and confirm operations under that tag carry both credentials while operations
outside it carry only the global one.

**Acceptance Scenarios**:

1. **Given** a profile attached to tag `Admin` and a second profile attached globally, **When**
   an operation tagged `Admin` is invoked, **Then** both profiles' headers are applied.
2. **Given** the same setup, **When** an operation not tagged `Admin` is invoked, **Then** only
   the global profile's headers are applied.
3. **Given** two attached profiles that would set the **same** header name for one operation,
   **When** the skill is generated, **Then** generation **fails** with an error naming the
   colliding header and the two profiles (no output is written).
4. **Given** a profile attached to a tag that matches no operation, **When** the skill is
   generated, **Then** a warning names the unused attachment and generation still succeeds.

---

### User Story 3 - Interactive OAuth login via browser (Priority: P2)

A user's API uses user-delegated OAuth (e.g. Microsoft Entra ID). They want to log in once
through their browser, after which the generated skill calls the API on their behalf and keeps
the session working without re-prompting Claude on every call.

**Why this priority**: This is the marquee capability and the reason for the callback/browser
design, but it depends on the P1 profile/secret plumbing and is heavier to build and test, so
it follows the simpler credential slices.

**Independent Test**: Generate a skill with an `oauth2` authorization-code profile (or the
`entra` preset), run the login step, complete the browser sign-in, then invoke an operation and
observe it succeeds using the acquired token — with no browser prompt on the operation call.

**Acceptance Scenarios**:

1. **Given** an `oauth2` authorization-code profile, **When** the user runs the login step,
   **Then** the system opens the user's browser to the authorize URL, receives the redirect on
   the configured local callback, and obtains and stores an access token (and a refresh token
   when the provider returns one).
2. **Given** a stored, still-valid access token, **When** an operation is invoked, **Then** the
   call uses the stored token and does not open a browser.
3. **Given** a stored access token that has expired and a stored refresh token, **When** an
   operation is invoked, **Then** the token is refreshed silently and the call proceeds without
   user interaction.
4. **Given** an expired access token and no usable refresh token, **When** an operation is
   invoked, **Then** the call fails with a clear message instructing the user to run the login
   step again, and no browser is launched during the (non-interactive) operation call.
5. **Given** the `entra` preset with a tenant identifier, **When** the skill is generated,
   **Then** the authorize and token endpoints are populated for that tenant and offline access
   (refresh capability) is requested by default.
6. **Given** a headless environment where a browser cannot be launched during login, **When**
   the user runs the login step, **Then** the authorize URL is printed for the user to open
   manually and the callback is still received.

---

### User Story 4 - Token obtained from an external command (Priority: P3)

A user already has a tool that prints a valid token (e.g. a cloud CLI). They want each API call
to use the current output of that command as the credential.

**Why this priority**: Valuable for CLI-centric environments but narrower than the other types;
it reuses the profile/header plumbing and adds only command execution.

**Independent Test**: Declare a `script` profile whose command echoes a token; invoke an
operation; confirm the request carries the command's (trimmed) stdout as the configured header.

**Acceptance Scenarios**:

1. **Given** a `script` profile with a command that prints a token, **When** an operation is
   invoked, **Then** the command runs and its trimmed stdout is sent as the configured header
   (default `Authorization`).
2. **Given** `bearerPrefix` enabled and stdout lacking the `Bearer ` prefix, **When** an
   operation is invoked, **Then** `Bearer ` is prepended exactly once.
3. **Given** a command that exits non-zero, **When** an operation is invoked, **Then** the call
   fails and the command's error output is surfaced to the user.

---

### Edge Cases

- **Missing auth config**: `--auth-config` points to a file that does not exist → generation
  fails with a clear, actionable error (no partial output).
- **Invalid auth config**: the file exists but is malformed or a profile has an unknown `type`
  → generation fails naming the offending profile/field.
- **Unresolved secret reference**: a profile references `{secret:NAME}` but `secrets.json` has
  no such key at call time → the call fails with a message naming the missing key (never sends
  an empty/placeholder credential silently).
- **Callback port already in use**: the configured local callback port is occupied during login
  → the login step reports the conflict and how to choose a different callback URL/port.
- **Browser cannot be launched (headless)**: login prints the authorize URL to open manually and
  still completes on redirect.
- **Login invoked on a client-credentials-only profile**: reported as not applicable (that flow
  needs no interactive login); the token is instead fetched on demand at call time.
- **Expired refresh token / provider revokes session**: silent refresh fails → operation call
  fails with a re-login instruction.
- **`offline_access` not requested but refresh attempted**: no refresh token is available →
  treated as "no usable refresh token" (re-login instruction), with guidance to enable offline
  access.
- **Script command not found / non-zero exit**: the call fails and surfaces stderr.
- **Duplicate header across stacked profiles**: two attached profiles set the same header name
  on the same operation → **generation fails** with an error naming the header and the profiles.
- **Callback `state` mismatch**: the redirect returns a `state` that doesn't match the one sent
  → the login step rejects it, stores no token, and reports a possible CSRF/mix-up.
- **`--auth <type>` shorthand for an interactive type**: passing `--auth oauth2`/`--auth entra`
  without `--auth-config` → usage error directing the user to supply `--auth-config` (interactive
  profiles need URLs/tenant that the shorthand cannot capture).
- **Attachment references a tag with no operations**: warn at generation; succeed.
- **Multiple interactive profiles needing the same callback port**: reported as a collision to
  resolve via distinct callback URLs.

## Requirements *(mandatory)*

### Functional Requirements

#### Configuration & selection

- **FR-001**: The generator MUST accept an explicit authentication configuration via a
  `--auth-config <file>` option, and a `--auth <type>` shorthand that scaffolds a single
  globally-attached profile for the **structure-free types only** (`bearer`, `basic`, `custom`).
  The interactive/OAuth types (`oauth2`, `entra`) require `--auth-config` (they need URLs/tenant
  the shorthand cannot express); passing them via `--auth` MUST produce a usage error directing
  the user to `--auth-config`.
- **FR-002**: The auth configuration MUST be a **committed, secret-free** artifact placed in the
  generated skill directory; the generator MUST NOT write any real credential into it or into
  any other committed file.
- **FR-003**: The auth configuration MUST express a **list of named profiles**, each with a
  `type` of `bearer`, `script`, `oauth2`, `basic`, or `custom`.
- **FR-004**: Each profile MUST be **attachable** either globally (default) or to one or more
  **tags** (operation groups). When attached to tags, it applies only to operations in those
  tags.
- **FR-005**: When two or more profiles are attached to the same operation, the generated skill
  MUST apply **all** of them (each contributes its header(s)/query value(s)).
- **FR-006**: Explicit auth MUST **override** spec-derived authentication for the operations it
  covers; operations not covered by any explicit profile retain their existing (spec-derived)
  behavior. The spec still determines which operations are secured vs public.
- **FR-007**: The configuration MUST support **secret references** (placeholder tokens such as
  `{secret:NAME}`) that are resolved at call time from `secrets.json`; literal non-secret values
  MUST also be allowed where appropriate.
- **FR-008**: The generator MUST scaffold `secrets.example.json` with the placeholder keys each
  configured profile requires, and ensure the real secret store and the token cache are
  git-ignored.

#### Auth types (call-time behavior of the generated skill)

- **FR-009**: `bearer` — the generated skill MUST send `Authorization: <token>`, prepending
  `Bearer ` if and only if the configured token does not already begin with `Bearer `
  (case-insensitive), exactly once.
- **FR-010**: `basic` — the generated skill MUST send `Authorization: Basic <base64(user:pass)>`
  from the profile's referenced username/password secrets.
- **FR-011**: `custom` — the generated skill MUST send an ordered set of one or more headers with
  values resolved from secret references or literals; multiple distinct header names (e.g.
  `Authorization` and `ApiKey`) MUST be supported.
- **FR-012**: `script` — the generated skill MUST run the profile's configured command **on each
  call**, use the command's **trimmed stdout** as the value of a configurable header (default
  `Authorization`), optionally prepend `Bearer ` (same rule as FR-009) when enabled, and **fail
  the call** surfacing stderr when the command exits non-zero.
- **FR-013**: `oauth2` — the generated skill MUST support the **authorization-code** grant **with
  PKCE**, treating the client secret as **optional** (public-client capable), and MUST continue
  to support the **client-credentials** grant for unattended machine-to-machine use.
- **FR-014**: The `oauth2` profile MUST allow configuration of: authorize URL, token URL,
  scopes, callback URL (default a local loopback callback, overridable), the **client
  authentication method** (client credentials sent in the request body [default] or as an HTTP
  Basic header), and **custom additional headers and body parameters** applied to **both** the
  authorize request and the token request (parity with common API clients such as Postman).
- **FR-015**: The generator MUST provide an **`entra` preset** that, from a tenant identifier,
  populates the Entra/Azure AD authorize and token endpoints and requests offline access
  (refresh) by default. A fully-manual generic `oauth2` profile MUST also be supported. Other
  identity-provider presets are out of scope for this feature.

#### Interactive login & token lifecycle

- **FR-016**: The generated skill MUST expose an interactive **login step** (a `login <profile>`
  entry point on the dispatcher) that, for an authorization-code profile, opens the user's
  browser to the authorize URL, receives the redirect on the configured local callback, and
  exchanges the authorization code (with the PKCE verifier) for tokens.
- **FR-016a**: The authorization-code login MUST generate a random **`state`** value, include it
  in the authorize request, and **reject the callback** (storing no token and reporting a
  possible CSRF/mix-up) when the returned `state` does not match. This applies in addition to
  PKCE.
- **FR-017**: The generator MUST support an **opt-in `--login` flag** that, after writing the
  skill files, runs the same interactive login once to prime the token cache. Without `--login`,
  generation MUST remain fully non-interactive (safe for automation/CI).
- **FR-018**: Acquired tokens MUST be stored **per profile** in a **git-ignored** token cache
  file placed next to `secrets.json`, with **restrictive file permissions**, recording at least
  the access token, its expiry, and the refresh token when provided.
- **FR-019**: On each operation call, the generated skill MUST: use the cached access token if
  still valid; otherwise, if a refresh token exists, **refresh silently** and proceed; otherwise
  **fail with an actionable message** instructing the user to run the login step again. The
  operation call path MUST NOT launch a browser (it may be invoked non-interactively).
- **FR-019a**: Because operation calls may run **concurrently** (separate dispatcher processes),
  the token cache MUST be accessed under an **inter-process lock** guarding the full
  read-modify-write. After acquiring the lock a process MUST **re-check** token validity and
  reuse a token another process just refreshed rather than refreshing again, so the cache is
  never corrupted and a rotated refresh token is never clobbered.
- **FR-020**: `client-credentials` tokens MUST be obtainable **on demand** at call time (no
  interactive login), and MAY be cached like other tokens.

#### Diagnostics, docs, and scope

- **FR-021**: The generator MUST **warn** (without failing) when a profile is attached to a tag
  that has no operations.
- **FR-021a**: The generator MUST **fail** generation (writing no output) when two attached
  profiles would set the **same header name** on the same operation, naming the header and the
  colliding profiles.
- **FR-022**: The generated skill's documentation (`SKILL.md` / reference) MUST explain, per
  configured profile, how to supply secrets, how to run the login step, and what each auth type
  does — including that `script` executes a **user-provided local command**.
- **FR-023**: All three script emitters (default `cs`, plus `fsx` and `csx`) MUST produce
  equivalent auth behavior for every supported type.
- **FR-024**: The existing `--out`/`-o` output-directory selection MUST remain available and
  unchanged; explicit output-folder selection is considered satisfied by it.
- **FR-025**: No real secret value MUST ever be persisted to a committed file; the auth config,
  `secrets.example.json`, and generator output MUST contain placeholders only.

### Key Entities *(include if feature involves data)*

- **Auth configuration**: the committed, secret-free description of how the generated skill
  authenticates — an ordered list of profiles plus their attachments.
- **Auth profile**: a named credential definition of one `type` (`bearer` | `script` | `oauth2`
  | `basic` | `custom`) with type-specific settings and secret references.
- **Attachment**: the binding of a profile to a scope — global or a set of tags — determining
  which operations it applies to.
- **Secret reference**: a placeholder in the auth configuration resolved at call time from the
  git-ignored secret store.
- **Token cache**: the git-ignored, restricted-permission per-profile store of access tokens,
  expiries, and refresh tokens produced by login/refresh.
- **OAuth preset**: a named shortcut (initially `entra`) that fills provider-specific
  authorize/token endpoints and defaults from a small input (a tenant identifier).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can make the generated skill send a chosen credential that differs from the
  spec's declared scheme, editing only the committed auth config and the git-ignored secret
  store (no code changes).
- **SC-002**: A user can configure two credentials (e.g. an OAuth token plus a static API-key
  header) so both are present on the same request.
- **SC-003**: For an authorization-code (or `entra`) API, a user completes an interactive login
  once and can then invoke operations repeatedly across separate process runs without any
  further browser prompt until the session can no longer be refreshed.
- **SC-004**: When a session can no longer be refreshed, an operation call fails with a message
  that tells the user exactly how to restore access (re-run login), and never hangs waiting on a
  browser during a non-interactive call.
- **SC-005**: Every supported auth type behaves identically across the three script emitters.
- **SC-006**: No committed artifact produced by the generator contains a real secret value.
- **SC-007**: Every edge case listed above yields a clear, actionable message rather than a
  silent failure or an unhandled crash.

## Assumptions

- The user runs the interactive login on a machine with a browser (or can open a printed URL
  manually); Claude only invokes operation calls, which never require interaction.
- The user controls the machine and the `script` profile's command; executing that command is an
  accepted, documented local-execution behavior, not a sandbox boundary.
- `secrets.json` and the token cache live next to the generated dispatcher and are git-ignored;
  protecting the local filesystem is the user's responsibility.
- Identity providers used with the authorization-code flow permit a loopback (localhost)
  redirect URI, and the redirect URI used at login is registered with the provider.
- Only the `entra` OAuth preset ships in this feature; other providers use the generic manual
  `oauth2` profile.
- The generated dispatcher continues to run via `dotnet run` (no separate build step), so it can
  host the local callback listener and launch a browser during the login step.
- The existing spec-derived auth for apiKey/bearer/basic/OAuth2-client-credentials remains for
  operations no explicit profile covers.
