# Feature Specification: HTTPS OAuth callback listener

**Feature Branch**: `feature/009-oauth-https-callback`

**Created**: 2026-07-14

**Status**: Draft — grill required before plan/implement

**Input**: User description: "Add support for HTTPS on the OAuth callback listener. Suspected reason a real-project login fails when the redirect URI is `https://…` instead of `http://localhost…`."

## Background (current behavior)

Generated dispatchers bind an `HttpListener` using prefixes derived from `callbackUrl`'s **scheme**, host, and port (`BeginCallbackListener` / `AddCallbackPrefixes`). HTTP works for `http://localhost:8400/` (and dual `127.0.0.1`).

For `https://…` prefixes, `HttpListener.Start()` typically fails or the browser cannot complete TLS unless an OS/server certificate is configured. Windows historically required `netsh http add sslcert` + URL ACL; cross-platform .NET does **not** provide a one-liner for HttpListener HTTPS. ASP.NET Core Kestrel + `dotnet dev-certs` / mkcert is the usual cross-platform approach.

Hypothesis to validate during grill/implement: real IdP redirect URI is **HTTPS** (Entra/B2C app registration or company policy), while the skill only successfully listens on **HTTP**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - HTTPS loopback callback completes login (Priority: P1)

A user sets `callbackUrl` to an `https://localhost:<port>/…` (or `https://127.0.0.1:…`) URL matching their IdP redirect registration. Running `login` starts a TLS callback listener; after browser redirect with `code`/`error`, login completes and tokens are cached — same as HTTP today.

**Why this priority**: Unblocks real OAuth apps that require HTTPS redirect URIs.

**Independent Test**: Generate skill with HTTPS `callbackUrl`, trust/config cert per design, run `login`, simulate or complete redirect over HTTPS, assert token cache written.

**Acceptance Scenarios**:

1. **Given** `callbackUrl` is `https://localhost:8400/callback` and cert material is available per design, **When** the user runs `login`, **Then** the terminal reports listening on that HTTPS URL and the browser can complete the redirect without a permanent TLS failure (after trust if self-signed).
2. **Given** a successful HTTPS redirect with matching `state` and `code`, **When** token exchange succeeds, **Then** `.auth-cache.json` is written as with HTTP.

---

### User Story 2 - HTTP callback unchanged (Priority: P1)

Existing `http://localhost:…` / `http://127.0.0.1:…` profiles keep working without requiring certificates.

**Independent Test**: Existing `DispatcherOAuthLoginTests` stay green with no cert setup.

---

### User Story 3 - Clear failure when HTTPS cannot start (Priority: P2)

If `callbackUrl` is HTTPS but no usable certificate / bind fails, `login` fails with an actionable message (how to provide a cert, trust it, or switch to HTTP if IdP allows).

**Independent Test**: Force missing cert → non-zero exit + message mentioning HTTPS/certificate/`callbackUrl`.

---

### Edge Cases

- Scheme case / trailing slash / path-only differences still subject to IdP exact redirect match.
- Self-signed cert not trusted by browser → user sees interstitial; skill should still receive request if user continues **or** docs require trust — **[NEEDS CLARIFICATION]**.
- Port already in use — same as HTTP conflict message.
- Non-loopback HTTPS hosts (e.g. company redirect to `https://app.example.com/callback`) — **out of scope for local listener**; redirect must hit the loopback process — **[NEEDS CLARIFICATION: loopback-only vs custom host]**.
- Windows / macOS / Linux parity for cert install/trust — **[NEEDS CLARIFICATION: supported OS for v1]**.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When `callbackUrl` uses scheme `http`, behavior MUST remain equivalent to current HTTP listener (including localhost/`127.0.0.1` dual bind).
- **FR-002**: When `callbackUrl` uses scheme `https`, `login` MUST start a local HTTPS listener capable of accepting the OAuth redirect on that host/port/path and completing the existing state/code/token flow.
- **FR-003**: Certificate source MUST be configurable or discoverable without embedding private keys in the repo — **[NEEDS CLARIFICATION: auth.json fields vs env vs auto `dotnet dev-certs` / mkcert]**.
- **FR-004**: On HTTPS listener startup failure, login MUST fail with a clear, actionable error (not hang).
- **FR-005**: All three emitters (`cs` / `csx` / `fsx`) MUST share the HTTPS-capable behavior.
- **FR-006**: Wiki/Authentication docs MUST document HTTPS `callbackUrl`, cert/trust steps, and IdP redirect URI exact match.
- **FR-007**: Automated tests MUST cover at least one HTTPS happy path (integration or unit with test cert) OR document why CI cannot (platform limits) with a compensating manual checklist — **[NEEDS CLARIFICATION]**.

### Key Entities

- **callbackUrl**: Absolute URI in `auth.json`; scheme selects HTTP vs HTTPS listener mode.
- **Callback TLS material**: Certificate (+ key) used only for the local login listener; never committed as a real secret in git.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer with an IdP redirect URI of `https://localhost:<port>/…` can complete `login` end-to-end on a supported OS without switching the IdP to HTTP.
- **SC-002**: HTTP-only profiles require zero new config after this feature.
- **SC-003**: HTTPS misconfiguration produces a diagnostic within seconds of `login` start (no indefinite hang).

## Assumptions

- IdP redirect URI must still match `callbackUrl` **exactly** (including `https` and path).
- Goal is **local loopback** HTTPS for OAuth code capture, not hosting a public TLS endpoint.
- Private keys / PFX passwords stay out of generated skill templates (user secrets / machine store / developer cert only).
- Package version bump follows `version-bump` rule when this ships (likely **0.5.1** patch or **0.6.0** if framing as new capability — decide at PR).

## Open questions (grill)

1. Cert strategy: auto `dotnet` ASP.NET HTTPS developer certificate, path-based PEM/PFX in `auth.json`, or mkcert-oriented docs only?
2. v1 OS support: macOS + Windows + Linux, or macOS-first?
3. Must the browser trust the cert without clicking through, or is click-through OK for v1?
4. Keep `HttpListener` + OS SSL bind where possible, or switch HTTPS path to Kestrel / `SslStream` for cross-platform reliability?
5. Allow non-loopback hosts in `callbackUrl` for HTTPS, or restrict to localhost/127.0.0.1?
