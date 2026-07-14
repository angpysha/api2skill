# Hosted OAuth relay

Deployable capture relay for non-loopback HTTPS callbacks (Postman-style).

**Stack choice (US4):** **Cloudflare Worker** (JavaScript module worker under `src/index.js`).

**Default public base (v1):** `https://oauth.api2skill.dev`  
Override: env `API2SKILL_OAUTH_RELAY_BASE` or CLI `--relay-base`.

## Contract

See [`specs/009-oauth-https-callback/contracts/hosted-relay.md`](../../specs/009-oauth-https-callback/contracts/hosted-relay.md):

| Method | Path | Role |
|--------|------|------|
| `POST` | `/v1/session` | Create pending session (TTL ≤ 5 minutes) |
| `GET` | `/v1/callback` | Browser redirect target — stores `code` / `error` only |
| `GET` | `/v1/poll` | One-shot poll for completed session |

## Local development

```bash
cd hosting/oauth-relay
npm install
npx wrangler dev          # listens on http://127.0.0.1:8787
```

Quickstart Scenario D:

```bash
export API2SKILL_OAUTH_RELAY_BASE=http://127.0.0.1:8787
api2skill oauth-capture --mode hosted \
  --callback-url "$API2SKILL_OAUTH_RELAY_BASE/v1/callback" \
  --timeout 60 --json
# In another shell, open/simulate the printed "Hosted callback URL" with ?code=&state=
```

CI and unit tests use an **in-process C# stub** (`TestHostedRelayServer`) — no Cloudflare account required.

## Deploy

1. `npm install` in this folder.
2. `npx wrangler login` (once).
3. Optional: create a KV namespace, bind as `SESSIONS` in `wrangler.toml` (recommended for multi-isolate production). Without KV, sessions live in an in-memory `Map` (fine for `wrangler dev` / single isolate).
4. Set `PUBLIC_BASE` to the public origin (e.g. `https://oauth.api2skill.dev`) if the Worker hostname differs from the URL registered at IdPs.
5. `npx wrangler deploy`
6. Point DNS / custom domain at the Worker; set `API2SKILL_OAUTH_RELAY_BASE` for CLI users until the default host is live.

## Privacy

- Store authorization **code** / **error** only — never tokens or client secrets.
- Delete after successful poll or TTL expiry.
- Do not durable-log full query strings in production.
- Soft rate-limit guidance: constrain `POST /v1/session` per client IP at the edge (Cloudflare rate limiting / WAF) — not implemented in-worker for v1.
