# Phase 1 Data Model: Explicit Auth Configuration

**Feature**: `002-explicit-auth-config` · **Plan**: [plan.md](./plan.md)

Two layers: the **generator-side config domain** (parsed from `auth.json`, validated, resolved)
and the **model additions** consumed by emitters. The **runtime dispatcher** owns a small token-
cache shape (documented in [contracts/dispatcher-auth.md](./contracts/dispatcher-auth.md)).

---

## 1. Auth config domain (generator, `src/Api2Skill/Auth/`)

### AuthConfig (root)

| Field | Type | Notes |
|-------|------|-------|
| `Profiles` | `IReadOnlyList<AuthProfile>` | Ordered; names unique (case-sensitive). ≥1 required if file present. |

Validation:
- Profile `name` unique and non-empty; duplicate ⇒ error.
- Unknown `type` ⇒ error (exit 5).
- Every `{secret:NAME}` reference is collected for scaffolding (missing key is a **call-time**
  failure, not generation — the generator never has real secrets).

### AuthProfile

| Field | Type | Notes |
|-------|------|-------|
| `Name` | `string` | Unique profile id, used in `login <name>` and cache keys. |
| `Type` | `AuthType` | `Bearer` \| `Script` \| `OAuth2` \| `Basic` \| `Custom`. |
| `Attach` | `Attachment` | Default global. |
| `Bearer` / `Script` / `OAuth` / `Basic` / `Custom` | type-specific settings (below) | Exactly one populated, matching `Type`. |

`AuthType` (enum): `Bearer, Script, OAuth2, Basic, Custom`. Unknown string ⇒ validation error.

### Attachment

| Field | Type | Notes |
|-------|------|-------|
| `Scope` | `AttachScope` | `Global` (default) \| `Tags`. |
| `Tags` | `IReadOnlyList<string>` | Required when `Scope=Tags`; a tag matching **no** operation ⇒ **warning** (FR-021), generation continues. |

Resolution rule (FR-004/005/006): an operation's applicable profiles = all `Global` profiles ∪
all `Tags` profiles whose tag set intersects the operation's tags. **All** apply (FR-005).
Explicit profiles **replace** spec-derived schemes for operations they cover (FR-006); operations
with **no** applicable profile keep the existing spec-derived `SecuritySchemeIds` path.

### Type-specific settings

**BearerSettings** (FR-009)

| Field | Type | Notes |
|-------|------|-------|
| `Token` | `string` (secret ref) | Sent as `Authorization`; `Bearer ` prepended iff absent (case-insensitive), once. |

**BasicSettings** (FR-010)

| Field | Type | Notes |
|-------|------|-------|
| `Username` | `string` (secret ref) | |
| `Password` | `string` (secret ref) | `Authorization: Basic base64(user:pass)`. |

**CustomSettings** (FR-011)

| Field | Type | Notes |
|-------|------|-------|
| `Headers` | ordered `IReadOnlyList<HeaderEntry>` | 1..n; each `{ Name, Value }`, `Value` = secret ref or literal. Multiple distinct names (e.g. `Authorization` + `ApiKey`) supported. |

**ScriptSettings** (FR-012)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `Command` | `string` | — | Executed fresh **each call**; trimmed stdout = header value. User-controlled local exec. |
| `Header` | `string` | `Authorization` | Target header name. |
| `BearerPrefix` | `bool` | `false` | If true, prepend `Bearer ` when absent (FR-009 rule). |
| Non-zero exit | — | — | Call **fails**, surfacing stderr. |

**OAuthSettings** (FR-013/014/015, FR-016a)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `Grant` | `authorization_code` \| `client_credentials` | `authorization_code` | |
| `Preset` | `string?` (`entra`) | none | Expands endpoints + adds `offline_access`. |
| `Tenant` | `string?` | — | Entra preset input (id/domain/`common`/`organizations`/`consumers`). |
| `AuthUrl` | `string?` | preset-filled | Authorize endpoint (auth-code only). |
| `TokenUrl` | `string` | preset-filled | Token endpoint. |
| `Scopes` | `IReadOnlyList<string>` | `[]` (+`offline_access` for entra) | |
| `CallbackUrl` | `string` | `http://localhost:8400/callback` | Loopback; overridable. |
| `ClientAuth` | `body` \| `basic` | `body` | Postman "Client Authentication". |
| `ClientId` | `string` (secret ref) | — | |
| `ClientSecret` | `string?` (secret ref) | — | **Optional** (public client / PKCE). |
| `AuthorizeRequest` | `{ Headers{}, Body{} }` | empty | Extra params on authorize (Postman parity). |
| `TokenRequest` | `{ Headers{}, Body{} }` | empty | Extra params on token/refresh (Postman parity). |

Validation: `authorization_code` requires `AuthUrl` (or a preset that supplies it) and
`CallbackUrl`; `client_credentials` requires `TokenUrl` and forbids `login` (interactive) —
`login` on such a profile is reported "not applicable" (edge case). `state` + PKCE are runtime
concerns, not config.

---

## 2. Model additions (consumed by emitters)

### OperationModel (extend)

Add `IReadOnlyList<string> AuthProfileNames` — the resolved, ordered profile names applicable to
this operation (empty ⇒ fall back to spec-derived `SecuritySchemeIds`). Existing fields unchanged.

### SkillModel (extend)

Add `AuthConfig? AuthConfig` — the validated config to copy into the skill dir and drive
`secrets.example.json`. When null, behavior is exactly today's spec-derived auth.

Collision invariant (FR-021a), checked in `AttachmentResolver` before emit: for every operation,
the union of headers produced by its applicable profiles has **no duplicate header name**;
otherwise generation fails (exit 5) naming the header and the two profiles. (`custom` header
names, `Authorization` from bearer/basic/oauth/script are all considered.)

---

## 3. Runtime token cache (dispatcher; see contracts/dispatcher-auth.md)

`.auth-cache.json` — object keyed by profile name:

| Field | Type | Notes |
|-------|------|-------|
| `access_token` | `string` | |
| `expires_at` | `string` (UTC ISO-8601) | Absolute; compared with a small skew margin. |
| `refresh_token` | `string?` | Present iff provider returned one (`offline_access`). |

Git-ignored; POSIX `0600`; all read-modify-write under a `.auth-cache.json.lock` (`FileShare.None`)
with a post-lock validity re-check (FR-019a). Never written to a committed file (Constitution IV).

---

## 4. Secrets scaffold (extend)

`SecretRefScanner` collects the distinct `{secret:NAME}` names across all profiles;
`SecretsScaffold` adds them as empty placeholders in `secrets.example.json` (in addition to
spec-derived scheme keys), always placeholders — never real values.

---

## State & lifecycle

- **Config**: static per generation; copied verbatim (byte-stable).
- **Access token**: `absent → acquired (login/client_creds) → valid → expired → refreshed → …`;
  terminal `unrefreshable` ⇒ call fails with re-login instruction.
- **Refresh token**: rotated in place under lock when the provider returns a new one.
