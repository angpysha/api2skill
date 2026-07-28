# Feature Specification: Multi-Spec Skill Composition

**Feature Branch**: `feature/012-multi-spec-skill`

**Created**: 2026-07-28

**Status**: Draft — plan + tasks + br epic ready; next `/speckit.analyze` or implement `.2`

**Input**: User description: "Support grouping multiple Swagger/OpenAPI documents (same base URL, often different auth) into one skill; merge previously generated separate skills for the same API; append new specs to an existing skill; update the api2skill-creator agent skill accordingly."

## Problem & Scope

**Problem**: Many real APIs publish **several OpenAPI documents** against the **same base URL** — for example separate Swagger UI entries for *public*, *admin*, and *internal* surfaces, each with different `securitySchemes`. Today `api2skill generate` accepts **one** spec and writes **one** skill. Users who generated separate skills per document get duplicated setup (multiple `secrets.json`, multiple dispatchers) and no single operation index for agents.

**In scope**:

- Compose **multiple spec sources** into **one** generated skill directory.
- **Merge** two or more existing api2skill skills (same base URL) into one target skill.
- **Add** a new spec source to an existing composite skill without losing credentials, examples, or auth state.
- Extend **`update`** so it refreshes **all** recorded spec sources.
- Extend **`.api2skill.json`** (backward compatible) to record multiple sources.
- Update the bundled **`api2skill-creator`** skill so agents can interview users through these flows.

**Out of scope**:

- Merging skills with **different base URLs** (fail unless user supplies an explicit shared `--base-url` override and accepts the risk).
- Automatic discovery of “all swagger.json links” on a host (user must supply paths/URLs or existing skill directories).
- Changing auth configuration during `merge` / `add-spec` (same rule as `update` — existing `auth.json` is preserved; user edits auth separately).
- Deleting source skill directories after merge (sources remain unless the user removes them manually).

## Proposal Summary (design intent)

Treat a generated skill as a **composite** built from **ordered spec parts**. Each part has a stable **`specId`** (slug, user-supplied or derived from filename/title), a **spec source** (file path or URL), and an optional **label** for human/agent display.

```
┌─────────────────────────────────────────────────────────┐
│  Skill directory (one SKILL.md, one dispatcher)         │
│  .api2skill.json  →  specSources: [ part A, part B, … ] │
│  auth.json        →  profiles attach per tag / global   │
│  examples/        →  union of all parts (by operationId)  │
└─────────────────────────────────────────────────────────┘
         ▲                    ▲                    ▲
         │ parse+merge        │ merge cmd          │ add-spec cmd
    generate --spec ×N    existing skills      new spec file
```

**Merge pipeline** (parse → **compose** → emit) stays constitution-compliant: parsers still produce one `SkillModel` per document; a new **composition** step unions operations, security schemes, schemas, and warnings before emitters run unchanged.

## Resolved Decisions (proposal defaults)

### D1 — Manifest shape: `specSources[]`, backward compatible

**Decided**: Extend `.api2skill.json` with a `specSources` array:

```json
{
  "name": "acme-api",
  "specSources": [
    { "id": "public", "source": "./openapi-public.json", "label": "Public API" },
    { "id": "admin", "source": "https://api.example.com/swagger/admin/swagger.json", "label": "Admin API" }
  ],
  "scriptKind": "cs",
  "include": [],
  "exclude": [],
  "format": null,
  "baseUrl": "https://api.example.com",
  "insecure": false
}
```

Legacy skills with singular `specSource` (string) **MUST** continue to load and behave as a one-element `specSources` list with `id` derived from the skill name or filename slug.

**Why**: Reuses the existing manifest/update pattern from feature 003 without a second config file.

### D2 — Base URL policy for composition

**Decided**: When composing multiple parts, all resolved base URLs **MUST** match after normalization (scheme + host + port + path prefix), **unless** the user passes `--base-url`, which becomes the single canonical base for the composite skill.

If sources disagree and no override is given → **fail** with a clear list of conflicting URLs per `specId`.

**Why**: One dispatcher, one `secrets.json` base URL — mixing unrelated hosts in one skill breaks the mental model and auth.

### D3 — `operationId` collisions

**Decided**: Default **fail** on duplicate `operationId` across parts, listing each conflict with the contributing `specId`s.

Optional `--on-conflict prefix` rewrites colliding IDs to `{specId}__{operationId}` in the **merged model only** (emitted dispatcher + reference). User-authored `examples/` keep their folder names; on prefix, emit a warning listing renamed IDs so examples can be relocated.

**Why**: Silent overwrites are dangerous for agents calling by `operationId`; explicit prefix is opt-in.

### D4 — Security scheme collisions

**Decided**: Security scheme IDs are namespaced on collision: `{specId}_{schemeId}` in the merged model. Non-colliding scheme IDs are kept as-is. `SKILL.md` auth table notes which **API part** each scheme came from.

Explicit `auth.json` profiles continue to attach by profile name / tags; creator skill guidance will recommend tag- or global-level profiles when multiple auths coexist.

### D5 — SKILL.md presentation

**Decided**: Add an **API parts** section (compact table: `specId`, label, source, operation count). Operation index remains tag-grouped; when the same tag name appears in multiple parts, **prefix the tag heading** with `[specId]` only on collision (e.g. `### [admin] Users` vs plain `### Users` when unique).

**Why**: Progressive disclosure (constitution V) — no per-operation source column in the main table unless warnings require it.

### D6 — CLI surface

| Command | Purpose |
|---------|---------|
| `generate --spec <source> [--spec-id <id>] [--spec-label <text>] …` | Repeatable `--spec`; creates a fresh composite skill |
| `add-spec <skill-path> <spec-source> [--spec-id <id>] [--spec-label <text>]` | Append one part to an **existing** skill; regenerate in place (`--force` semantics) |
| `merge --into <target-dir> <skill-path> [<skill-path> …]` | Merge **two or more** skills into a target (usually new); not for appending one doc (D8) |
| `update <skill-path> [<spec-source>] [--spec-id <id>]` | No positional: refresh all parts. With positional: replace one part (`--spec-id` required when N>1, D10) |

`merge` never deletes source directories. `merge --into` on a non-existent directory creates it; on an existing api2skill skill fails unless `--force` — use `add-spec` to grow an existing skill (D8).

### D7 — Creator skill (interview + post-generate setup)

**Decided**: Refresh `templates/api2skill-creator/SKILL.md` so agents **always** interview on **authorization**, **base URL**, and **secrets placement** (not only multi-spec). Multi-spec flows add per-part questions on top.

**Interview topics** (skip what the user already answered):

1. Spec source(s) and workflow (`generate` / `merge` / `add-spec` / `update`) — see D8.
2. **Base URL** — see [Creator skill contract](#creator-skill-contract-api2skill-creator) § Base URL.
3. **Authorization** — see § Authorization.
4. Output path, name, script kind, filters, `--insecure`, `--login`, `--force` as today.
5. After emitting the `generate`/`merge`/`add-spec` command, walk through **secrets setup** — see § Secrets placement.

Emit exact CLI commands first; then post-generate file instructions (never invent secret values).

**Rejected**: Leaving auth/base-url/secrets as optional footnotes; agents guessing credentials or base URLs.

### D8 — Merge vs add-spec (grilling Q1 → **A**)

**Decided**: **`add-spec`** appends a document into an **existing** skill (single- or multi-part). **`merge --into`** combines **two or more full skill directories** into a target — typically a **new** directory. `merge` MUST NOT be the documented path for “add one more doc to my existing skill”; creator skill and errors MUST steer users to `add-spec` instead.

**Rejected**: `merge --into` appending into an existing single-spec skill (blurs verbs).

### D9 — Credential union on merge (grilling Q2 → **A**; C1 constitution note)

**Decided**: When merging skills, **union** credential **keys** from gitignored runtime files (`secrets.json`, `.auth-cache.json`) into the target. When the same key has **different values**, the **first source skill in the argument list wins**; emit a **CLI warning** listing each colliding key. Do **not** silently delete credential files that exist in any source. `secrets.example.json` in the target is regenerated from the merged model (union of required keys — placeholders only).

**`auth.json`**: New target → copy from the **first** source that has `auth.json`; warn if other sources' `auth.json` differ. `--force` into an **existing** skill → **preserve** the target's `auth.json` (update-like).

**Constitution IV (narrow exception)**: Merge/add-spec MAY **read** gitignored `secrets.json` / `.auth-cache.json` solely to union/preserve them into the target skill directory. Implementations MUST **never** embed real credentials into committed outputs (`SKILL.md`, `.api2skill.json`, `secrets.example.json`, examples, reference). This is recorded in plan Complexity Tracking — not a constitution amendment.

**Rejected**: Fail merge on any key collision; TTY prompt (not CI-friendly); writing secrets into the secret-free manifest or SKILL.md.

### D10 — Replacing one part on `update` (grilling Q3 → **A**)

**Decided**: `update <skill-path> <spec-source>` with a **single-part** manifest replaces that part's source (today's behavior). With **multi-part** manifest, a positional `<spec-source>` is accepted **only** together with **`--spec-id <id>`**; omitting `--spec-id` with multiple parts is a usage error. All parts are always refreshed after any source replacement.

**Rejected**: Positional spec replacing all parts; separate `update-spec` subcommand.

### D11 — Creator skill self-update (always ask user)

**Decided**: The bundled `api2skill-creator` version tracks the **api2skill tool package version**. On install, write `<skills-root>/api2skill-creator/.creator-version.json` (`{"version":"X.Y.Z"}`). Legacy installs without that file are treated as `0.0.0`.

- **`api2skill creator check-update`** — scans the four standard project skill roots from the working directory; reports installed vs bundled version; exit code **8** when any installation is older than bundled.
- **`install-creator`** — when overwriting an existing install on a TTY, prompt explicitly for **updates** (e.g. `Update available: <installed> → <bundled matching package Version>. Install update? [y/N]`). Never silently upgrade.
- **Creator SKILL.md** — at session start, agents run `creator check-update`; if an update is available, **ask the user** before running `install-creator`. Agents must not pass `--force` unless the user explicitly requests it.
- **Non-TTY `install-creator`** without `--force` when an update is available: refuse with message naming bundled vs installed versions (same as today for overwrite, with update hint).

**Rejected**: Auto-updating the installed skill without user confirmation; agents running `install-creator --force` without explicit user approval.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate one skill from multiple OpenAPI documents (Priority: P1)

A platform exposes five Swagger documents at the same API host (different paths, different OAuth scopes / API keys). The user wants **one** agent skill with a unified operation index and one secrets file, while still documenting which auth applies to which operations.

**Why this priority**: Core greenfield case — avoids ever creating N separate skills.

**Independent Test**: Run `generate` with two fixture specs sharing a base URL and distinct security schemes; verify one skill directory, both operation sets present, auth table lists schemes from both parts, `.api2skill.json` lists two `specSources`.

**Acceptance Scenarios**:

1. **Given** two valid specs with the same base URL and unique `operationId`s, **When** `api2skill generate --spec ./a.json --spec ./b.json --out ./combined`, **Then** `./combined/SKILL.md` indexes operations from both and `.api2skill.json` records two parts.
2. **Given** the composite skill, **When** the user copies `secrets.example.json` to `secrets.json` and fills keys for both auth schemes, **Then** authenticated calls succeed for operations from each part (via existing auth attachment rules).
3. **Given** specs with conflicting base URLs and no `--base-url`, **When** `generate` runs, **Then** it fails with a per-part URL summary and writes nothing.

---

### User Story 2 - Merge previously generated skills (Priority: P1)

A user already generated `skills/acme-public` and `skills/acme-admin` from two documents on the same host. They want a single `skills/acme` without re-entering generate flags.

**Why this priority**: Directly addresses migration from today's one-spec-per-skill workflow.

**Independent Test**: Generate two skills with distinct manifests; run `merge --into ./skills/acme ./skills/acme-public ./skills/acme-admin`; verify union of operations, preserved union of secret keys in `secrets.example.json`, and source directories unchanged.

**Acceptance Scenarios**:

1. **Given** two valid api2skill skill directories with the same base URL, **When** `api2skill merge --into ./acme ./acme-public ./acme-admin`, **Then** `./acme` contains the merged skill and both sources remain on disk unmodified.
2. **Given** both sources have `secrets.json` with the same key and different values, **When** merge succeeds, **Then** target `secrets.json` keeps the **first source's** value for that key and the CLI emits a warning naming the key (D9).
3. **Given** `./acme` already exists as another api2skill skill, **When** merge runs without `--force`, **Then** it fails clearly without modifying sources or target.

---

### User Story 3 - Add a new document to an existing composite skill (Priority: P2)

After shipping a composite skill, the API team publishes a third OpenAPI document (e.g. *billing*). The user wants to append it without rebuilding from scratch or losing OAuth cache.

**Why this priority**: Ongoing maintenance — mirrors `update` ergonomics for growing APIs.

**Independent Test**: Start from a two-part composite with populated `auth.json` and `.auth-cache.json`; `add-spec ./combined ./billing.json --spec-id billing`; verify third part in manifest, preserved auth cache, new operations in index.

**Acceptance Scenarios**:

1. **Given** a composite skill, **When** `add-spec <path> <new-spec> --spec-id billing`, **Then** the skill regenerates with three `specSources` and preserves `secrets.json`, `auth.json`, and `.auth-cache.json`.
2. **Given** `--spec-id` omitted, **When** `add-spec` runs, **Then** `id` defaults to a slug of the spec filename and fails if that `id` already exists in the manifest.

---

### User Story 4 - Update refreshes all parts (Priority: P2)

A user runs `api2skill update ./combined` monthly. Each upstream spec may have changed.

**Why this priority**: Without multi-part update, composite skills would rot immediately.

**Independent Test**: Change both fixture specs; `update ./combined` with no positional arg re-fetches/re-reads every recorded source and updates operations from all.

**Acceptance Scenarios**:

1. **Given** a manifest with two URL sources, **When** `update ./combined`, **Then** both URLs are fetched and the skill reflects changes from both.
2. **Given** a manifest with parts `public` and `admin`, **When** `update ./combined ./new-admin.json --spec-id admin`, **Then** only the `admin` part's source is replaced and recorded; `public` is refreshed from its recorded source.

---

### User Story 5 - Creator skill guides auth, base URL, secrets, and multi-spec (Priority: P2)

An agent with `api2skill-creator` installed helps a user who says “I have five swagger docs on the same server” (or a single doc with OAuth).

**Why this priority**: Reduces CLI and credential-setup mistakes; user explicitly requested auth, base URL, and secrets guidance in the creator skill.

**Independent Test**: After `install-creator --force`, the bundled SKILL.md requires agents to ask about authorization and base URL, emit valid CLI commands, then instruct copying `secrets.example.json` → `secrets.json` with placeholder-shaped values before real credentials.

**Acceptance Scenarios**:

1. **Given** the updated creator skill, **When** an agent follows it for a multi-doc scenario, **Then** it asks for each spec path/URL, confirms a shared **base URL** (or `--base-url`), asks **per-part authorization** expectations, and emits a valid multi-`--spec` generate command (or `merge` / `add-spec` as appropriate).
2. **Given** the user already has separate skills, **When** the agent follows the creator skill, **Then** it recommends `merge --into` rather than re-generating from scratch.
3. **Given** generate has completed, **When** the agent continues the creator flow, **Then** it instructs the user to `cp secrets.example.json secrets.json` (or create `secrets.json` with the **same keys and empty-string placeholders** as `secrets.example.json`), lists which keys map to which auth scheme, and reminds that `secrets.json` is gitignored.
4. **Given** OAuth or multi-profile auth, **When** the agent follows the creator skill, **Then** it points to `auth.json` editing / `--auth-config` and does **not** invent client secrets or tokens.
5. **Given** specs lack a `servers` entry or disagree on URL, **When** the agent interviews the user, **Then** it includes `--base-url <url>` on the emitted generate/merge command and explains it becomes the skill's canonical API root.
6. **Given** a newer api2skill tool is installed, **When** the agent starts a creator session, **Then** it runs `api2skill creator check-update`, informs the user if an update is available, and **asks** before running `install-creator` (D11).
7. **Given** the user agrees to update on a TTY, **When** `install-creator` runs, **Then** it shows an explicit update confirmation prompt and writes `.creator-version.json` matching the bundled version.

---

### Edge Cases

- **Duplicate `specId` in one invocation** → fail before parse.
- **`operationId` collision** → fail by default; `--on-conflict prefix` renames in emitted artifacts only.
- **One part fails parse** → fail entire operation; no partial composite (same staged-write guarantee as `generate --force`).
- **Merge skills with different `scriptKind`** → **fail** if any source's `scriptKind` differs from the **canonical** choice. Canonical = the **existing target's** `scriptKind` when `--force` regenerates into an existing composite; otherwise all sources MUST match each other (no silent pick).
- **Include/exclude filters** → apply globally across merged model (unchanged semantics); per-part filters are out of scope for v1.
- **Examples** → union by `operationId`; orphaned examples after prefix rename emit warnings (existing example sync behavior).
- **Legacy creator install** (no `.creator-version.json`) → treated as `0.0.0`; `check-update` and `install-creator` offer update with user confirmation (D11).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `generate` MUST accept repeatable `--spec <source>` with optional `--spec-id` and `--spec-label` per occurrence (id defaults to slug of source; label defaults to id).
- **FR-002**: The generation manifest MUST record all parts in `specSources` and MUST remain secret-free.
- **FR-003**: Loaders MUST accept legacy manifests with singular `specSource` as a single-part composite.
- **FR-004**: A composition step MUST merge multiple parsed documents into one `SkillModel` before emit, applying D2–D4 collision rules.
- **FR-005**: `merge --into <target> <skill-path>…` MUST build a composite from **two or more** existing skills without deleting sources; it MUST NOT be used to append a single new document into an existing skill (D8 — use `add-spec`).
- **FR-006**: `add-spec <skill-path> <spec-source>` MUST append one part and regenerate in place with credential preservation.
- **FR-007**: `update` MUST refresh all recorded parts when no spec source argument is given.
- **FR-008**: `update` with a positional `<spec-source>` MUST require `--spec-id` when the manifest has more than one part; with a single part, `--spec-id` is optional and the positional replaces the only source (D10). After any replacement, all parts MUST be refreshed.
- **FR-013**: On `merge` / credential union, `secrets.json` and `.auth-cache.json` MUST be unioned by key (no silent deletion of credential files). When keys collide with different values, the first source MUST win and the CLI MUST warn (D9). Real credential values MUST NOT appear in committed skill artifacts. `auth.json`: first source that has one for a new target; preserve target on `--force` into existing (D9).
- **FR-009**: Composite skills MUST emit one `SKILL.md`, one dispatcher, and unified `reference/` per existing progressive-disclosure rules.
- **FR-010**: `SKILL.md` MUST include an **API parts** section for multi-part skills (hidden when only one part).
- **FR-011**: Bundled `api2skill-creator` MUST implement the [Creator skill contract](#creator-skill-contract-api2skill-creator) (multi-spec, authorization, base URL, secrets placement, self-update — D11).
- **FR-014**: `install-creator` MUST write `.creator-version.json` beside `SKILL.md` with the bundled tool version on every successful install.
- **FR-015**: `api2skill creator check-update` MUST scan all four standard project skill roots, compare installed vs bundled versions, and exit non-zero (code **8**) when any installation is older than bundled.
- **FR-016**: When an update is available, `install-creator` on a TTY MUST prompt the user before overwriting; agents using the creator skill MUST ask the user before invoking `install-creator` (never silent or agent-initiated `--force`).
- **FR-012**: All composite commands MUST use the same staged atomic write and preservation rules as `generate --force` / `update`.

### Key Entities

- **Spec part**: One OpenAPI source within a composite (`id`, `source`, optional `label`).
- **Composite manifest**: `.api2skill.json` with `specSources[]` plus existing generation options.
- **Composition result**: Single merged `SkillModel` with namespaced collisions resolved and warnings collected.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can produce one skill covering N≥2 OpenAPI documents on the same base URL with at most one `generate` command (or one `merge` command for existing skills).
- **SC-002**: Merging two existing skills **unions credential keys** from sources' `secrets.json` and `.auth-cache.json` into the target with **no silent deletion** of those credential files; on value conflicts, first-wins + CLI warning. `auth.json` follows D9 (first source with file for new target; preserve target on force into existing).
- **SC-003**: `update` on a composite skill with N parts succeeds without re-supplying original flags and reflects changes from all N upstream specs.
- **SC-004**: Agents using the updated creator skill can produce a valid multi-spec CLI command **and** correct post-generate secrets/base-url/auth instructions without undocumented steps.
- **SC-005**: Legacy single-spec skills continue to `update` without manifest migration steps.

- **SC-006**: After following the creator skill, a user can locate `secrets.json`, understand every placeholder key, and know which file holds OAuth client configuration vs runtime tokens.
- **SC-007**: Users are always prompted (CLI or agent chat) before an older `api2skill-creator` installation is replaced by a newer bundled version.

## Creator skill contract (api2skill-creator)

The bundled template `src/Api2Skill/templates/api2skill-creator/SKILL.md` (installed via `api2skill install-creator`) MUST teach agents to run this interview **before** emitting commands, then **after** generate/merge/add-spec walk through credential files.

### Multi-spec workflow (when N≥2 documents or skills)

| User situation | Ask | Emit |
|----------------|-----|------|
| New composite | Path/URL per doc; optional `--spec-id` / `--spec-label` per part | `generate --spec … --spec … --out …` |
| Existing separate skills | Confirm same base URL; list skill directories | `merge --into <target> <skill> …` |
| Grow existing skill | Path to new spec; unique `spec-id` | `add-spec <skill-path> <spec> --spec-id …` |
| Refresh composite | Whether any part's source changed | `update <path>` or `update <path> <new-spec> --spec-id …` |

Steer users to **`add-spec`** when appending one document to an existing skill (D8), not `merge`.

### Base URL

Agents MUST ask:

1. **What is the API base URL** callers should use (e.g. `https://api.example.com/v1`)?
2. Does each OpenAPI document define the **same** `servers` URL (or none)?
3. Is this **dev/staging** (may need `--insecure` for spec fetch or calls)?

**Emit `--base-url <url>` when**:

- Any spec has **no** `servers` entry.
- Multiple parts resolve to **different** URLs (user confirms they still want one skill — D2).
- User's stated canonical URL differs from the spec (override).

**Record in manifest**: `baseUrl` in `.api2skill.json` (secret-free). Remind user the dispatcher uses this unless `secrets.json` contains a top-level `baseUrl` key (when spec had no servers).

### Authorization

Agents MUST ask (per spec part when composite):

1. **Does this document require authentication?** (public vs protected operations)
2. **What auth types?** — API key, Bearer, Basic, OAuth2, Entra, custom headers, script token, or mixed across parts.
3. **One credential set or several?** — e.g. public API key + admin OAuth client.
4. **Generate-time auth flag**:
   - Simple single global profile: `--auth bearer|basic|custom`
   - OAuth2 / Entra / multi-profile / per-tag attach: `--auth-config <path>` (mutually exclusive with `--auth`)
   - Spec-only scaffold first: generate without auth flags → edit scaffolded `auth.json` → re-run with `--auth-config` + `--force`
5. **OAuth authorization-code**: need `--login` after generate? `callbackUrl` in `auth.json`?

For **multi-part** skills with different schemes, recommend **tag-level or global profiles** in `auth.json` and note scheme IDs may be prefixed on collision (`{specId}_{schemeId}`, D4). Point to `wiki/Authentication.md`. **Never invent** client IDs, secrets, or tokens.

### Secrets placement (post-generate)

After the user runs generate/merge/add-spec, agents MUST give **step-by-step** instructions:

1. **Change to the skill directory**: `cd <skill-dir>`.
2. **Start from the committed template**: `secrets.example.json` (generator output — placeholder values only).
3. **Create the real secrets file** (gitignored), either:
   - **Copy**: `cp secrets.example.json secrets.json`, then replace empty strings with real values; **or**
   - **Create** `secrets.json` with the **same JSON shape** as `secrets.example.json` (same keys, empty-string placeholders) if the user prefers the agent to write the file — then the user fills real values locally.
4. **Map keys to auth**: explain nested objects are keyed by **security scheme ID** from the spec (e.g. `api_key.apiKey`, `petstore_auth.clientId`). Top-level keys may come from `auth.json` `{secret:NAME}` references.
5. **OAuth**: client id/secret go in `secrets.json`; access tokens go in `.auth-cache.json` after `api2skill login --skill . --profile <name>` — do not put tokens in `secrets.json`.
6. **Never commit** `secrets.json` or `.auth-cache.json` (`.gitignore` is generated).

**Example** (agent may show after generate; placeholders only):

```json
{
  "$comment": "Fill in real values. Copy from secrets.example.json if this file does not exist yet.",
  "api_key": { "apiKey": "" },
  "admin_oauth": {
    "clientId": "",
    "clientSecret": "",
    "tokenUrl": "https://api.example.com/oauth/token",
    "scopes": ["api.read"]
  }
}
```

For **merge**: if sources already had `secrets.json`, explain the merged skill may have combined keys and that duplicate keys kept the **first source's** value (D9) with a CLI warning.

### Command order in creator session

0. **Self-update check** — run `api2skill creator check-update`; if update available, **ask user** before `install-creator` (D11).
1. Interview (specs, base URL, auth, out path, flags).
2. Emit **complete** `api2skill generate|merge|add-spec|update` command(s).
3. User runs command (or agent runs with approval).
4. Emit **secrets placement** steps and optional `auth.json` edit guidance.
5. If OAuth authorization-code: emit `api2skill login --skill . --profile <name>` when `auth.json` is ready.

### Self-update (D11)

| Step | Actor | Action |
|------|-------|--------|
| 1 | Agent | `api2skill creator check-update` from repo root |
| 2 | Agent | If `update available`, tell user bundled vs installed versions |
| 3 | User | Approves or declines update |
| 4 | Agent (if approved) | `api2skill install-creator --target <root>` — user confirms TTY prompt unless they explicitly said to use `--force` |
| 5 | Tool | Writes `SKILL.md` + `.creator-version.json` |

**Version file** (committed in skill dir after install; `version` matches the api2skill
package `<Version>`):

```json
{ "version": "<package Version>" }
```

Legacy installs missing this file are treated as `0.0.0` and offered an update.

## Assumptions

- Users know the paths/URLs of all Swagger documents (no auto-crawl).
- “Same API” means same canonical base URL after override, not merely same hostname with different paths.
- Per-part include/exclude filters are deferred; global filters from the manifest apply to the merged operation set.
- `auth.json` editing for multi-auth composites is manual or scaffold-assisted (same as today); this feature only ensures schemes and operations from all parts coexist.

## Grilling log (resolved 2026-07-28)

| # | Topic | Choice |
|---|--------|--------|
| Q1 | Merge vs add-spec for existing skill | **A** — `add-spec` to append; `merge` for combining multiple skill dirs into a target (D8) |
| Q2 | `secrets.json` key collision on merge | **A** — first source wins + warning (D9) |
| Q3 | Replacing one part on `update` | **A** — `--spec-id` required when N>1 parts (D10) |
