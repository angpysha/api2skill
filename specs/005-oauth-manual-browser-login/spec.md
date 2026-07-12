# Feature Specification: Manual-Browser OAuth Login

**Feature Branch**: `feature/005-oauth-manual-browser-login`

**Created**: 2026-07-12

**Status**: Implemented

**Input**: User description: "For OAuth, don't open the login link in the default system browser — make that configurable. Instead, copy the URL to the clipboard so the user can paste it into their browser of choice, and keep listening for the OAuth callback on the local port as before."

## Overview

Every generated skill's `login` command performs the interactive OAuth2 authorization-code flow (`AppendOAuthFunctions` / `LoginAsync` in `CsxEmitter`, `FsxEmitter`, and `CsFileEmitter`). Today it unconditionally shells out to the OS default-browser launcher (`TryLaunchBrowser`) with the authorize URL, then starts a local `HttpListener` on the profile's `callbackUrl` to receive the redirect.

Some users don't want the system default browser used for login — e.g. it's not signed into the right account, or they use a different browser for work identities. This feature adds a per-profile `auth.json` setting so `login` can, instead of launching a browser, copy the authorize URL to the system clipboard (and still print it) so the user can paste it into whichever browser they choose. The local callback listener behavior is unchanged — it already listens on the profile's configured `callbackUrl` regardless of how the authorize URL was delivered to the user.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Opt a profile out of auto-launching the browser (Priority: P1)

A user generates a skill with an `oauth2` profile whose `auth.json` entry sets `"browserLaunch": "clipboard"`. When they run `login`, the tool does not attempt to open any browser. Instead it copies the authorize URL to the system clipboard and prints it, then waits for the callback exactly as before. The user pastes the URL into their preferred browser, signs in, and the callback completes the flow.

**Why this priority**: This is the entire feature — without it there is no way to skip the auto-launch.

**Independent Test**: Generate a skill with an oauth2 profile that has `"browserLaunch": "clipboard"`, run `login <profile>`, confirm no browser process is launched, confirm the clipboard contains the authorize URL, confirm the URL is printed, and confirm completing the flow in a manually opened browser still stores a token in `.auth-cache.json`.

**Acceptance Scenarios**:

1. **Given** an oauth2 profile with `"browserLaunch": "clipboard"`, **When** the user runs `login <profile>`, **Then** the tool does not call the OS browser launcher, copies the authorize URL to the clipboard, and prints a message confirming the copy plus the URL itself.
2. **Given** the same profile, **When** the user pastes the URL into a browser and completes sign-in, **Then** the local callback listener receives the redirect and the flow completes exactly as it does today (state check, code exchange, token cache write).

---

### User Story 2 - Default behavior is unchanged for existing profiles (Priority: P1)

A user with an existing `auth.json` (no `browserLaunch` field, from before this feature) runs `login` and sees the exact same behavior as today: the tool attempts to launch the OS default browser and falls back to printing the URL only if launching fails.

**Why this priority**: Regression safety — this feature must not change behavior for the common case without explicit opt-in.

**Independent Test**: Generate a skill with an oauth2 profile that omits `browserLaunch`, run `login <profile>`, confirm the OS browser launcher is attempted first (current `TryLaunchBrowser` behavior).

**Acceptance Scenarios**:

1. **Given** an oauth2 profile with no `browserLaunch` field, **When** the user runs `login <profile>`, **Then** behavior is identical to the current implementation (`"auto"` is the default).
2. **Given** an oauth2 profile with `"browserLaunch": "auto"` explicitly set, **When** the user runs `login <profile>`, **Then** behavior is identical to the default (explicit `"auto"` and omitted field are equivalent).

---

### User Story 3 - Clipboard tool unavailable (Priority: P2)

A user runs `login` with `"browserLaunch": "clipboard"` on a machine/environment where no clipboard utility is available (e.g. a headless Linux box with no `xclip`/`xsel`/`wl-copy`). The tool does not crash — it prints the URL with a clear note that it could not be copied, and still waits for the callback.

**Why this priority**: Keeps the feature usable in degraded environments without a hard failure; lower priority because it's a fallback path, not the primary flow.

**Independent Test**: Simulate a missing clipboard utility (e.g. via `PATH` manipulation on Linux) and confirm `login` still prints the URL, notes the clipboard copy failed, and still starts the callback listener.

**Acceptance Scenarios**:

1. **Given** `"browserLaunch": "clipboard"` and no clipboard utility available, **When** the user runs `login <profile>`, **Then** the tool prints the authorize URL with a message that automatic copying was not available, and proceeds to wait for the callback (does not exit early).

---

### Edge Cases

- **Invalid `browserLaunch` value** (anything other than `"auto"`/`"clipboard"`, e.g. a typo): generation-time validation MUST reject it with a clear error naming the profile and the allowed values, consistent with how other malformed `auth.json` fields are rejected today.
- **`browserLaunch` on a non-oauth2 profile**: generation-time validation MUST reject it — the field only applies to `type: "oauth2"` profiles.
- **`client_credentials` grant with `browserLaunch` set**: no-op, same as today — `login` for `client_credentials` returns early before any browser/clipboard logic runs, regardless of `browserLaunch`.
- **Clipboard copy succeeds but paste/login never happens**: no change from today's behavior — the callback listener has no timeout; the process waits until the user completes the flow or the process is interrupted.
- **Windows/macOS/Linux clipboard mechanism differs**: MUST use native, dependency-free OS clipboard tools (mirroring how `TryLaunchBrowser` already shells out to `open`/`xdg-open`/`cmd`) — no new NuGet/package dependency in the emitted scripts, since generated `login.csx`/`login.fsx`/`Program.cs` intentionally have zero external dependencies today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `auth.json` oauth2 profiles MUST support an optional `browserLaunch` field with allowed values `"auto"` (default) and `"clipboard"`.
- **FR-002**: When `browserLaunch` is `"auto"` or omitted, `login` MUST behave exactly as it does today (attempt OS browser launch, fall back to printing the URL only if launch fails).
- **FR-003**: When `browserLaunch` is `"clipboard"`, `login` MUST NOT attempt to launch any browser. It MUST instead attempt to copy the authorize URL to the system clipboard using a native OS mechanism, and MUST print the authorize URL regardless of whether the clipboard copy succeeded.
- **FR-004**: When the clipboard copy in FR-003 fails or no clipboard mechanism is available, `login` MUST print a message noting that the URL was not copied automatically, without treating this as a fatal error.
- **FR-005**: Regardless of `browserLaunch`, `login` MUST continue to start the local callback listener on the profile's `callbackUrl` (existing behavior, unchanged) and complete the state check, code exchange, and token cache write exactly as today.
- **FR-006**: Generation-time validation MUST reject a `browserLaunch` value other than `"auto"`/`"clipboard"` with a clear per-profile error.
- **FR-007**: Generation-time validation MUST reject `browserLaunch` set on a profile whose `type` is not `"oauth2"`.
- **FR-008**: This feature MUST be implemented consistently across all three emitters that generate a `login` command (`CsxEmitter`, `FsxEmitter`, `CsFileEmitter`) so behavior does not depend on which script target was chosen at `generate` time.
- **FR-009**: The emitted clipboard-copy logic MUST use only OS-native command-line tools reachable via `Process.Start` (e.g. `pbcopy` on macOS, `clip` on Windows, `xclip`/`xsel`/`wl-copy` on Linux) — no new NuGet package dependency.

### Key Entities

- **OAuth profile setting `browserLaunch`**: new optional string field on an `oauth2` `auth.json` profile controlling how the authorize URL is delivered to the user (`"auto"` | `"clipboard"`).
- **Emitted `login` command**: the generated script logic (present in `.csx`, `.fsx`, and compiled `Program.cs` skill targets) that resolves the authorize URL, delivers it per `browserLaunch`, and waits for the local OAuth callback.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can set `"browserLaunch": "clipboard"` on an oauth2 profile and complete login using a browser other than the OS default, without the tool ever invoking the OS browser launcher.
- **SC-002**: Existing skills/`auth.json` files with no `browserLaunch` field continue to work with zero behavior change (regression-safe).
- **SC-003**: On a machine with no clipboard utility, `"browserLaunch": "clipboard"` still results in a completable login (URL is visible in the terminal) rather than a crash or silent failure.
- **SC-004**: Behavior is identical across skills generated with `--target csx`, `--target fsx`, and `--target cs` (or equivalent) for the same `auth.json`.

## Assumptions

- "Configurable" means a per-profile `auth.json` field, matching how `callbackUrl`, `scopes`, `preset`, etc. are already configured per-profile (spec 002's model) — not a new global CLI flag or environment variable.
- "Listen for the callback on the port" requires no new behavior: the existing per-profile `callbackUrl` (default `http://localhost:8400/callback`) and `WaitForCallbackAsync` listener already do this, independent of how the authorize URL reaches the user. This feature does not add a `--port` override or auto-port-selection.
- Clipboard delivery is best-effort: if no OS clipboard tool is found, the flow degrades to "print the URL" (same terminal experience as today's browser-launch-failure fallback), not a hard error.
- No new NuGet/package dependency is introduced in generated skills — clipboard copy shells out to native OS binaries the same way `TryLaunchBrowser` already shells out to `open`/`xdg-open`/`cmd`.

## Traceability

| Source | Reference |
|--------|-----------|
| Existing OAuth flow | `src/Api2Skill/Emit/CsxEmitter.cs` `AppendOAuthFunctions`/`LoginAsync`; mirrored in `FsxEmitter.cs`, `CsFileEmitter.cs` |
| Per-profile auth config model | specs/002-explicit-auth-config/spec.md |
