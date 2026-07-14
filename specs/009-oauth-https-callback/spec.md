# Feature Specification: App-owned OAuth redirect capture (multi-mode)

**Feature Branch**: `feature/009-oauth-https-callback`

**Created**: 2026-07-14

**Updated**: 2026-07-14

**Status**: Draft — grill in progress

**Input**: Capture OAuth redirects in the **api2skill app** (not HTTPS-listener-only). Must support:

- **HTTP** callback (mandatory)
- **HTTPS** callback (mandatory)
- **Custom URL scheme(s)** (our default e.g. `api2skill://…` **and** user/other schemes)
- **Any other address** in the Postman sense: user-supplied Callback URL that is registered with the IdP (loopback, custom host, or app-provided), with the app performing capture / handoff back into api2skill token flow

## Grill decisions so far

| Topic | Decision |
|-------|----------|
| Logic lives in | **api2skill app**; skill invokes / continues after capture |
| Local HTTPS as *only* fix | Rejected as exclusive approach |
| Supported callback kinds | **HTTP + HTTPS mandatory**; **custom schemes**; **arbitrary/custom callback address** (Postman-like field) |
| Our scheme | Supported (e.g. `api2skill://oauth/callback`) plus **other** schemes the user configures |
| HTTP / HTTPS | Required (must work for `http://…` and `https://…` redirect URIs) |
| HTTPS cert / trust | **Tool parameter** for cert path (when IdP/browser requires a trusted cert). If missing when HTTPS bind needs it, **prompt the user** (interactive). Use **colored CLI output** so the trust/prompt is impossible to miss |

## Background

### Postman analogy

Postman lets you set a **Callback URL** (custom) and also ships defaults (`https://oauth.pstmn.io/...`). Capture is app-controlled; the URL must match the IdP allow-list.

### api2skill target

`callbackUrl` in `auth.json` (or an equivalent capture URL) may be:

| Kind | Example | App responsibility |
|------|---------|-------------------|
| HTTP loopback | `http://localhost:8400/` | Bind HTTP listener (existing) |
| HTTPS loopback | `https://localhost:8400/` | Bind HTTPS listener (**mandatory**); cert via tool param / interactive ask (see FR-006) |
| Our custom scheme | `api2skill://oauth/callback` | Register / handle OS protocol |
| Other custom scheme | `myapp://auth` or vendor scheme | Handle if OS routes to us, or document limits |
| Other address | Hosted / custom HTTPS URL | **[NEEDS CLARIFICATION: how we receive the redirect without owning that host]** |

After capture → PKCE token exchange → write skill `.auth-cache.json`.

## User Scenarios & Testing

### User Story 1 - HTTP callback (Priority: P1)

Existing loopback HTTP login continues to work via app-owned or delegated listener.

### User Story 2 - HTTPS callback (Priority: P1)

`callbackUrl` with `https://` completes login without forever-hang. User supplies cert via CLI flag/config, or is prompted (colored warning) when trust material is required and missing.

### User Story 3 - Our custom scheme (Priority: P1)

User registers `api2skill://…` (exact string TBD) at IdP; after auth, OS opens api2skill; capture completes; tokens cached.

### User Story 4 - Other custom scheme / custom address (Priority: P2)

User sets an arbitrary Callback URL (Postman-like). App either captures it (if traffic can reach the app) or documents + integrates an app-provided default HTTPS capture URL — **[NEEDS CLARIFICATION]**.

### Edge Cases

- Scheme registered but different version of tool installed — **[NEEDS CLARIFICATION]**
- HTTPS without cert param and non-interactive session → fail with clear colored error (do not hang)
- Non-loopback HTTPS `https://customer.app/callback` when we don’t host it — cannot receive without relay — **must clarify**

## Requirements

- **FR-001**: App MUST support OAuth redirect capture for **`http://`** callback URLs.
- **FR-002**: App MUST support OAuth redirect capture for **`https://`** callback URLs (mandatory; not optional stretch).
- **FR-003**: App MUST support a first-party custom scheme (e.g. `api2skill://…`) for capture.
- **FR-004**: App MUST allow configuring **additional / arbitrary** callback URLs and schemes (Postman-like Callback URL field), subject to OS/IdP constraints documented in wiki.
- **FR-005**: Capture logic MUST live in the **api2skill app**; skill/`login` calls into it then continues token exchange + cache.
- **FR-006**: For local HTTPS, the app MUST accept a **tool parameter** for TLS certificate material (path and/or related options — exact flag names in plan). If HTTPS capture requires a trusted cert and the parameter is unset, the app MUST **ask the user interactively** (TTY). Prompts and trust-related warnings MUST use **colored output** so they stand out. Non-TTY / CI without the parameter MUST fail fast with an explicit error (no silent hang).
- **FR-007**: How “any other address” is received when not loopback and not our scheme — **[NEEDS CLARIFICATION]** (hosted relay vs require loopback/scheme only for capture)

## Success Criteria

- **SC-001**: HTTP and HTTPS callback profiles can both complete `login` on supported OS.
- **SC-002**: First-party custom scheme login works with IdP registration.
- **SC-003**: User can set a custom callback string like Postman; behavior for that string is documented and tested where feasible.

## Open questions (remaining grill)

1. ~~HTTPS cert trust / param~~ → **locked**: tool parameter + interactive ask + colored output (FR-006). Exact PEM/PFX vs `dotnet`/mkcert paths → plan stage OK unless human prefers one format now.
2. **Non-loopback “any address”**: ship a **hosted** capture URL (Postman `oauth.pstmn.io` analogue), or only support addresses the local app can receive (loopback HTTP/HTTPS + custom schemes)?
3. **Handoff CLI**: `api2skill login --skill …` vs skill shells out to `api2skill oauth-capture`?
4. **Registration** of `api2skill://` on install vs first login vs explicit `api2skill register-protocol`?
