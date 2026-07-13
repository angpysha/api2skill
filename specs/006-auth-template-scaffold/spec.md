# Feature Specification: Auth Template Scaffold & Script Working Directory

**Feature Branch**: `006-auth-template-scaffold`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "Add empty auth.json generation (prefilled auth json without specific info), create guidance on what profile names should be created in auth to avoid wrong config, and add current directory (skill root) for script-type auth command execution."

## Clarifications

### Session 2026-07-13

- Q: How should users invoke auth.json scaffolding? → A: Via `generate` (not a separate subcommand). Write `auth.json` to the skill output folder using the default filename — no separate output path argument.
- Q: When should auth.json scaffolding run during generate? → A: Automatically when neither `--auth` nor `--auth-config` is supplied and the spec has at least one referenced security scheme.
- Q: How should scaffolded profiles attach to operations? → A: Emit a minimal global-attach template as the active profiles, plus commented tag-attach examples per tag showing how to switch scope when tags use different schemes.
- Q: How should scaffolding handle unsupported OpenAPI security schemes? → A: Omit silently from scaffolded profiles for now (unsupported scheme support deferred). Users manually configure custom headers / auth for those schemes when needed.
- Q: Where should profile-naming guidance appear? → A: Both — compact scheme→profile mapping in scaffolded `auth.json` (`$comment` / `_guidance`) and a human-readable "Auth profile names" section in generated `SKILL.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scaffold auth.json from an OpenAPI spec (Priority: P1)

A developer generating a skill for an API with one or more security schemes wants a starting
`auth.json` in the skill folder they can edit and then activate with `--auth-config`, instead of
hand-authoring the file from the contract docs. During `generate`, the tool reads the spec,
infers one profile per referenced security scheme (with placeholder `{secret:…}` values only),
and writes a committed-safe `auth.json` template (default name, skill output directory) plus
inline guidance on profile naming.

**Why this priority**: Unblocks the most common multi-profile / OAuth / script auth setups and
reduces misconfiguration from guessing profile names or types.

**Independent Test**: Run the scaffold command against a fixture spec with bearer + apiKey
schemes; verify the output validates, names profiles after scheme IDs, and contains no real
credentials.

**Acceptance Scenarios**:

1. **Given** an OpenAPI document with two referenced security schemes (`BearerAuth`, `ApiKeyAuth`),
   **When** the user runs `generate` with auth scaffolding enabled, **Then** the skill folder
   contains `auth.json` with two profiles whose `name` values match those scheme IDs and whose
   `type` values match the inferred mechanism (bearer / custom header).
2. **Given** a scaffolded `auth.json` in the skill folder, **When** the user fills
   `secrets.json` and re-runs `generate --force --auth-config <skill-dir>/auth.json`, **Then**
   generation succeeds (exit 0) without profile-name or header-collision errors for the default
   scaffold shape.
3. **Given** an OpenAPI document with no security schemes, **When** the user runs `generate`
   without `--auth` or `--auth-config`, **Then** no `auth.json` is written and generation
   completes normally (no error).

---

### User Story 2 - Profile naming guidance avoids wrong config (Priority: P1)

A developer editing a scaffolded or hand-written `auth.json` needs to know which profile names
must exist so explicit auth attaches to the right operations. The tool surfaces this guidance
when scaffolding and in generated skill docs when explicit auth is used.

**Why this priority**: Wrong profile names silently fall back to spec-derived auth (FR-006), which
is confusing; naming guidance is the main value-add beside the template itself.

**Independent Test**: Scaffold from `multi-auth.yaml`; verify output includes a mapping comment
or companion section listing each scheme ID → suggested profile name → applicable operations or
tags.

**Acceptance Scenarios**:

1. **Given** a spec where operation `getPet` requires scheme `petstore_auth`, **When** auth is
   scaffolded, **Then** the output documents that a profile named `petstore_auth` (matching the
   scheme ID) is required for explicit auth to override that operation.
2. **Given** multiple operations sharing one scheme, **When** guidance is emitted, **Then** the
   scheme ID appears once with a summary of affected operation IDs or tags (not duplicated per
   operation).

---

### User Story 3 - Script auth runs with skill-root working directory (Priority: P2)

A developer uses a `script` auth profile whose command relies on relative paths (e.g. a local
`get-token.sh` next to `auth.json`). When the dispatcher runs the command, it uses the skill
root directory (parent of `scripts/`) as the process working directory so relative paths resolve
consistently regardless of where the user invoked `dotnet run`.

**Why this priority**: Fixes a real runtime bug for script auth; independent of scaffolding but
bundled here because it was reported alongside auth.json work.

**Independent Test**: Generate a skill with script auth whose command writes a sentinel file to
`.`; verify the file appears in the skill root, not the caller's cwd.

**Acceptance Scenarios**:

1. **Given** a generated skill with a script profile, **When** an operation is invoked, **Then**
   the script subprocess `WorkingDirectory` is the skill root (directory containing
   `auth.json`).
2. **Given** the same skill, **When** invoked from a different cwd, **Then** script auth behavior
   is identical (stdout still becomes the header value).

---

### Edge Cases

- OpenAPI scheme kind is `Unsupported` (e.g. openIdConnect): omitted from scaffold silently;
  user adds a `custom` (or other) profile manually when ready. Full unsupported-scheme inference
  is out of scope for this feature (future work).
- Duplicate scheme IDs or empty scheme names: validation error at scaffold time.
- OAuth2 `authorization_code` vs `client_credentials`: scaffold infers grant from available flows
  in the spec when possible; otherwise placeholder with `$comment` telling user to choose.
- Tag-scoped auth: scaffold emits active profiles with global attach, and `$comment`-annotated
  tag-attach examples per tag for users who need tag-scoped auth.
- Regenerating an existing skill: script cwd fix applies to newly generated dispatchers only
  (existing skills need `update` / `generate --force`).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `generate` MUST automatically emit a secret-free, prefilled `auth.json` template
  when neither `--auth` nor `--auth-config` is supplied and the spec has at least one referenced
  security scheme. Output is written to the skill directory as `auth.json` (default name, no
  separate output-path argument). When the spec has zero referenced schemes, no `auth.json` is
  written.
- **FR-001a**: On `--force`, an existing `auth.json` in the target skill directory MUST be
  preserved (same policy as today); scaffold runs only when no `auth.json` exists yet.
- **FR-002**: Each scaffolded profile MUST use the OpenAPI security scheme **ID** as its
  `name` (stable, matches operation `SecuritySchemeIds` and reference docs).
- **FR-003**: Scaffolded profiles MUST use `{secret:NAME}` placeholders only — no literal
  credentials (Constitution IV).
- **FR-004**: Scaffold output MUST map each supported scheme kind to the correct auth profile
  `type`: bearer, basic, custom (apiKey header/query), oauth2 (when inferrable), with
  `$comment` fields explaining required user edits for oauth2/script.
- **FR-004a**: Active scaffolded profiles MUST use global attach (`attach` omitted or
  `"scope": "global"`). For each tag that references a distinct scheme mix, the scaffold MUST
  also include commented-out tag-attach profile examples (`"attach": { "scope": "tags",
  "tags": ["…"] }`) so users can enable tag-scoped auth without guessing syntax.
- **FR-004b**: Referenced security schemes classified as `Unsupported` MUST be omitted from
  scaffolded active profiles (no warning required in v1). Profile-naming guidance SHOULD still
  list them so users know to add manual `custom` header profiles if needed.
- **FR-005**: Profile-naming guidance MUST appear in two places: (1) compact scheme ID → profile
  name → operations/tags mapping inside scaffolded `auth.json` via `$comment` / `_guidance`
  fields, and (2) a human-readable **Auth profile names** section in generated `SKILL.md` when
  auto-scaffold runs (lists required profile names, supported vs manual-only schemes).
- **FR-006**: Scaffolded `auth.json` MUST pass existing `AuthConfigLoader` validation when the
  user has not yet customized unsupported fields (or clearly mark sections that need edit before
  use).
- **FR-007**: Generated dispatchers (`cs`, `fsx`, `csx`) MUST set `WorkingDirectory` to the skill
  root when executing `script` auth commands.
- **FR-008**: Script working-directory behavior MUST be identical across all three emitters.
- **FR-009**: Documentation (`wiki/Authentication.md` and/or generate wiki) MUST describe the
  auto-scaffold workflow, profile-naming rules, and the edit-then-`--auth-config` activation flow.

### Key Entities

- **Auth scaffold output**: A committed-safe `auth.json` (and optionally matching
  `secrets.example.json` keys) derived from spec security schemes.
- **Scheme-to-profile map**: Logical mapping from OpenAPI `components.securitySchemes` ID to
  auth profile `name` and `type`.
- **Profile naming guidance**: Annotations listing scheme IDs, suggested names, and operation/tag
  coverage so users avoid silent fallback to spec-derived auth.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can go from OpenAPI spec to a skill folder containing a validatable
  `auth.json` template in one `generate` run without reading the full auth contract first.
- **SC-002**: 100% of referenced, supported security schemes in test fixtures appear as
  scaffolded profiles with matching `name` equal to the scheme ID.
- **SC-003**: Script auth integration tests pass when commands use relative paths from skill root,
  regardless of process cwd at invocation.
- **SC-004**: Zero scaffold or generated committed artifacts contain non-placeholder credential
  values (existing no-leakage tests extended).

## Assumptions

- Profile names matching OpenAPI scheme IDs is the least surprising default (already used in
  reference docs and dispatcher spec-derived path).
- Scaffold runs as part of `generate` and writes `auth.json` into the skill folder; activating
  explicit auth still requires a subsequent `generate --auth-config` (or supplying
  `--auth-config` on the same run).
- Script auth working directory is the skill root (`auth.json` directory), not `scripts/`.
- Unsupported scheme kinds are omitted from active profiles in v1; users add `custom` profiles
  manually. Full inference for unsupported schemes is deferred to a future feature.
