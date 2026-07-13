# Quickstart & Validation: Explicit Auth Configuration

End-to-end scenarios that prove the feature. Each maps to spec acceptance criteria. Run from repo
root; `SKILL=./out` is the generated skill dir.

## Prerequisites

- .NET 10 SDK (for `generate` and the `.cs`/`.fsx` dispatchers); `dotnet-script` for `.csx`.
- A sample spec (`tests/.../fixtures/petstore.json`) and, for OAuth scenarios, a stub IdP (the
  integration harness provides one) or a real Entra app registration with an `http://localhost`
  redirect.

## Scenario A — Manual bearer overrides the spec (US1 / FR-009, FR-006)

```bash
api2skill generate ./petstore.json --auth bearer --out ./out
# fill the scaffolded key:
#   echo '{"MY_TOKEN":"abc123"}' > ./out/secrets.json   # (git-ignored)
dotnet run ./out/scripts/call.cs -- listPets
```

**Expect**: request carries `Authorization: Bearer abc123` (prefix added once) even though the
spec declared a different/no scheme. A token already starting with `Bearer ` is sent unchanged.

## Scenario B — Two credentials at once (US2 / FR-005, FR-011)

`auth.json`:

```jsonc
{ "profiles": [
  { "name": "gw", "type": "custom",
    "headers": [ { "name": "ApiKey", "value": "{secret:GW_KEY}" } ] },
  { "name": "user", "type": "bearer", "token": "{secret:USER_TOKEN}" }
] }
```

```bash
api2skill generate ./petstore.json --auth-config ./auth.json --out ./out
# secrets.json: { "GW_KEY": "...", "USER_TOKEN": "..." }
dotnet run ./out/scripts/call.cs -- listPets
```

**Expect**: both `ApiKey` and `Authorization: Bearer …` present. If two profiles set the **same**
header on one operation, `generate` instead **fails with exit 5** naming the header.

## Scenario C — Per-tag scoping (US2 / FR-004)

Attach a profile to `{ "scope": "tags", "tags": ["Admin"] }`; confirm only `Admin` operations
carry it and non-Admin operations do not. A tag matching no operation prints a **warning** but
generation succeeds.

## Scenario D — Interactive OAuth / Entra (US3 / FR-013..019a)

`auth.json`:

```jsonc
{ "profiles": [
  { "name": "aad", "type": "oauth2", "preset": "entra",
    "tenant": "{secret-free-tenant-id}",
    "scopes": ["api://<app>/.default", "offline_access"],
    "clientId": "{secret:CLIENT_ID}" } ] }   // public client, no secret
```

```bash
api2skill generate ./api.json --auth-config ./auth.json --out ./out   # (optionally add --login)
# secrets.json: { "CLIENT_ID": "..." }
dotnet run ./out/scripts/call.cs -- login aad     # browser opens; sign in
dotnet run ./out/scripts/call.cs -- someOperation # uses cached token, no browser
```

**Expect**:
- login opens the browser (or prints the URL when headless), validates `state`, exchanges the code
  with PKCE, and writes `.auth-cache.json` (mode `0600`, git-ignored).
- subsequent calls reuse the token; when expired, they refresh silently via `offline_access`.
- with no usable refresh token, a call **fails** with `run: … -- login aad` and launches no
  browser.

## Scenario E — Token from a script (US4 / FR-012)

```jsonc
{ "profiles": [ { "name": "az", "type": "script",
  "command": "printf 'tok-123'", "header": "Authorization", "bearerPrefix": true } ] }
```

**Expect**: each call runs the command; request carries `Authorization: Bearer tok-123`. A command
that exits non-zero fails the call and surfaces stderr.

## Scenario F — Cross-emitter parity (FR-023)

Repeat Scenario A/B for `--script fsx` and `--script csx`; behavior is identical.

## Scenario G — Concurrency (FR-019a)

Invoke two oauth2 calls in parallel with an expired token; confirm exactly one refresh occurs (the
file lock serializes; the second reuses the freshly stored token) and `.auth-cache.json` is intact.

## Scenario H — OAuth custom authorize query params and token headers/body (US3 / FR-014)

Use when an IdP needs extra authorize URL parameters, custom headers on the token POST (e.g.
CORS/origin rewrite), or additional form fields on token exchange / refresh.

`auth.json`:

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

```bash
api2skill generate ./api.json --auth-config ./auth.json --out ./out --login
# secrets.json: { "CLIENT_ID": "..." }
dotnet run ./out/scripts/call.cs -- someOperation
```

**Expect**:

- `login` builds an authorize URL with `prompt=consent` (and PKCE + `state` as usual).
- Token exchange POST includes the custom `Origin` / `X-Rewrite-Origin` headers and the extra
  `resource` form field, plus the standard OAuth fields (`grant_type`, `code`, `code_verifier`,
  `client_id`, …).
- `authorizeRequest.headers` are ignored (browser navigation cannot carry custom HTTP headers).

See also [wiki/Authentication.md](../../../wiki/Authentication.md) for the full OAuth customization
reference.

## Automated coverage

- **Unit**: `tests/Api2Skill.Tests/Auth/` — loader/validation, collision (exit 5), entra preset,
  secret-ref scan, PKCE/state helpers, bearer-prefix rule.
- **Golden**: `tests/Api2Skill.Tests/Emit/` — per-emitter auth-engine snapshots.
- **Integration**: `tests/Api2Skill.Tests/Integration/` — each dispatcher vs. stub IdP + stub API:
  bearer/basic/custom/script application, full auth-code login, silent refresh, re-login failure
  message, and the concurrency lock.
