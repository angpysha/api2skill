# Feature Specification: Multi-spec merge/generate bugfixes

**Feature Branch**: `fix/014-multi-spec-merge-bugs`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User report of six bugs in api2skill 0.7.0 during real multi-spec generate/merge of five OpenAPI docs into one composite skill (tag path crash, merge re-fetch, `file://` baseUrl, auth.json guidance drop, empty `AuthProfileNames`, global Authorization collision).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Safe reference files for free-form tags (Priority: P1)

A developer generates (or merges) a skill from an OpenAPI document whose tag names contain filesystem-unsafe characters such as `/` (e.g. `Group / Item`). Generation succeeds and writes a single reference markdown file under `reference/` whose name is a sanitized path segment; `SKILL.md` links point at that same sanitized file.

**Why this priority**: Blocking crash (`DirectoryNotFoundException`) on otherwise valid OpenAPI.

**Independent Test**: Generate from a minimal OpenAPI with tag `Group / Item`; assert no exception and `reference/` contains a sanitized `.md` file; assert `SKILL.md` links match.

**Acceptance Scenarios**:

1. **Given** a spec with tag `Group / Item`, **When** `generate`/`update`/`merge` runs, **Then** no path exception is thrown and a reference file is written.
2. **Given** that tag, **When** `SKILL.md` is written, **Then** reference links use the same sanitized file name as the written file.

---

### User Story 2 - Offline-friendly merge using cached specs (Priority: P1)

A developer generates each part skill successfully, then runs `merge` without the original remote OpenAPI URLs being reachable. Merge reconstitutes the composite from cached spec documents stored inside each source skill directory (written at generate/update time), falling back to live `specSource` only when no cache exists.

**Why this priority**: Merge currently re-fetches every `specSource`, re-introducing transient generate failures and breaking offline/idempotent merge.

**Independent Test**: Generate a skill, delete/block network to the original URL, `merge` two skills that have local caches → succeeds using caches.

**Acceptance Scenarios**:

1. **Given** skills with `.api2skill/specs/<id>.*` caches, **When** `merge` runs, **Then** those files are preferred over remote URLs.
2. **Given** a legacy skill with no cache, **When** `merge` runs, **Then** behavior falls back to fetching `specSource` as today.

---

### User Story 3 - Usable composite base URL (Priority: P1)

When composing multiple parts, a part whose OpenAPI has no usable HTTP(S) `servers` entry (or resolves to a `file:` URL) must not force the composite `baseUrl` to `file://`. Prefer `--base-url`, then any HTTP(S) base from other parts/manifests; if none exist, fail with a clear error requiring `--base-url`.

**Why this priority**: Silent `file://` composite baseUrl breaks every runtime HTTP call.

**Independent Test**: Compose one HTTPS part + one server-less local part without `--base-url` → composite `baseUrl` is the HTTPS URL (or explicit error if none).

**Acceptance Scenarios**:

1. **Given** parts with mixed HTTPS and unset/`file:` bases and no override, **When** compose runs, **Then** composite uses the HTTPS base (or errors if none).
2. **Given** `--base-url`, **When** compose runs, **Then** that value wins.

---

### User Story 4 - Composite auth guidance lists every final scheme id (Priority: P2)

After scheme-id prefixing on merge/multi-spec generate, the written `auth.json` `_guidance.schemes` section documents every final (possibly prefixed) scheme id used by the dispatcher, with operations/tags — not only the first source's original ids.

**Why this priority**: Humans cannot wire auth for `api2_oauth2`-style ids that the code references.

**Independent Test**: Merge two skills that both declare scheme `oauth2` → composite auth guidance includes `oauth2` and `api2_oauth2` (or equivalent prefixed id).

**Acceptance Scenarios**:

1. **Given** colliding scheme ids across parts, **When** composite auth is written, **Then** `_guidance.schemes` lists every final scheme id.

---

### User Story 5 - Auth profiles wire into the dispatcher (Priority: P1)

When `--auth-config` is supplied (or a scaffolded `auth.json` with active profiles is produced), each operation's emitted `AuthProfileNames` is non-empty for profiles that apply — matching profile name to the operation's security scheme id when unscoped, or via `attach.tags`. Login + call uses `.auth-cache.json` via the explicit-profile path.

**Why this priority**: `login` is useless if `AuthProfileNames` is always `[]`.

**Independent Test**: Generate with `--auth-config` where profile name equals scheme id → `BuildOperations()` entry has non-empty `AuthProfileNames`.

**Acceptance Scenarios**:

1. **Given** profile `oauth2` and scheme `oauth2`, **When** generate runs with `--auth-config`, **Then** operations using that scheme have `AuthProfileNames` containing `oauth2`.
2. **Given** a profile that matches no scheme and no tags, **When** generate completes, **Then** a warning notes the profile is never referenced.

---

### User Story 6 - Multiple Bearer-style profiles for different schemes (Priority: P1)

Two unscoped profiles that both set `Authorization` but whose names match different security scheme ids do not collide; each applies only to operations that use its scheme. Collision errors name the competing scheme ids when relevant.

**Why this priority**: Blocks declaring interactive OAuth + script Bearer profiles in one `auth.json`.

**Independent Test**: Auth config with profiles `oauth2` and `service` (both Authorization) + ops using those scheme ids → generate succeeds; each op gets only its matching profile.

**Acceptance Scenarios**:

1. **Given** two unscoped Authorization profiles named after distinct scheme ids, **When** resolve runs, **Then** no collision and each op gets the matching profile only.
2. **Given** two unscoped Authorization profiles that are both truly global (names match no scheme), **When** resolve runs on any op, **Then** collision error still fires.

---

### Edge Cases

- Tag sanitization collisions (`A/B` vs `A-B`) → disambiguate with a numeric suffix.
- Cache present but corrupt → fall back to live source with a warning.
- All parts lack HTTP(S) base and no `--base-url` → hard error, no `file://` written.
- Preserved first-wins `auth.json` profiles kept, but `_guidance` refreshed from composed model.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sanitize OpenAPI tag names before using them as `reference/` path segments; keep display titles unsanitized; keep SKILL.md links consistent.
- **FR-002**: Persist acquired OpenAPI bytes per spec part under the skill directory at generate/update; `merge` prefers those caches.
- **FR-003**: Composite base URL ignores non-HTTP(S) candidates; prefer override then any HTTP(S) part; otherwise error.
- **FR-004**: After composition, refresh `auth.json` `_guidance.schemes` for all final scheme ids.
- **FR-005**: Populate `AuthProfileNames` via scheme-id name match and tag attach; warn on unused profiles.
- **FR-006**: Unscoped profiles named after a scheme apply only to operations using that scheme (fixes Authorization collisions across schemes).

### Key Entities

- **Spec cache**: per-part raw OpenAPI document under `.api2skill/specs/`.
- **Attachment resolution**: maps profiles → operations using scheme id and tags.
- **Reference path segment**: sanitized tag file stem.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tag containing `/` no longer crashes generate/merge.
- **SC-002**: Merge of two cached skills succeeds without network to original URLs.
- **SC-003**: Composite never writes `baseUrl` of `file://` when any HTTP(S) candidate or `--base-url` exists; otherwise fails clearly.
- **SC-004**: Generated dispatcher `AuthProfileNames` non-empty when `--auth-config` profile matches a scheme id.
- **SC-005**: Two scheme-named Authorization profiles generate successfully.
- **SC-006**: Composite `_guidance.schemes` includes every prefixed scheme id.
