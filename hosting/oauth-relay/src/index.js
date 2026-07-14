/**
 * Hosted OAuth relay — Cloudflare Worker.
 * Contract: specs/009-oauth-https-callback/contracts/hosted-relay.md
 *
 * Stores authorization code / error only. TTL ≤ 300s. Poll is one-shot consume.
 * In-memory Map for wrangler dev / single isolate; bind KV `SESSIONS` for multi-isolate prod.
 */

const MAX_TTL = 300;
const HTML_OK =
  "<!doctype html><html><body><p>You can close this window and return to the terminal.</p></body></html>";
const HTML_GONE =
  "<!doctype html><html><body><p>Session expired or unknown.</p></body></html>";

/** @type {Map<string, object>} */
const memory = new Map();

export default {
  /**
   * @param {Request} request
   * @param {{ SESSIONS?: KVNamespace, PUBLIC_BASE?: string }} env
   */
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname.replace(/\/+$/, "") || "/";

    try {
      if (request.method === "POST" && path === "/v1/session") {
        return await handleSession(request, url, env);
      }
      if (request.method === "GET" && path === "/v1/callback") {
        return await handleCallback(url, env);
      }
      if (request.method === "GET" && path === "/v1/poll") {
        return await handlePoll(url, env);
      }
      if (request.method === "GET" && (path === "/" || path === "/health")) {
        return json({ ok: true, service: "api2skill-oauth-relay" });
      }
      return new Response("Not found", { status: 404 });
    } catch (err) {
      return new Response("Internal error", { status: 500 });
    }
  },
};

/**
 * @param {Request} request
 * @param {URL} url
 * @param {{ SESSIONS?: KVNamespace, PUBLIC_BASE?: string }} env
 */
async function handleSession(request, url, env) {
  let body = {};
  try {
    body = await request.json();
  } catch {
    body = {};
  }

  const ttl = clampTtl(body.ttlSeconds);
  const sessionId = crypto.randomUUID().replace(/-/g, "");
  const expiresUtc = new Date(Date.now() + ttl * 1000).toISOString();
  const publicBase = (env.PUBLIC_BASE || `${url.protocol}//${url.host}`).replace(/\/+$/, "");
  const callbackUrl = `${publicBase}/v1/callback?sid=${encodeURIComponent(sessionId)}`;

  const session = {
    sessionId,
    state: body.state ?? null,
    expiresUtc,
    code: null,
    error: null,
    errorDescription: null,
    completed: false,
    consumed: false,
  };

  await putSession(env, sessionId, session, ttl);

  return json(
    { sessionId, callbackUrl, expiresUtc },
    { status: 201 },
  );
}

/**
 * @param {URL} url
 * @param {{ SESSIONS?: KVNamespace }} env
 */
async function handleCallback(url, env) {
  const sid = url.searchParams.get("sid");
  if (!sid) {
    return html(HTML_GONE, 404);
  }

  const session = await getSession(env, sid);
  if (!session || isExpired(session) || session.consumed) {
    if (session) await deleteSession(env, sid);
    return html(HTML_GONE, 410);
  }

  const code = url.searchParams.get("code");
  const state = url.searchParams.get("state");
  const error = url.searchParams.get("error");
  const errorDescription = url.searchParams.get("error_description");

  session.code = code || null;
  session.state = state || session.state;
  session.error = error || null;
  session.errorDescription = errorDescription || null;
  session.completed = true;

  const ttlLeft = Math.max(
    1,
    Math.ceil((Date.parse(session.expiresUtc) - Date.now()) / 1000),
  );
  await putSession(env, sid, session, ttlLeft);

  // Optional deep link when custom scheme is registered (hint only; no secrets).
  const deep = `api2skill://oauth/callback?sid=${encodeURIComponent(sid)}`;
  const page =
    HTML_OK.replace(
      "</body>",
      `<p><a href="${deep}">Open in api2skill</a> (if registered)</p></body>`,
    );
  return html(page, 200);
}

/**
 * @param {URL} url
 * @param {{ SESSIONS?: KVNamespace }} env
 */
async function handlePoll(url, env) {
  const sid = url.searchParams.get("sid");
  const stateKey = url.searchParams.get("state");

  let key = sid;
  let session = sid ? await getSession(env, sid) : null;

  if (!session && stateKey) {
    // Best-effort state lookup (memory only; KV listing not used).
    for (const [k, v] of memory.entries()) {
      if (v && v.state === stateKey) {
        key = k;
        session = v;
        break;
      }
    }
  }

  if (!session || !key) {
    return new Response(null, { status: 410 });
  }

  if (isExpired(session) || session.consumed) {
    await deleteSession(env, key);
    return new Response(null, { status: 410 });
  }

  if (!session.completed) {
    return json({ status: "pending" });
  }

  session.consumed = true;
  await deleteSession(env, key);

  return json({
    status: "completed",
    code: session.code,
    state: session.state,
    error: session.error,
    errorDescription: session.errorDescription,
  });
}

function clampTtl(raw) {
  const n = Number(raw);
  if (!Number.isFinite(n) || n <= 0) return MAX_TTL;
  return Math.min(Math.floor(n), MAX_TTL);
}

function isExpired(session) {
  return Date.parse(session.expiresUtc) <= Date.now();
}

async function putSession(env, id, session, ttlSeconds) {
  memory.set(id, session);
  if (env.SESSIONS) {
    await env.SESSIONS.put(id, JSON.stringify(session), {
      expirationTtl: Math.max(60, Math.min(ttlSeconds, MAX_TTL)),
    });
  }
}

async function getSession(env, id) {
  if (env.SESSIONS) {
    const raw = await env.SESSIONS.get(id);
    if (raw) {
      try {
        return JSON.parse(raw);
      } catch {
        return null;
      }
    }
  }
  return memory.get(id) ?? null;
}

async function deleteSession(env, id) {
  memory.delete(id);
  if (env.SESSIONS) {
    await env.SESSIONS.delete(id);
  }
}

function json(data, init = {}) {
  return new Response(JSON.stringify(data), {
    status: init.status || 200,
    headers: {
      "content-type": "application/json; charset=utf-8",
      ...(init.headers || {}),
    },
  });
}

function html(body, status) {
  return new Response(body, {
    status,
    headers: { "content-type": "text/html; charset=utf-8" },
  });
}
