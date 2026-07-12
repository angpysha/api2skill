# Contract: Generated dispatcher — auth runtime & `login`

Behavior the emitted dispatcher (`scripts/call.*`) must exhibit, identically across the `cs`,
`fsx`, and `csx` emitters (FR-023). The auth engine is **API-independent** and reads `auth.json`
+ `secrets.json` at runtime.

**Path resolution**: `auth.json`, `secrets.json`, `.auth-cache.json`, and `.auth-cache.json.lock`
all resolve the same way as today's `secrets.json` lookup — relative to the script's own file
location (`[CallerFilePath]` → `../`), not the process's working directory or the `dotnet run`
build-cache path (`AppContext.BaseDirectory` is unreliable for file-based apps — see
`CsFileEmitter`'s existing doc comment).

## Subcommands

| Invocation | Behavior |
|------------|----------|
| `<operationId> [--<param> …] [--body …]` | Existing call path, now applying explicit profiles first (override) and falling back to spec-derived schemes only for operations with no applicable profile. |
| `login <profile>` | Interactive OAuth for an `authorization_code` profile (below). |
| `login` (no profile) / unknown profile | Lists login-capable profiles; non-zero exit. |
| `login <client_credentials profile>` | Prints "not applicable — this profile obtains tokens automatically at call time"; non-zero exit. |

## Per-call auth application

1. Resolve the called operation's applicable profiles (from its emitted profile-name list).
2. For each, in order, contribute headers/query:
   - **bearer**: `Authorization` (+`Bearer ` if absent).
   - **basic**: `Authorization: Basic …`.
   - **custom**: each declared header.
   - **script**: run command; trimmed stdout → header (+optional `Bearer `); non-zero exit ⇒ the
     **call fails** surfacing stderr.
   - **oauth2**: obtain a valid access token (cache/refresh below) → `Authorization: Bearer …`.
3. A `{secret:NAME}` unresolved in `secrets.json` ⇒ the **call fails** naming `NAME` (never sends
   an empty credential).
4. The operation call path **never launches a browser** (may run non-interactively).

## `login <profile>` (authorization_code)

1. Generate PKCE `code_verifier` (43–128 unreserved chars) + `code_challenge` (S256) and a random
   `state` (`RandomNumberGenerator`).
2. Start an `HttpListener` on the profile's `callbackUrl` (loopback). If the port is in use ⇒
   report the conflict and how to change `callbackUrl`; non-zero exit.
3. Open the browser to `authUrl` with `response_type=code`, `client_id`, `redirect_uri`, `scope`,
   `code_challenge`, `code_challenge_method=S256`, `state`, + any `authorizeRequest` extras. If no
   browser can be launched (headless) ⇒ **print the URL** to open manually and keep listening.
4. On redirect: if returned `state` ≠ sent `state` ⇒ **reject** (no token stored), report possible
   CSRF/mix-up. Otherwise exchange `code` at `tokenUrl` (POST form: `grant_type=authorization_code`,
   `code`, `redirect_uri`, `code_verifier`, client auth per `clientAuth`, + `tokenRequest` extras).
5. Store `{ access_token, expires_at, refresh_token? }` under the profile name (write flow below).
6. Print success; the browser tab shows a minimal "you can close this window" page.

## Token cache (`.auth-cache.json`)

- Location: next to `secrets.json`; git-ignored; POSIX mode `0600` (Windows: user-profile ACL,
  best-effort).
- Shape: object keyed by profile name → `{ access_token, expires_at (UTC ISO-8601), refresh_token? }`.
- **Concurrency (FR-019a)**: every read-modify-write holds an inter-process lock
  (`.auth-cache.json.lock`, `FileShare.None`, bounded retry). After acquiring the lock, **re-check**
  validity and reuse a token another process just refreshed. Writes go to a temp file then atomic
  `File.Move`; a rotated `refresh_token` replaces the old one in place.

## Refresh & failure (FR-019)

On a call needing an oauth2 token:
- valid `access_token` (with skew margin) ⇒ use it, no network.
- expired + `refresh_token` present ⇒ POST `grant_type=refresh_token` (client auth per `clientAuth`,
  + `tokenRequest` extras); on success store + use; provider may rotate the refresh token.
- expired + no/absent refresh, **or** refresh fails ⇒ **fail the call** with:
  `run: dotnet run <script> -- login <profile>` (no browser launched).
- `client_credentials`: fetch on demand (`grant_type=client_credentials`), cache like above.

## TLS

`--insecure`/`API2SKILL_INSECURE` continues to gate acceptance of untrusted certificates — and now
also applies to OAuth `authUrl`/`tokenUrl` calls. Off by default (dev-only, labeled in SKILL.md).
