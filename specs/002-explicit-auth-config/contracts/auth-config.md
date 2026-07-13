# Contract: `auth.json` (committed auth configuration)

The committed, **secret-free** file that declares how a generated skill authenticates. Placed in
the skill directory next to `SKILL.md`. Structure only — every credential is a `{secret:NAME}`
reference resolved at call time from the git-ignored `secrets.json`.

## Top-level shape

```jsonc
{
  "profiles": [ /* AuthProfile, ordered, names unique */ ]
}
```

## AuthProfile

```jsonc
{
  "name": "string",                 // unique; used in `login <name>` and as cache key
  "type": "bearer|script|oauth2|basic|custom",
  "attach": { "scope": "global" },  // or { "scope": "tags", "tags": ["Admin","Billing"] }
  // exactly one type block below, matching "type":
}
```

`attach` default when omitted: `{ "scope": "global" }`. All applicable profiles apply to an
operation; a `tags` attachment whose tag matches no operation ⇒ **warning** (non-fatal).

### `bearer`

```jsonc
{ "type": "bearer", "token": "{secret:MY_TOKEN}" }
```
Sends `Authorization: <token>`, prepending `Bearer ` iff absent (case-insensitive), exactly once.

### `basic`

```jsonc
{ "type": "basic", "username": "{secret:USER}", "password": "{secret:PASS}" }
```
Sends `Authorization: Basic base64(user:pass)`.

### `custom`

```jsonc
{ "type": "custom",
  "headers": [
    { "name": "Authorization", "value": "{secret:GW_TOKEN}" },
    { "name": "ApiKey",        "value": "{secret:GW_KEY}" }
  ] }
```
Ordered; 1..n distinct header names.

### `script`

```jsonc
{ "type": "script",
  "command": "az account get-access-token --query accessToken -o tsv",
  "header": "Authorization",   // default "Authorization"
  "bearerPrefix": true }       // default false
```
Runs `command` fresh **each call**; trimmed stdout is the header value; non-zero exit **fails the
call** and surfaces stderr. `command` is user-controlled local execution (documented in SKILL.md).

### `oauth2`

```jsonc
{ "type": "oauth2",
  "grant": "authorization_code",          // or "client_credentials"
  "preset": "entra",                       // optional; only "entra"
  "tenant": "{tenant-id-or-domain}",       // entra preset input
  "authUrl":  "https://.../authorize",     // preset-filled for entra
  "tokenUrl": "https://.../token",         // preset-filled for entra
  "scopes": ["api://app-id/.default", "offline_access"],
  "callbackUrl": "http://localhost:8400/callback",
  "clientAuth": "body",                    // or "basic"
  "clientId": "{secret:CLIENT_ID}",
  "clientSecret": "{secret:CLIENT_SECRET}", // OPTIONAL (public client / PKCE)
  "authorizeRequest": { "headers": {}, "body": { "prompt": "consent" } },
  "tokenRequest":     { "headers": {}, "body": {} } }
```

Rules:
- `authorization_code` requires `authUrl` + `callbackUrl` (or an `entra` preset supplying them);
  runtime login adds **PKCE (S256)** and an anti-CSRF **`state`** automatically.
- `client_credentials` requires `tokenUrl`; it is fetched on demand at call time and is **not** a
  valid `login` target.
- `clientAuth: body` sends `client_id`/`client_secret` as form fields; `basic` sends
  `Authorization: Basic base64(id:secret)` and omits them from the body.
- `clientSecret` is optional — omit for public clients (PKCE-only authorization code).
- `authorizeRequest.body` — each key/value is merged as an extra **query parameter** on the
  authorize URL at `login` time. `authorizeRequest.headers` is accepted in the schema but has
  **no runtime effect** (browser navigation cannot attach HTTP headers).
- `tokenRequest.headers` — added to the HTTP request on token exchange and refresh POSTs.
- `tokenRequest.body` — each key/value is merged into the `application/x-www-form-urlencoded` body
  alongside standard OAuth fields. `client_id` from `clientId` is already sent; use `body` only for
  provider-specific extras or overrides. Values in `authorizeRequest` / `tokenRequest` are literals
  (not `{secret:…}`-resolved).
- `entra` preset: from `tenant`, fills `authUrl`/`tokenUrl` to
  `https://login.microsoftonline.com/{tenant}/oauth2/v2.0/{authorize|token}` and ensures
  `offline_access` ∈ `scopes`. Explicit `authUrl`/`tokenUrl`/`scopes` override the expansion.

Example — public client with custom token headers (CORS/origin rewrite) and authorize query param:

```jsonc
{ "profiles": [
  { "name": "user", "type": "oauth2", "grant": "authorization_code",
    "authUrl": "https://auth.example.com/oauth/authorize",
    "tokenUrl": "https://auth.example.com/oauth/token",
    "callbackUrl": "http://localhost:8400/callback",
    "clientId": "{secret:CLIENT_ID}",
    "scopes": ["openid", "offline_access"],
    "authorizeRequest": { "body": { "prompt": "consent" } },
    "tokenRequest": {
      "headers": { "Origin": "https://app.example.com", "X-Rewrite-Origin": "https://app.example.com" },
      "body": { "resource": "my-api" }
    } } ] }
```

## Secret references

Any string value may be `{secret:NAME}`; resolved at call time from `secrets.json[NAME]`. Missing
at call time ⇒ the call fails naming `NAME` (never sends an empty credential). The generator
scaffolds every referenced `NAME` into `secrets.example.json` as an empty placeholder.

## Validation (generation time; failure ⇒ exit 5, no output)

1. Valid JSON; `profiles` non-empty (if the file is supplied).
2. Unique, non-empty `name`; known `type`; type block matches `type`.
3. oauth2 per-grant required fields present (post-preset).
4. **No duplicate header name** across the profiles applicable to any single operation
   (FR-021a) — names the header + the two profiles.

Non-fatal warnings: `tags` attachment matching no operation.
