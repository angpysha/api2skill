# Entra OAuth callback — live debug session

Use your **existing Entra app registration** and your user account. No release until login works end-to-end on your machine.

Branch: `debug/oauth-callback-entra`

## 1. Entra app registration checklist

In [Microsoft Entra admin center](https://entra.microsoft.com/) → **App registrations** → your app:

| Setting | Required value |
|---------|----------------|
| **Redirect URI** (platform: **Mobile and desktop applications**) | `http://localhost:8400/callback` — must match **exactly** (scheme, host, port, path) |
| **Allow public client flows** | **Yes** (PKCE public client — no `clientSecret` in api2skill) |
| **Supported account types** | Whatever matches your test user (single tenant vs multitenant) |

Copy from the app overview:

- **Application (client) ID** → `secrets.json` → `CLIENT_ID`
- **Directory (tenant) ID** → `auth.json` → `tenant` (or use your `*.onmicrosoft.com` domain)

**Scopes:** start minimal in `auth.json.example`:

```json
"scopes": ["openid", "profile", "offline_access"]
```

If you need a custom API scope later, add e.g. `api://<app-id>/.default` once basic login works.

## 2. Local config (git-ignored)

From repo root:

```bash
cd debug/entra-oauth
cp auth.json.example auth.json
cp secrets.example.json secrets.json
# Edit auth.json: tenant, scopes (if needed)
# Edit secrets.json: real CLIENT_ID
```

Do **not** commit `auth.json` or `secrets.json`.

## 3. Generate a test skill

Use the built-in generator (no NuGet publish required):

```bash
# from repo root
dotnet run --project src/Api2Skill -- \
  generate tests/Api2Skill.Tests/fixtures/petstore.json \
  --auth-config debug/entra-oauth/auth.json \
  --out debug/entra-oauth/skill \
  --force
```

## 4. Run login — what to watch

```bash
dotnet run debug/entra-oauth/skill/scripts/call.cs -- login entra
```

**Expected sequence:**

1. Terminal: `Listening for OAuth callback on http://localhost:8400/callback ...`
2. Browser opens (or URL printed — open it manually)
3. Sign in with your Entra user
4. Browser redirects to `http://localhost:8400/callback?code=...&state=...`
5. Browser shows: *Login complete — you can close this window…*
6. Terminal: `Login succeeded for profile 'entra'.`
7. File created: `debug/entra-oauth/skill/.auth-cache.json`

## 5. Capture evidence when it fails

Paste back (redact tokens if any appear):

1. **Full terminal output** from `login entra` (stdout + stderr)
2. **Browser address bar** after redirect (you can redact `code=` value; keep `error=` / `error_description=` if present)
3. **What the browser shows** (blank, spinning, error page, “can’t connect”, success page, etc.)
4. **Entra redirect URI** exactly as registered (screenshot or copy-paste)
5. Whether port `8400` is free: `lsof -i :8400` (macOS)

Optional quick listener check (no Entra):

```bash
# Terminal A
python3 -c "
from http.server import HTTPServer, BaseHTTPRequestHandler
class H(BaseHTTPRequestHandler):
    def do_GET(self):
        print('GOT', self.path)
        self.send_response(200); self.end_headers()
        self.wfile.write(b'ok')
    def log_message(self, *a): pass
HTTPServer(('127.0.0.1', 8400), H).serve_forever()
"

# Terminal B — browser should show 'ok'
open 'http://localhost:8400/callback?code=test&state=test'
```

If Python receives the request but api2skill does not, the bug is in the generated dispatcher. If neither receives it, the issue is OS/browser/network.

## 6. After login works

```bash
dotnet run debug/entra-oauth/skill/scripts/call.cs -- listPets
```

Confirms cached token is used on a real API call (petstore base URL is fake — a 404/connection error is fine; we care that **Authorization** is sent).

## 7. Release policy

- **No version bump / tag** until you confirm step 4 succeeds on your Mac with your Entra app.
- Fixes go on this branch first; PR only after your live test passes.
