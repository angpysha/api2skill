# Contract: Generated dispatcher `login` handoff

Delta to [specs/002-explicit-auth-config/contracts/dispatcher-auth.md](../../002-explicit-auth-config/contracts/dispatcher-auth.md).

## Behavior change

For `authorization_code` interactive `login <profile>`:

1. Build authorize URL + PKCE as today (unchanged).
2. **Capture** via external tool when available:

   ```text
   api2skill oauth-capture --callback-url <profile.callbackUrl> --state <state> --json
     [+ cert flags if https]
   ```

3. Parse [oauth-capture.md](./oauth-capture.md) JSON; exchange code; write `.auth-cache.json`
   (unchanged cache schema / locking).

## Fallback

| Mode needed | Tool missing |
|-------------|--------------|
| HTTP loopback | Keep existing in-script `HttpListener` path |
| HTTPS / custom scheme / hosted | Fail with message: install/upgrade `api2skill` and retry |

## `api2skill login --skill`

Equivalent to running the skill’s login for one profile **inside the tool** (load auth + secrets,
capture, token exchange, cache write) so humans need not remember dispatcher script paths. Must
produce the same `.auth-cache.json` shape as scripted login.

## Docs in SKILL.md

Auth section gains 3–5 lines pointing to:

- `api2skill login --skill .`
- `api2skill register-protocol` when using `api2skill://…`
- Hosted default callback URL for non-loopback HTTPS
