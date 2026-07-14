# Contract: Hosted OAuth relay

Deployable service under `hosting/oauth-relay/`. Default public base:
`https://oauth.api2skill.dev` (overridable via `API2SKILL_OAUTH_RELAY_BASE` / `--relay-base`).

## Endpoints

### `POST /v1/session`

Creates a pending session.

**Request** (JSON):

```json
{ "state": "oauth-state", "ttlSeconds": 300 }
```

`ttlSeconds` clamped to ≤ 300.

**Response** `201`:

```json
{
  "sessionId": "…",
  "callbackUrl": "https://oauth.api2skill.dev/v1/callback?sid=…",
  "expiresUtc": "2026-07-14T12:00:00Z"
}
```

### `GET /v1/callback`

Browser redirect target. Query: IdP `code`, `state`, `error`, `error_description`, plus `sid`
(session id) if used.

**Behavior**: persist code/error on session; return minimal HTML (“You can close this window”)
and optional deep link `api2skill://oauth/callback?…` when feasible.

**Must not** display full tokens; must not require cookies beyond the session id in query.

### `GET /v1/poll?sid=…` or `?state=…`

**Response** `200` pending:

```json
{ "status": "pending" }
```

**Response** `200` completed (one-shot consume):

```json
{
  "status": "completed",
  "code": "…",
  "state": "…",
  "error": null,
  "errorDescription": null
}
```

**Response** `410` expired / unknown.

## Privacy & abuse

- Store **authorization code / error only**; never client secrets or access tokens.
- Delete on consume or TTL.
- Rate-limit session creation (implement guidance: e.g. per-IP soft limit).
- No retention logging of raw query strings in recommended deploy config.

## Local stub

Tests and `quickstart` may run an in-process stub matching this contract; production deploy is
documented in wiki, not required for unit tests.
