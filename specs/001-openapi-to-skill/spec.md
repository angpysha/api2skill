# Feature Specification: OpenAPI → Claude Skill generator (core)

**Feature Branch**: `001-openapi-to-skill`

**Created**: 2026-07-10

**Status**: Draft — MVP grilling complete (D1–D7 resolved); ready for `/speckit.plan`

**Input**: User description: "Convert an OpenAPI/Swagger specification into a ready-to-use Claude Agent Skill (SKILL.md plus supporting scripts) via a .NET console app"

## Problem & Scope

**Problem**: Writing a correct, well-documented Claude Skill for an existing REST API by hand
is repetitive — endpoint/parameter docs, auth handling, and example requests all have to be
derived from the API's OpenAPI spec anyway. `api2skill` automates that derivation.

**In scope (this feature — the MVP)**: A .NET console app that reads an OpenAPI/Swagger
document and emits a self-contained Claude Skill directory (`SKILL.md` + generated scripts +
on-demand reference docs) that Claude can load and use to make correct calls against the API.

**Out of scope (this feature)**: generating an MCP server (that would be a different product,
"api2mcp"); generating a compiled/typed client library; interactive editing of the generated
skill. See Assumptions / Open Questions.

## Resolved Decisions (grilling log)

> Each entry: the decision, the chosen option, and the rejected alternatives. Populated one at
> a time during the `grill-with-docs` session.

### D1 — Runtime mechanism of the generated skill

**Decided**: The generator emits **`SKILL.md` + generated scripts** (plus on-demand
`reference/` docs). `SKILL.md` describes what the API does and when to use each operation;
thin scripts (a dispatcher and/or per-operation scripts, run via Claude's Bash tool) own the
actual HTTP call, base URL, request shaping, and auth-header injection.

**Why**: Most deterministic/reliable calls; keeps `SKILL.md` compact (endpoint minutiae live
in scripts + `reference/`, loaded on demand); auth handled once in a script rather than
re-derived by Claude on every call.

**Rejected**:
- *Pure-documentation SKILL.md* (Claude curls directly) — simplest output but non-deterministic
  calls and auth re-derived each time.
- *Generated typed client library* — strongest typing but forces a build/runtime dependency
  into every generated skill; heavier than a skill needs.

### D2 — Script language, HTTP client, extensibility, and TLS policy

**Decided**: Generated scripts are **.NET scripts (`.csx` and/or `.fsx`)**, not bash — this
keeps the whole toolchain on .NET. Within those scripts the HTTP calls use **plain
`System.Net.Http.HttpClient` + `System.Text.Json`** (no third-party client). The script
**emitter is a pluggable abstraction** so other script kinds (bash, Python, a compiled client,
etc.) can be added later without changing the core. Because api2skill is a **developer tool**
often pointed at dev/staging endpoints, generated scripts **MUST support calling untrusted
HTTPS** (self-signed / invalid certs) behind an explicit opt-in flag/env var, implemented with
`HttpClientHandler.DangerousAcceptAnyServerCertificateValidator`.

**Why**: `.csx`/`.fsx` keep everything in the .NET ecosystem the generator already lives in and
require no separate compiled artifact per skill. Plain `HttpClient` is the only client option
that reliably works in a script (see rejected Refit note) with **zero NuGet dependencies**, and
the untrusted-HTTPS toggle is a one-line handler setting.

**Rejected**:
- *Refit inside `.csx`/`.fsx`* — **does not work in scripts.** Since Refit v6 the client
  implementation is emitted by a **Roslyn source generator at build time**; a `dotnet-script` /
  F# Interactive run has no build step, so `RestService.For<T>()` finds no generated
  implementation and throws *"doesn't look like a Refit interface"* (confirmed for current
  Refit 12.x/13.x; the pre-v6 reflection fallback was removed). Refit would only work if we
  emitted a **compiled project**, which contradicts the "generate scripts" directive.
  Refs: reactiveui/refit#1327, LINQPad source-generator discussion.
- *Flurl.Http / RestSharp via `#r "nuget:"`* — runtime-based so they *do* work in scripts, but
  add a NuGet dependency the plain-`HttpClient` path avoids. Kept as a possible future emitter,
  not the default.
- *bash + curl* — rejected in favour of staying on .NET; remains a candidate future emitter.

**Untrusted-HTTPS safety note**: the bypass is **opt-in only** (flag/env var, off by default) and
must be clearly labelled as dev-only in the generated `SKILL.md` — it disables all certificate
validation when enabled.

### D3 — Emitter set and default (concrete instance of the pluggable emitter)

**Decided**: Ship **three script emitters** and let the user choose which the generated skill
uses:

1. **.NET 10 file-based `.cs`** — run with `dotnet run app.cs`; NuGet via `#:package`. Runner is
   **SDK-native** (no extra tool). **Default emitter.**
2. **`.fsx`** — run with `dotnet fsi script.fsx`; runner ships with the SDK.
3. **`.csx`** — run with `dotnet script file.csx`; requires the `dotnet-script` global tool.

Selection is via a generator flag (e.g. `--script cs|fsx|csx`). All three go through the same
parse → intermediate-model stage (FR-006); only the emitter differs. The generated `SKILL.md`
documents the runner its chosen emitter assumes (and, for `.csx`, the `dotnet-script` install
step).

**Why**: The three cover the common .NET scripting preferences; `.cs` file-based is the default
because it pairs natural C# `HttpClient` code (D2) with an SDK-native runner (no extra install).
`.fsx`/`.csx` satisfy F#/dotnet-script users. This validates the pluggable-emitter design against
three real emitters up front.

**Open**: exact CLI flag name/values, and whether one invocation can emit multiple kinds at once
(deferred — default is a single chosen kind).

### D4 — Authentication: schemes + credential source

**Decided (schemes)**: The MVP generates working auth for **all four** OpenAPI security scheme
families, driven by the spec's `securitySchemes`:

1. **API key** (`apiKey`) — inject into the named header or query parameter.
2. **Bearer** (`http`/`bearer`) — `Authorization: Bearer <token>` (also covers a pre-obtained
   OAuth2 access token).
3. **Basic** (`http`/`basic`) — `Authorization: Basic base64(user:pass)`.
4. **OAuth2 full flow** (`oauth2`) — the script **obtains a token itself** (client-credentials at
   minimum) from the spec's token URL before calling the operation, then applies it as a bearer.

**Decided (credential source)**: Credentials are **never hardcoded** in emitted scripts. Each
generated skill ships a **gitignored per-skill config file** (e.g. `secrets.json` in the skill
directory) plus a committed **template** (`secrets.example.json`) and a `.gitignore` entry. At
call time scripts **load credentials from that config file**. The file holds whatever the API's
schemes require: bearer token, API key, basic user/pass, and OAuth2 `client_id` / `client_secret`
/ `token_url` (+ scopes).

**Why (schemes)**: These four cover essentially all REST auth seen in practice; OAuth2 full flow
is included so the skill works against APIs that only issue short-lived tokens, without the user
minting one by hand each time.

**Why (credential source)**: A per-skill config file is persistent across runs (no re-exporting
env vars every session), keeps each skill's secrets self-contained next to it, and is safe by
default via `.gitignore`. The committed template documents exactly what to fill in.

**Rejected**:
- *Environment variables as the source of truth* — no on-disk secret, but must be re-set every
  session and is clumsy for multi-credential OAuth2. **Kept as a possible future fallback layer**
  (env-var override of the file), not the MVP default.
- *CLI args* — leak secrets into shell history / process list.

**Safety**: the generator MUST write/append a `.gitignore` that excludes the real secrets file,
MUST emit only the template with placeholder values, and MUST NOT read or embed any real
credential during generation. OAuth2 tokens obtained at runtime are held in memory for that run
only (no token written to disk in the MVP).

**Open**: exact config filename/format (`secrets.json` vs `.env`); per-operation vs per-skill
credential scoping when a spec defines multiple schemes; OAuth2 grant types beyond
client-credentials (authorization-code/PKCE — likely deferred).

### D5 — Spec versions & input sources (parser: Microsoft.OpenApi)

**Decided (parser)**: Use **Microsoft.OpenApi** (OpenAPI.NET, current 3.8.0) as the parsing
library — it reads 2.0/3.0/3.1/3.2 into a single object model on `System.Text.Json`, so the
generator works against one normalized model regardless of source version.

**Decided (versions)**: Officially **support & test OpenAPI 3.0 and 3.1**; **accept Swagger 2.0**
(the library normalizes it for near-free, and the product explicitly targets "Swagger"); treat
**3.2 as best-effort**.

**Decided (input sources)**: Accept **all three** — a **local file** (`.json`/`.yaml`), a
**remote URL** (e.g. a running service's `/swagger.json`), and **stdin**. The D4/D2 untrusted-
HTTPS opt-in also applies when fetching a spec from a dev server with a self-signed cert.

**Why**: 3.0/3.1 are today's mainstream; 2.0 acceptance is cheap via the normalized model and
widens reach to older/enterprise specs. Remote-URL + stdin make the tool composable with dev
workflows (`api2skill https://svc.local/swagger.json`, or `curl … | api2skill`).

**Rejected**:
- *3.x only* (reject 2.0) — simpler matrix but drops many real specs; unnecessary given the
  library handles 2.0.
- *File-only input* — too limiting for a dev tool that will often target a live service.

### D6 — Output structure for large APIs & script granularity

**Decided (layout)**: Use **progressive disclosure**. `SKILL.md` stays compact: API overview,
auth setup, and a **compact operation index grouped by OpenAPI tag** (one line per operation →
its `operationId`, one-line summary, pointer to its reference doc). Full parameter/schema detail
lives in **`reference/<tag>.md`**, loaded on demand. The generator also supports
**`--include`/`--exclude` filters** (by tag / path / operationId) to emit a focused subset of a
large API.

**Decided (script granularity)**: **One dispatcher script per skill** —
`call <operationId> --params…`. Auth, base-URL resolution, TLS policy, and request shaping live
in that single script; it scales to hundreds of operations without hundreds of files. `SKILL.md`
+ `reference/` tell Claude which `operationId` and parameters to pass.

**Why**: Compact `SKILL.md` + per-tag reference keeps the always-loaded surface small so the
skill works on large APIs within the token budget, while full detail stays one hop away.
Filters let a user scope a giant API down to what they need. A single dispatcher centralizes auth
and setup (one place to get right) and keeps the skill directory small.

**Rejected**:
- *Everything inline in SKILL.md* — token bloat / slow load on large specs.
- *One folder or script per operation* — explodes file count and duplicates auth/setup on large
  APIs; scatters the overview.

**Depends on D1** (SKILL.md + scripts) and **D4** (auth centralized — natural in one dispatcher).

**Open**: reference granularity (per-tag vs per-operation files) for very large single tags;
handling operations with **no `tag`** (bucket into a default group) and **no `operationId`**
(synthesize a stable id from method+path); exact filter flag syntax.

### D7 — Output location, naming, and overwrite policy

**Decided (location & naming)**: Write the skill to a **user-specified output directory** (`-o`),
defaulting to **`./<slug>`** in the current directory. The skill **name is derived from the
OpenAPI `info.title`** (slugified), overridable with **`--name`**. Generation is **separate from
installation** — the user reviews the output, then copies/symlinks it into `~/.claude/skills/`
(or a project's `.claude/skills/`). An optional `--install` convenience may be added later.

**Decided (overwrite)**: If the target directory exists, **fail with a clear message by default**;
**`--force`** regenerates the generated files (SKILL.md, reference, dispatcher script,
`secrets.example.*`, `.gitignore`) but **MUST preserve an existing real secrets file** — never
clobber filled-in credentials.

**Why**: Separating generate from install keeps output reviewable and CI-friendly and avoids
mutating the user's skills directory as a side effect. Title-derived naming is the obvious
default; `--name` covers collisions/preferences. Fail-by-default + secrets-preserving `--force`
makes re-running against an updated spec safe.

**Rejected**:
- *Install directly into the skills dir by default* — convenient but mutates user state without
  review and is awkward in CI (kept as a future `--install` opt-in).
- *Overwrite everything* — risks destroying a user's real secrets file / local edits.
- *Merge (write only missing)* — leaves stale docs/scripts out of sync with the updated spec.

**Open**: exact slug rules; behavior of a future `--install` (symlink vs copy; user vs project
scope).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a working skill from an OpenAPI file (Priority: P1)

A developer points the console app at a local OpenAPI/Swagger document and gets back a skill
directory they can drop into `~/.claude/skills/` (or a project's `.claude/skills/`) and use
immediately, without hand-editing for the common case.

**Why this priority**: This is the product's entire reason to exist; without it there is no MVP.

**Independent Test**: Run the app against a known-good OpenAPI file (e.g. the Swagger Petstore),
load the emitted skill, and confirm Claude can make a correct call to at least one endpoint.

**Acceptance Scenarios**:

1. **Given** a valid OpenAPI document, **When** the app runs against it, **Then** a skill
   directory is written containing a `SKILL.md` and runnable script(s) covering the API's
   operations.
2. **Given** the emitted skill is loaded by Claude, **When** Claude is asked to perform an
   action the API supports, **Then** Claude invokes the correct operation with correct
   parameters and auth, and receives a valid response.

### Edge Cases

- **Invalid / unparseable spec** → exit non-zero with an actionable message; emit nothing
  partial (FR-010).
- **Unsupported/unknown version** (e.g. a future 3.x the parser rejects) → clear error naming the
  detected version; 2.0 accepted, 3.2 best-effort (D5).
- **Operation with no `operationId`** → synthesize a stable id from method+path (FR-004c).
- **Operation with no `tag`** → bucket into a default group in the index (FR-004c).
- **Duplicate/colliding operationIds or synthesized ids** → deterministic disambiguation
  (e.g. suffix); the dispatcher must resolve unambiguously.
- **Security scheme in the spec that isn't one of the four supported** → generate the skill but
  surface a clear warning + a `reference` note that the op needs manual auth.
- **No `servers` / base URL in the spec** → require the user to supply a base URL (flag/secrets
  config) rather than guessing.
- **Spec fetched from a URL with a self-signed cert** → only succeeds when the untrusted-HTTPS
  opt-in is set (D5/FR-007).
- **Very large API** → progressive-disclosure layout + optional `--include`/`--exclude` (D6).
- **Target dir exists / real secrets already filled in** → fail without `--force`; `--force`
  preserves secrets (FR-009).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept an OpenAPI/Swagger document from a **local file**, a
  **remote URL**, or **stdin**, and parse it with **Microsoft.OpenApi**. It MUST support & test
  **3.0 and 3.1**, accept **2.0**, and best-effort **3.2**. Fetching a spec over untrusted HTTPS
  MUST honor the same opt-in TLS bypass as FR-007.
- **FR-002**: The system MUST emit a self-contained Claude Skill directory consisting of a
  `SKILL.md`, a runnable **.NET script (`.cs` / `.fsx` / `.csx`, per D3)** dispatcher that
  performs the API calls, on-demand reference documentation, a secrets template, and a
  `.gitignore`.
- **FR-003**: Generated scripts MUST own base-URL resolution, request shaping, and injection of
  authentication credentials.
- **FR-003a**: The system MUST generate working auth for OpenAPI `apiKey` (header/query), `http`
  bearer, `http` basic, and `oauth2` (obtaining a token via the spec's token URL) schemes, driven
  by the document's `securitySchemes`.
- **FR-003b**: Generated scripts MUST load credentials from a **gitignored per-skill config file**
  and MUST NOT hardcode them. The generator MUST emit a committed template
  (`secrets.example.*`) and a `.gitignore` entry excluding the real file, and MUST NOT read or
  embed any real credential during generation.
- **FR-004**: `SKILL.md` MUST stay compact — API overview, auth setup, and a **tag-grouped
  operation index** (operationId + one-line summary + pointer to reference) — with full
  parameter/schema detail in on-demand **`reference/<tag>.md`** files (progressive disclosure).
- **FR-004a**: The system MUST emit a **single dispatcher script** per skill invoked as
  `call <operationId> --params…`, centralizing auth, base URL, TLS policy, and request shaping.
- **FR-004b**: The system MUST support **`--include`/`--exclude` filters** (by tag / path /
  operationId) to generate a focused subset of a large API.
- **FR-004c**: The system MUST handle operations with **no `tag`** (assign a default group) and
  **no `operationId`** (synthesize a stable id from HTTP method + path).
- **FR-005**: Generated scripts MUST perform HTTP using `System.Net.Http.HttpClient` +
  `System.Text.Json` with **no third-party HTTP client dependency**.
- **FR-006**: The script generator MUST be a **pluggable emitter** — the core pipeline (parse →
  model → emit) MUST allow additional script/output kinds (bash, Python, compiled client, …) to
  be added without changing parsing or the intermediate model.
- **FR-006a**: The system MUST provide **three built-in emitters** — .NET 10 file-based `.cs`
  (default, `dotnet run`), `.fsx` (`dotnet fsi`), and `.csx` (`dotnet script`) — selectable via a
  generator flag, all sharing one parse/intermediate-model stage.
- **FR-006b**: The generated `SKILL.md` MUST state which runner its chosen emitter assumes,
  including the `dotnet-script` global-tool install step when the `.csx` emitter is used.
- **FR-007**: Generated scripts MUST support calling **untrusted HTTPS** endpoints (self-signed
  / invalid TLS certs) via an **explicit opt-in** flag/env var (off by default), implemented
  with `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator`, and MUST label it as
  dev-only in the generated docs.
- **FR-008**: The system MUST write the skill to a user-specified output directory (`-o`),
  default `./<slug>`, with the skill name derived from `info.title` (overridable via `--name`).
  It MUST NOT install into `~/.claude/skills` or `.claude/skills` as a default side effect.
- **FR-009**: If the output directory exists, the system MUST fail with a clear message unless
  `--force` is given; with `--force` it regenerates generated files but MUST preserve an existing
  real secrets file.
- **FR-010**: On invalid/unparseable input or an unsupported spec version, the system MUST exit
  non-zero with a clear, actionable error and MUST NOT emit a partial skill directory.

### Key Entities

- **OpenAPI document**: the input contract (info, servers, paths/operations, parameters,
  request/response schemas, security schemes).
- **Generated Skill**: the output directory — `SKILL.md`, `scripts/`, `reference/`.
- **Operation**: one API endpoint+method the skill can invoke.
- **Security scheme**: an auth requirement from the spec (`apiKey` / bearer / basic / `oauth2`)
  that maps to header/token handling in the scripts.
- **Secrets config**: the gitignored per-skill file holding real credentials, plus its committed
  template.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a valid OpenAPI 3.0/3.1 document, the app produces a loadable skill
  directory (`SKILL.md` + dispatcher script + per-tag reference + secrets template + `.gitignore`)
  with no manual edits required beyond filling in the secrets file.
- **SC-002**: A Claude session with the emitted skill loaded makes a correct, **authenticated**
  call to a target endpoint on the first attempt (verified against the Swagger Petstore or an
  equivalent live API for each of the four auth schemes).
- **SC-003**: For an API with 100+ operations, the always-loaded `SKILL.md` stays within a
  workable size because per-operation detail is in on-demand `reference/` files.
- **SC-004**: The same spec can be emitted as each of the three script kinds (`.cs`/`.fsx`/`.csx`)
  and each runs successfully with its documented runner.
- **SC-005**: No real credential is ever written into a committed/generated file; the real
  secrets file is `.gitignore`d and preserved across `--force` regeneration.

## Assumptions

- The generated skill targets Claude Code / Claude Agent Skills (`SKILL.md` format).
- Output is a skill, not an MCP server or a compiled client (see Out of scope).
- The machine running a generated skill has the runner its emitter needs: .NET 10 SDK for
  `.cs` (`dotnet run`) and `.fsx` (`dotnet fsi`); the `dotnet-script` global tool for `.csx`.
- Parsing uses **Microsoft.OpenApi**; its normalized model is the generator's single input model.
- OAuth2 support targets the **client-credentials** grant for the MVP (other grants deferred).
- The user supplies real credentials by editing the gitignored secrets file after generation.
- A base URL comes from the spec's `servers`; if absent, the user supplies one.

## Open Questions

> Tracked here as they surface; each is resolved into a D-entry above via grilling.

- ~~OpenAPI versions~~ → **Resolved D5**: support/test 3.0+3.1, accept 2.0, best-effort 3.2.
- ~~Auth schemes & credential source~~ → **Resolved D4**: apiKey/bearer/basic/oauth2 +
  gitignored per-skill config file.
- ~~Large-API handling / SKILL.md vs reference split~~ → **Resolved D6**: compact SKILL.md +
  tag-grouped index + per-tag reference + filters; single dispatcher script.
- ~~Input source(s)~~ → **Resolved D5**: local file, remote URL, and stdin.
- ~~Output location / packaging~~ → **Resolved D7**: `-o` output dir (default `./<slug>`),
  name from `info.title`, fail-unless-`--force` with secrets preserved.
- ~~Script language / HTTP client / TLS~~ → **Resolved D2**: `.cs`/`.fsx`/`.csx` + plain
  `HttpClient`, pluggable emitter, opt-in untrusted HTTPS.
- ~~csx vs fsx default & runner~~ → **Resolved D3**: support all three (`.cs` default / `.fsx` /
  `.csx`), selectable via flag.

### Deferred (post-MVP)

- `--install` convenience (symlink/copy into skills dir).
- Env-var override layer on top of the secrets file.
- OAuth2 grants beyond client-credentials (authorization-code/PKCE).
- Additional emitters (bash, Python) and additional HTTP-client backends (Flurl/RestSharp).
- Emitting multiple script kinds in one invocation.
