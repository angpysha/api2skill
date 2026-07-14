# Hosted OAuth relay

Deployable capture relay for non-loopback HTTPS callbacks (Postman-style).

**Default public base (v1):** `https://oauth.api2skill.dev`  
Override: env `API2SKILL_OAUTH_RELAY_BASE` or CLI `--relay-base`.

## Contract

See [`specs/009-oauth-https-callback/contracts/hosted-relay.md`](../../specs/009-oauth-https-callback/contracts/hosted-relay.md):

| Method | Path | Role |
|--------|------|------|
| `POST` | `/v1/session` | Create pending session (TTL ≤ 5 minutes) |
| `GET` | `/v1/callback` | Browser redirect target — stores `code` / `error` only |
| `GET` | `/v1/poll` | One-shot poll for completed session |

## Deploy intent

Implement as a **Cloudflare Worker** (preferred) or Azure Function under this folder.
The api2skill CLI polls the relay; skills never embed relay secrets.

**Status:** stub folder — Worker/Function source lands with US4 (`api2skill-009-oauth-capture-ol0.5`).

## Privacy

- Store authorization **code** / **error** only — never tokens or client secrets.
- Delete after successful poll or TTL expiry.
- Do not durable-log full query strings in production.
