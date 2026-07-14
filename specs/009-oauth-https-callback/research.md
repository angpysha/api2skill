# Research: App-owned OAuth redirect capture

**Feature**: `009-oauth-https-callback` (title outdated; scope pivoted)  
**Date**: 2026-07-14

## Postman (reference)

Postman typically does **not** bind local HTTPS for OAuth:

1. **Hosted callbacks**: `https://oauth.pstmn.io/v1/callback` (desktop), `https://oauth.pstmn.io/v1/browser-callback` (web).
2. **Intercept**: Desktop/web app observes the IdP redirect (Location / embedded flow) and extracts `code` / tokens; may not need the user’s local server at all.
3. Custom Callback URL fields are mainly for IdP allow-list match; capture is still in Postman’s control.

Sources: Postman OAuth 2.0 docs; Postman Community “authorization-code work”; Stack Overflow “handle localhost OAuth 2 redirects”.

## api2skill options after pivot

| Mechanism | Local bind? | HTTPS without local cert? | Fit for CLI tool |
|-----------|-------------|---------------------------|------------------|
| Custom URI scheme `api2skill://…` | No listen | Yes (OS handles) | Good; needs install/register |
| Hosted page (our infra) | No | Yes | Needs hosting + privacy |
| Embedded WebView | No OS listen | N/A | Heavy for `dotnet tool` |
| HTTP loopback listener | Yes | N/A | Already works; Entra allows `http://localhost` for public clients |
| Local HTTPS + cert | Yes | Needs cert | Deferred |

## Architecture sketch (user direction)

```
[browser / IdP]
      |
      |  redirect with ?code=&state=
      v
[api2skill app capture]  ----handoff---->  [token exchange + .auth-cache.json in skill]
      ^
      |
[skill login]  or  [api2skill login --skill …]
```

Generated skill should **call** app capture, not reimplement HTTPS TLS.

## Decision log

| Decision | Choice | Date |
|----------|--------|------|
| Primary fix = local HTTPS HttpListener only | No | 2026-07-14 |
| Capture logic in api2skill app | Yes | 2026-07-14 |
| Supported kinds | HTTP + HTTPS **mandatory**; first-party + **other** custom schemes; Postman-like arbitrary Callback URL | 2026-07-14 |
| How non-loopback arbitrary HTTPS is received | TBD (hosted relay vs document as out of band) | |
| HTTPS cert strategy | TBD | |
| Skill ↔ app handoff command shape | TBD | |
