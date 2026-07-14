# Quickstart & Validation: OAuth multi-mode capture

Maps to Spec 009 success criteria. Run from repo root after `dotnet build`.

## Prerequisites

- .NET 10 SDK; `api2skill` built or `dotnet run --project src/Api2Skill`
- Sample skill with oauth2 `authorization_code` profile (e.g. `debug/entra-oauth` playbook **without
  committing secrets**)
- For HTTPS loopback: create and trust a local PFX (or use PEM+key):
  `dotnet dev-certs https -ep ./dev.pfx -p pass --trust`
- Optional: stub relay (`hosting/oauth-relay` or test stub)

Alias for examples:

```bash
A2S="dotnet run --project src/Api2Skill --"
```

## Scenario A — HTTP loopback (SC-001)

```bash
$A2S oauth-capture --callback-url http://127.0.0.1:8400/callback --timeout 60 --json
# In another terminal: open a URL that redirects to that callback with ?code=x&state=y
```

**Expect**: JSON `ok:true`, `mode:"HttpLoopback"`, `code` set. Then:

```bash
$A2S login --skill ./path-to-skill --profile <oauth-profile>
```

**Expect**: `.auth-cache.json` updated; subsequent `dotnet run ./skill/scripts/call.cs -- <op>` uses token.

## Scenario B — HTTPS loopback + cert param (SC-001 / FR-006)

Set skill `auth.json` → `callbackUrl` to `https://127.0.0.1:8443/callback` (match IdP redirect URI).
HTTPS **requires** the tool (no in-script HTTPS fallback).

```bash
dotnet dev-certs https -ep ./dev.pfx -p pass --trust

$A2S oauth-capture \
  --callback-url https://127.0.0.1:8443/callback \
  --cert ./dev.pfx --cert-password pass \
  --timeout 60 --json
```

**Expect**: listener starts; browser redirect (trusting cert) yields JSON success.

Full login:

```bash
$A2S login --skill ./path-to-skill --profile <oauth-profile> \
  --cert ./dev.pfx --cert-password pass
```

PEM alternative: `--cert-pem ./cert.pem --cert-key ./key.pem` instead of `--cert` / `--cert-password`.

Non-TTY without `--cert`:

```bash
$A2S oauth-capture --callback-url https://127.0.0.1:8443/callback --json </dev/null
```

**Expect**: exit `2`, colored error on stderr (or plain if redirected), no hang.

## Scenario C — Explicit protocol registration (SC-002 / FR-009)

```bash
$A2S register-protocol
# IdP redirect URI: api2skill://oauth/callback
$A2S oauth-capture --callback-url 'api2skill://oauth/callback' --timeout 120 --json
```

**Expect**: after browser auth, OS launches handler; capture completes.

Without register:

```bash
$A2S unregister-protocol || true
$A2S oauth-capture --callback-url 'api2skill://oauth/callback' --json
```

**Expect**: exit `6`, colored instruction to run `register-protocol`.

## Scenario D — Hosted relay (SC-003 / FR-007)

```bash
export API2SKILL_OAUTH_RELAY_BASE=http://127.0.0.1:8787   # local stub
$A2S oauth-capture --mode hosted --callback-url "$API2SKILL_OAUTH_RELAY_BASE/v1/callback" --timeout 60 --json
# Simulate IdP: GET callback?sid=…&code=…&state=…
```

**Expect**: poll completes; `mode:"Hosted"`, `ok:true`.

## Scenario E — Generated skill handoff (FR-008)

Regenerate or use existing skill; run:

```bash
dotnet run ./skill/scripts/call.cs -- login <profile>
```

**Expect**: process invokes `api2skill oauth-capture` (PATH includes tool); cache written like today.

## Human review checkpoint

Do not treat CI green alone as done — walk A–D once on the developer’s OS and confirm colored
prompts are visible for cert/protocol paths.
