# Feature Specification: App-owned OAuth redirect capture (multi-mode)

**Feature Branch**: `feature/009-oauth-https-callback`

**Created**: 2026-07-14

**Updated**: 2026-07-14

**Status**: Planned — see [plan.md](./plan.md); next `/speckit.tasks`

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
| Non-loopback / any address | **B — hosted capture URL in v1** (Postman-style analogue); complements local HTTP/HTTPS + schemes |
| Skill ↔ app handoff | **C** — both: `api2skill login --skill …` for humans; thin `api2skill oauth-capture` (name TBD in plan) for scripts / generated skill; skill may shell out then continue token/cache |
| Custom scheme registration | **C — explicit only**: `api2skill register-protocol` (and unregister if planned); no silent install/first-login registration |

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
| Other address | Hosted / custom HTTPS URL | **v1: ship hosted capture URL** (app-provided default); user can register that URL (or their custom URL if traffic can reach app) at IdP |

After capture → PKCE token exchange → write skill `.auth-cache.json`.

## User Scenarios & Testing

### User Story 1 - HTTP callback (Priority: P1)

Existing loopback HTTP login continues to work via app-owned or delegated listener.

### User Story 2 - HTTPS callback (Priority: P1)

`callbackUrl` with `https://` completes login without forever-hang. User supplies cert via CLI flag/config, or is prompted (colored warning) when trust material is required and missing.

### User Story 3 - Our custom scheme (Priority: P1)

User runs `api2skill register-protocol`, registers `api2skill://…` at the IdP; after auth, OS opens api2skill; capture completes; tokens cached.

### User Story 4 - Hosted capture + custom address (Priority: P1)

User registers the **app-provided hosted** HTTPS Callback URL (or a custom URL) at the IdP. Redirect lands on the hosted page; capture completes and hands off to the local api2skill app / skill token flow. Custom schemes and loopback remain available as alternatives.

### Edge Cases

- Scheme used before `register-protocol` — clear colored error telling user to run register
- Scheme registered but different tool version — document re-run `register-protocol` after upgrade
- HTTPS without cert param and non-interactive session → fail with clear colored error (do not hang)
- User’s own non-loopback HTTPS without relay — fail with docs pointing to hosted URL, loopback, or scheme
- Hosted page unavailable / timeout — clear colored error; no hang

## Requirements

- **FR-001**: App MUST support OAuth redirect capture for **`http://`** callback URLs.
- **FR-002**: App MUST support OAuth redirect capture for **`https://`** callback URLs (mandatory; not optional stretch).
- **FR-003**: App MUST support a first-party custom scheme (e.g. `api2skill://…`) for capture.
- **FR-004**: App MUST allow configuring **additional / arbitrary** callback URLs and schemes (Postman-like Callback URL field), subject to OS/IdP constraints documented in wiki.
- **FR-005**: Capture logic MUST live in the **api2skill app**; skill/`login` calls into it then continues token exchange + cache.
- **FR-006**: For local HTTPS, the app MUST accept a **tool parameter** for TLS certificate material (path and/or related options — exact flag names in plan). If HTTPS capture requires a trusted cert and the parameter is unset, the app MUST **ask the user interactively** (TTY). Prompts and trust-related warnings MUST use **colored output** so they stand out. Non-TTY / CI without the parameter MUST fail fast with an explicit error (no silent hang).
- **FR-007**: v1 MUST ship a **hosted HTTPS capture URL** (Postman-style) that users can register at the IdP. After redirect, the capture result MUST hand off into the local api2skill app / skill token + cache flow. Privacy/timeout/abuse constraints are documented in plan/wiki. Exact host/path and infra — plan stage (may be temporary staging URL until permanent domain).
- **FR-008**: App MUST expose both (1) a user-facing `api2skill login --skill …` (profile/options TBD in plan) that owns end-to-end login where appropriate, and (2) a thin capture command (e.g. `api2skill oauth-capture`) for scripts and generated skill shells; capture handoff MUST feed token exchange + `.auth-cache.json` without duplicating capture logic in the skill.
- **FR-009**: Registration of the first-party custom scheme MUST be **explicit only** via `api2skill register-protocol` (unregister optional in plan). MUST NOT register silently on install or first login. Docs and colored CLI MUST tell users to run register when the scheme is required.

## Success Criteria

- **SC-001**: HTTP and HTTPS callback profiles can both complete `login` on supported OS.
- **SC-002**: First-party custom scheme login works after explicit `register-protocol` + IdP registration.
- **SC-003**: User can set a custom callback string like Postman; hosted default works end-to-end for at least one IdP smoke path; behavior for other strings is documented.

## Grill status

All blocking grill questions answered. Residual details (host domain, exact flag names, PEM vs PFX, OS matrix for protocol register) deferred to `/speckit.plan`.

| # | Topic | Decision |
|---|-------|----------|
| 1 | HTTPS cert | Tool param + interactive ask + colored output |
| 2 | Any address | Hosted capture URL in v1 |
| 3 | Handoff | `login` + thin `oauth-capture` |
| 4 | Scheme register | Explicit `register-protocol` only |
