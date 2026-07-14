# Research: App-owned OAuth redirect capture (multi-mode)

**Feature**: `009-oauth-https-callback`  
**Date**: 2026-07-14  
**Discovery mode**: **DEGRADED** — `mcp-codebase-search` unavailable; `graphify` failed (no LLM API key). Existing OAuth loopback path confirmed via Grep/Read on emitters (`BeginCallbackListener` / `HttpListener` in `CsFileEmitter`, `CsxEmitter`, `FsxEmitter`).

## Postman (reference)

Postman typically does **not** bind local HTTPS for OAuth:

1. **Hosted callbacks**: `https://oauth.pstmn.io/v1/callback` (desktop), `https://oauth.pstmn.io/v1/browser-callback` (web).
2. **Intercept**: Desktop/web app observes the IdP redirect and extracts `code` / tokens.
3. Custom Callback URL fields are mainly for IdP allow-list match; capture stays app-controlled.

## Current api2skill baseline

- Interactive login lives **inside generated skill scripts** (`login <profile>`).
- Capture = `HttpListener` on `callbackUrl` (default `http://localhost:8400/callback`), dual `localhost`/`127.0.0.1` prefixes, listener started before browser.
- Token exchange + `.auth-cache.json` also live in the script.
- CLI today: `generate`, `update`, `install-creator` — **no** first-class `login` / `oauth-capture` / `register-protocol`.

## Decisions (Phase 0 — all grill + plan clarifications resolved)

### Decision: Capture ownership

**Choice**: Move redirect **capture** into the **api2skill app**. Generated skill (and `api2skill login --skill`) call the app; token exchange + `.auth-cache.json` stay skill-owned (or owned by `login --skill` which uses the same skill paths).

**Rationale**: Local HTTPS TLS, custom URL schemes, and hosted relay cannot ship cleanly as BCL-only script text (Constitution II for emitters; HTTPS cert binding is OS-specific). App can own platform integrations.

**Alternatives considered**: HTTPS-only HttpListener inside scripts (rejected); WebView embed (too heavy for `dotnet tool`).

### Decision: Capture mode routing

**Choice**: Derive mode from `callbackUrl` (or explicit `--mode`):

| Mode | When | Mechanism |
|------|------|-----------|
| `http-loopback` | `http://` + loopback host | Existing HttpListener pattern, in **app** |
| `https-loopback` | `https://` + loopback host | HTTPS listener in **app** + cert param |
| `custom-scheme` | non-http(s) scheme | Wait for OS protocol invocation / second process handoff |
| `hosted` | non-loopback `https://` matching configured relay host, **or** `--mode hosted` / sentinel default URL | Poll / receive from hosted relay |
| `unsupported` | other absolute URLs we cannot receive | Fail fast with colored error + docs (loopback, scheme, or hosted default) |

**Rationale**: Matches Postman UX (any string in the field) while being honest about what the local app can receive.

### Decision: HTTPS certificate tool parameters (FR-006)

**Choice**:

| Flag | Meaning |
|------|---------|
| `--cert <path>` | Prefer **PFX/PKCS#12** for local HTTPS bind |
| `--cert-password <pwd>` | Optional; if PFX needs password and unset → **colored interactive prompt** (TTY) |
| `--cert-pem <path>` + `--cert-key <path>` | Alternative to PFX (plan allows both) |

Non-TTY without required material → fail fast (exit code documented in contracts). Trust warnings use **yellow/red** `Console.ForegroundColor` (no Spectre — keep AOT/trim surface small, same as `ConsoleMultiSelect`).

**Local HTTPS stack in the app**: prefer **Kestrel** (or `HttpListener` where sufficient) **only inside `src/Api2Skill`**, never in emitted script text. Exact stack recorded during implement; must bind the loopback URL and extract query `code`/`state`/`error`.

**Rationale**: User asked for a tool parameter + visible ask; PFX is the path of least resistance for .NET HTTPS listen; PEM kept for mkcert-style workflows.

**Alternatives considered**: Ship only `dotnet` developer certificate (too opaque / Windows-centric); mkcert-only (external dep).

### Decision: Hosted capture URL in v1 (FR-007)

**Choice**: Ship a **hosted OAuth relay** (Postman analogue):

1. CLI starts a capture session → relay returns a **callback URL** (or user pastes the documented default into IdP + session id in `state`/path).
2. Browser redirects to hosted page with `?code=&state=` (and/or errors).
3. Relay stores **authorization code (and error) only**, keyed by `state`/session id, **TTL ≤ 5 minutes**. Never stores tokens, refresh tokens, or client secrets.
4. CLI **polls** (or long-polls) until result or timeout.
5. Optional: hosted page also offers `api2skill://…` deep link when protocol registered.

**Default public URL**: `https://oauth.api2skill.dev/v1/callback` (or env override `API2SKILL_OAUTH_RELAY_BASE`). Until DNS exists, implement against a **Worker/Azure Function URL** constant + override.

**Infra artefact**: `hosting/oauth-relay/` (minimal Worker **or** Azure Functions HTTP endpoints: `POST /v1/session`, `GET /v1/callback`, `GET /v1/poll`). Deploy docs in wiki; CI may smoke against a stub.

**Privacy**: no durable logs of query strings in prod config guidance; codes deleted after successful poll or TTL.

**Alternatives considered**: Hosted later (rejected by grill B); pasted code only (kept as manual fallback for headless, not primary).

### Decision: CLI shape (FR-008)

**Choice C**:

| Command | Role |
|---------|------|
| `api2skill oauth-capture` | Thin capture only → prints JSON result on stdout (code/state/error/mode) |
| `api2skill login --skill <dir> [--profile <name>]` | End-to-end: load skill `auth.json`/`secrets.json`, PKCE, browser or clipboard, capture, token exchange, write skill `.auth-cache.json` |
| Generated `login` | Shells out to `api2skill oauth-capture` when tool on PATH; **HTTP loopback in-script remains emergency fallback** for environments without the tool (document deprecation intent) |

Exact JSON schema in `contracts/oauth-capture.md`.

### Decision: Protocol registration (FR-009)

**Choice C — explicit only**:

- `api2skill register-protocol` — register first-party scheme `api2skill` (handler path = current tool binary / shim).
- `api2skill unregister-protocol` — optional but **in scope** for clean uninstall docs.
- Never auto-register on install or first login.
- Unregistered scheme use → colored error instructing `register-protocol`.

**OS matrix (v1)**:

| OS | Mechanism (implement best-effort) |
|----|-----------------------------------|
| macOS | LaunchServices / `LSRegister` helper or documented `.app` shim if required |
| Windows | HKCU URL protocol registration |
| Linux | `~/.local/share/applications/*.desktop` + `xdg-mime` |

Unsupported platform → clear message, non-zero exit.

### Decision: Versioning

**Choice**: treat as **minor** (`0.5.x` → `0.6.0`) — new user-facing CLI commands and capture modes. Exact bump at PR per `version-bump` rule.

## Architecture sketch

```text
[IdP / browser]
      |  redirect ?code=&state=
      v
┌─────────────────────────────────────────────┐
│ api2skill oauth-capture / login --skill     │
│  http-loopback | https-loopback+cert        │
│  custom-scheme | hosted-relay poll          │
└─────────────────────────────────────────────┘
      |  CaptureResult { code, state, error }
      v
[token exchange + .auth-cache.json in skill dir]
```

## Decision log (grill + plan)

| Decision | Choice | Date |
|----------|--------|------|
| Primary fix = local HTTPS HttpListener only | No | 2026-07-14 |
| Capture logic in api2skill app | Yes | 2026-07-14 |
| Supported kinds | HTTP + HTTPS **mandatory**; first-party + **other** custom schemes; Postman-like arbitrary Callback URL | 2026-07-14 |
| Non-loopback HTTPS | **B — hosted relay in v1** | 2026-07-14 |
| HTTPS cert | `--cert` / PEM pair; interactive colored ask; fail fast non-TTY | 2026-07-14 |
| Handoff CLI | **C** — `login --skill` + `oauth-capture` | 2026-07-14 |
| Scheme OS registration | **C — explicit** `register-protocol` (+ unregister) | 2026-07-14 |
| Cert format | PFX primary; PEM+key alternative | 2026-07-14 |
| Hosted privacy | code/error only, ≤5m TTL, poll API | 2026-07-14 |
| Colored output | BCL `Console.ForegroundColor` (no Spectre) | 2026-07-14 |
| Local HTTPS runtime | In **app** only (Kestrel or equivalent), not in emitters | 2026-07-14 |
