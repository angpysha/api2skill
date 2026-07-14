# Phase 1 Data Model: App-owned OAuth redirect capture

**Feature**: `009-oauth-https-callback` · **Plan**: [plan.md](./plan.md)

App-side types for capture + handoff. Skill-side `auth.json` OAuth fields (`callbackUrl`,
`browserLaunch`, `tokenField`, etc.) are unchanged from specs/002 (+007); this feature adds
**runtime capture** entities and CLI option shapes.

---

## 1. CaptureMode

| Value | Trigger |
|-------|---------|
| `HttpLoopback` | `http://` + loopback host (`localhost`, `127.0.0.1`, `[::1]`) |
| `HttpsLoopback` | `https://` + loopback host |
| `CustomScheme` | URI scheme not `http`/`https` |
| `Hosted` | Non-loopback `https://` on configured relay host, or explicit `--mode hosted` / default relay URL |
| *(error)* | Other absolute URLs → validation failure before listen |

---

## 2. CaptureOptions

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `CallbackUrl` | `Uri` / string | required | From `--callback-url` or profile `callbackUrl` |
| `Mode` | `CaptureMode?` | inferred | Optional override |
| `Timeout` | `TimeSpan` | 180s | `--timeout` |
| `CertPath` | `string?` | null | `--cert` PFX |
| `CertPassword` | `string?` | null | `--cert-password` or prompted |
| `CertPemPath` | `string?` | null | `--cert-pem` |
| `CertKeyPath` | `string?` | null | `--cert-key` |
| `RelayBaseUrl` | `string?` | built-in / env | `API2SKILL_OAUTH_RELAY_BASE` |
| `State` | `string?` | null | Expected `state` for validation when present |
| `ExpectedPath` | `string?` | from callback | Path match for loopback |

Validation:

- `HttpsLoopback` requires PFX **or** (PEM+key); missing on non-TTY → error.
- `Hosted` requires reachable `RelayBaseUrl`.
- `CustomScheme` requires OS registration for first-party scheme; other schemes documented as “OS must route to us”.

---

## 3. CaptureResult

Stdout JSON for `oauth-capture` (see [contracts/oauth-capture.md](./contracts/oauth-capture.md)).

| Field | Type | Notes |
|-------|------|-------|
| `ok` | `bool` | |
| `mode` | `string` | enum name |
| `code` | `string?` | authorization code |
| `state` | `string?` | echoed |
| `error` | `string?` | OAuth error or transport message |
| `errorDescription` | `string?` | optional |
| `callbackUrl` | `string` | effective URL used |

---

## 4. CertMaterial

| Field | Type | Notes |
|-------|------|-------|
| `Kind` | `Pfx` \| `Pem` | |
| `PfxBytes` / paths | — | loaded at start of HTTPS capture |
| `Password` | `string?` | never written to disk by tool |

---

## 5. HostedSession (relay + client)

| Field | Type | Notes |
|-------|------|-------|
| `SessionId` | `string` | opaque |
| `State` | `string` | OAuth `state` |
| `CreatedUtc` | `DateTimeOffset` | |
| `ExpiresUtc` | `DateTimeOffset` | ≤ Created + 5 minutes |
| `Code` | `string?` | set on callback |
| `Error` | `string?` | set on callback |
| `Consumed` | `bool` | true after successful poll |

State transitions: `Created → (Pending) → Completed | Expired | Consumed`.

---

## 6. LoginCommandOptions

| Field | Type | Notes |
|-------|------|-------|
| `SkillDir` | `string` | `--skill` |
| `Profile` | `string?` | default: first `authorization_code` profile or required |
| (capture flags) | — | forwarded: cert, timeout, relay base |

Uses existing generator auth domain (`AuthConfig` / `OAuthSettings`) by reading skill
`auth.json` + `secrets.json` at runtime (reuse `AuthConfigLoader` patterns — **extend**, do not
fork).

---

## 7. ProtocolRegistration

| Field | Type | Notes |
|-------|------|-------|
| `Scheme` | `string` | default `api2skill` |
| `HandlerPath` | `string` | resolved tool binary |
| `Registered` | `bool` | query status if implementable |

No durable app config file required for v1.
