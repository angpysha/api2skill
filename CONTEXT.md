# api2skill — Ubiquitous Language (Glossary)

> Domain glossary for api2skill. Terms only — the canonical *what/why* lives in
> `specs/NNN-slug/spec.md` and the *how* in `plan.md`. Keep this consistent with those; if a term
> here conflicts with a spec, the spec wins and this file is updated.

- **OpenAPI document / spec**: The input contract (OpenAPI 2.0/3.0/3.1, best-effort 3.2) parsed
  by Microsoft.OpenApi into one normalized model. Also loosely "the Swagger spec".

- **Generated Skill (or "skill")**: The output directory api2skill produces — a Claude Agent
  Skill consisting of `SKILL.md` + a dispatcher script + `reference/` + a secrets template +
  `.gitignore`. Not to be confused with an api2skill *emitter* or the *pipeline*.

- **`SKILL.md`**: The compact, always-loaded entry document of a Generated Skill: API overview,
  auth setup, and a tag-grouped **operation index**. Full detail is offloaded to `reference/`.

- **Operation**: One API endpoint+method (identified by `operationId`, synthesized from
  method+path when absent) that the Generated Skill can invoke.

- **Operation index**: The tag-grouped list of operations in `SKILL.md` — one line each
  (operationId, summary, pointer to reference), enabling progressive disclosure.

- **Reference doc**: An on-demand `reference/<tag>.md` file holding full parameter/schema detail
  for the operations in a tag. Loaded only when needed.

- **Dispatcher (script)**: The single generated script per skill, invoked as
  `call <operationId> --params…`, that centralizes base-URL resolution, request shaping, auth,
  and TLS policy. The one runnable artifact that actually calls the API.

- **Emitter**: A pluggable component of api2skill that turns the intermediate model into a
  specific script kind. Built-in emitters: `.cs` (file-based, default), `.fsx`, `.csx`. Future:
  bash, Python, compiled client. (An *emitter* produces a *Generated Skill*; don't conflate them.)

- **Intermediate model**: api2skill's internal, emitter-agnostic representation produced after
  parsing, shared by every emitter (parse → model → emit).

- **Security scheme**: An auth requirement from the spec — `apiKey` (header/query), `http` bearer,
  `http` basic, or `oauth2` (client-credentials) — mapped to header/token handling in the
  dispatcher.

- **Secrets config**: The **gitignored** per-skill file holding real credentials at call time.
  Paired with a committed **secrets template** (`secrets.example.*`) documenting what to fill in.
  Real credentials are never embedded in generated files.

- **Untrusted-HTTPS opt-in**: The explicit, off-by-default flag/env var that lets the dispatcher
  (and spec fetching) accept self-signed/invalid TLS certs — dev-only, via
  `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator`.

- **Runner**: The tool that executes an emitter's script — `dotnet run` (`.cs`), `dotnet fsi`
  (`.fsx`), or the `dotnet-script` global tool (`.csx`).
