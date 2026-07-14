# Contract: `oauth-capture` result JSON

Emitted on **stdout** as one JSON object (UTF-8, no BOM). Stable field names for skill parsers.

## Success

```json
{
  "ok": true,
  "mode": "HttpLoopback",
  "code": "AUTHORIZATION_CODE",
  "state": "STATE_VALUE",
  "error": null,
  "errorDescription": null,
  "callbackUrl": "http://localhost:8400/callback"
}
```

`mode` enum strings: `HttpLoopback` | `HttpsLoopback` | `CustomScheme` | `Hosted`.

## Failure (still often exit ≠ 0)

```json
{
  "ok": false,
  "mode": "HttpsLoopback",
  "code": null,
  "state": "STATE_VALUE",
  "error": "access_denied",
  "errorDescription": "user cancelled",
  "callbackUrl": "https://localhost:8400/callback"
}
```

Timeout:

```json
{
  "ok": false,
  "mode": "Hosted",
  "code": null,
  "state": null,
  "error": "timeout",
  "errorDescription": "No redirect received within 180 seconds",
  "callbackUrl": "https://oauth.api2skill.dev/v1/callback"
}
```

## Consumer rules (generated `login`)

1. Prefer `Process` → `api2skill oauth-capture ... --json`.
2. Parse stdout JSON; ignore stderr for control flow.
3. On `ok: true` and non-empty `code`, continue existing PKCE token POST.
4. If tool missing on PATH and mode would be HTTP loopback, **optional fallback** to in-script
   `HttpListener` (temporary); HTTPS/scheme/hosted **require** the tool.
