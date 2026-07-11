# Feature Specification: Skill Rename/Move During Update

**Feature Branch**: `feature/004-skill-rename-move-on-update`

**Created**: 2026-07-12

**Status**: Draft

**Input**: Deferred follow-up from feature 003 (beads `api2skill-4zx`, specs/003 T020): allow changing `--name` or output path during `update` without orphaning `secrets.json`, `auth.json`, or `.auth-cache.json`.

## Overview

Feature 003 introduced `api2skill update <skill-path> [<spec-source>]`, which regenerates a skill in place using options recorded in `.api2skill.json`. That command intentionally fixed the skill's **name** and **directory** to whatever was recorded at generation time — renaming or relocating during update was explicitly out of scope (specs/003-skill-update-command/spec.md Assumptions).

Users who regenerate from a newer spec sometimes also need to **rename** the skill (different `--name`) or **relocate** it (different `--out` / output directory) in the same operation — for example after reorganizing a monorepo or aligning folder names with a new API title. Today they must manually move directories and risk leaving `secrets.json`, `auth.json`, or `.auth-cache.json` behind at the old path.

This feature extends `update` with optional `--name` and `--out` flags (matching `generate`'s surface) so rename and/or move happen atomically alongside regeneration, with the same preservation guarantees as `generate --force` and feature 003's in-place update.

## Dependency on Feature 003

- Requires `.api2skill.json` (generation manifest) written by `generate` and rewritten by `update` (FR-001/FR-007 in 003).
- Builds on 003's delegation model: `update` reconstructs `GenerateOptions` and reuses the existing acquire → parse → build → emit → write pipeline.
- Does **not** change auth configuration semantics (003 FR-008 still applies).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rename a skill during update (Priority: P1)

A user generated a skill named `petstore-staging` in `./petstore-staging/`. The API title changed and they want the skill to be called `petstore-prod` while refreshing from a new OpenAPI document — without manually editing `SKILL.md` frontmatter or leaving credential files at a path they no longer use.

**Why this priority**: Rename-only is the most common deferred case and validates manifest + content rewrite without directory move complexity.

**Independent Test**: Generate a skill with a custom `--name`, add real `secrets.json`/`auth.json`, run `update <path> <new-spec> --name new-name`, and confirm the skill content reflects the new name, `.api2skill.json` records the new name, and all three credential/cache files remain present and byte-identical.

**Acceptance Scenarios**:

1. **Given** a skill at `./my-api/` with manifest `name: my-api`, **When** the user runs `update ./my-api/ <new-spec> --name my-api-v2`, **Then** regenerated `SKILL.md` and scripts use `my-api-v2`, the manifest records `my-api-v2`, and `secrets.json`, `auth.json`, and `.auth-cache.json` (if present) are preserved in `./my-api/`.
2. **Given** the same skill, **When** `update` completes with `--name`, **Then** no credential or cache file is left orphaned outside the target directory.

---

### User Story 2 - Relocate a skill during update (Priority: P1)

A user wants to move a generated skill from `./old-location/` to `./apis/petstore/` while updating from a newer spec, keeping OAuth session cache and secrets intact.

**Why this priority**: Move is the core deferred scenario from 003 T020 — orphaning auth state is the primary user pain.

**Independent Test**: Generate with non-default options and populated `secrets.json`/`auth.json`/`.auth-cache.json`, run `update ./old-location/ <new-spec> --out ./apis/petstore/`, confirm the new directory contains regenerated content plus preserved files, and the old directory is removed (or clearly reported if removal fails after a successful write).

**Acceptance Scenarios**:

1. **Given** a skill at `./old/` with credential files, **When** the user runs `update ./old/ <new-spec> --out ./new/`, **Then** `./new/` contains the regenerated skill with preserved `secrets.json`, `auth.json`, and `.auth-cache.json`, and `./old/` no longer exists after success.
2. **Given** `./new/` does not exist, **When** `update --out ./new/` succeeds, **Then** the manifest at `./new/.api2skill.json` reflects the invocation (including any new spec source and optional `--name`).
3. **Given** `./new/` already exists as an unrelated non-api2skill directory, **When** the user runs `update ./old/ --out ./new/`, **Then** the command fails clearly without modifying `./old/` or partially writing to `./new/`.

---

### User Story 3 - Rename and relocate together (Priority: P2)

A user changes both the skill name and output directory in one `update` invocation.

**Why this priority**: Combines US1 and US2; important but follows naturally once each works alone.

**Independent Test**: `update ./a/ <spec> --name b --out ./c/` produces `./c/` with name `b`, preserved secrets, and no `./a/`.

**Acceptance Scenarios**:

1. **Given** a skill at `./a/` with credentials, **When** the user runs `update ./a/ <spec> --name renamed --out ./c/`, **Then** `./c/` holds a skill named `renamed` with preserved credentials and `./a/` is gone.

---

### Edge Cases

- **No rename/move flags**: behavior identical to feature 003 — regenerate in place at `<skill-path>` using manifest name.
- **`--out` equals `<skill-path>`** (normalized absolute paths): treat as in-place update (no directory move).
- **Target directory exists and is a different api2skill skill** (has its own `.api2skill.json`): fail clearly — do not merge or overwrite another skill's credentials without explicit future `--force` semantics.
- **Target directory exists and is empty**: allow write (same as `generate` into empty dir).
- **Target directory exists with unrelated files only**: fail clearly (no partial clobber).
- **Move succeeds but old directory deletion fails**: new location must remain complete with preserved credentials; emit a clear warning/error naming the stale `./old/` path (operator can delete manually). Must not roll back a good regeneration.
- **Manifest rewrite**: after success, `.api2skill.json` at the **final** directory records the resolved name, spec source, and all other options from 003 — not the pre-update name/path.
- **Partial failure during staged write**: no partial output at destination; source directory unchanged until atomic success (same safety bar as 003 FR-005).
- **Auth configuration changes**: out of scope — `update` still never accepts `--auth`/`--auth-config`; existing files are preserved or copied, never replaced by new auth config.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `update` MUST accept optional `--name` and `--out` flags with the same meaning as `generate` (override skill name; override output directory).
- **FR-002**: When only `--name` is supplied, `update` MUST regenerate in place at `<skill-path>` with the new name recorded in output content and `.api2skill.json`.
- **FR-003**: When `--out` differs from `<skill-path>`, `update` MUST write the regenerated skill to the resolved output directory and MUST copy/preserve `secrets.json`, `auth.json`, and `.auth-cache.json` (and its lock file) from the source directory — never leave them orphaned at the source.
- **FR-004**: After a successful move, `update` MUST remove the source skill directory when it is no longer the target directory.
- **FR-005**: `update` MUST retain all feature 003 behavior when neither `--name` nor `--out` is supplied.
- **FR-006**: `update` MUST fail clearly, changing neither source nor destination, when the target path is an existing non-empty directory that is not the source skill directory.
- **FR-007**: After any successful `update` (with or without rename/move), `.api2skill.json` at the final directory MUST reflect the resolved name, spec source, and other recorded options from the invocation.
- **FR-008**: `update` MUST NOT alter auth configuration — preserved/copied `auth.json` is never replaced by newly generated auth config.
- **FR-009**: Rename/move during `update` MUST use the same staged-then-atomic-write safety guarantees as `generate --force` / feature 003 (no partial output on failure).

### Key Entities

- **Generation manifest** (`.api2skill.json`): unchanged schema from 003; `name` field updated when `--name` is supplied.
- **Source skill directory**: the `<skill-path>` argument — origin for preserved credential files and manifest load.
- **Target skill directory**: resolved from `--out` if supplied, otherwise `<skill-path>`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can rename a skill during update with a single `--name` flag without losing credential or cache files.
- **SC-002**: A user can relocate a skill during update with `--out` without orphaning `secrets.json`, `auth.json`, or `.auth-cache.json`.
- **SC-003**: Combined rename + move works in one command with the same preservation guarantees.
- **SC-004**: Invoking `update` without `--name`/`--out` behaves identically to feature 003 (regression-safe).
- **SC-005**: Failed rename/move attempts leave source and destination in a predictable state (no partial destination, source intact on write failure).

## Assumptions

- CLI surface mirrors `generate`: `--name` and `--out` (not a new `--move-to` flag) to minimize learning curve.
- The `<skill-path>` argument always identifies the **current** skill location; `--out` identifies the **new** location when moving.
- Directory name on disk may differ from skill `name` (same as `generate` today when `--out` differs from `./<name>`); this feature does not require renaming the folder when only `--name` changes.
- Skills without `.api2skill.json` remain unsupported (003 User Story 2) — one `generate --force` still required for legacy skills.
- Cross-filesystem moves use the same .NET `Directory.Move` / copy fallback patterns as the existing atomic write implementation.

## Traceability

| Source | Reference |
|--------|-----------|
| Deferred scope | specs/003-skill-update-command/spec.md Assumptions; tasks.md T020 |
| Beads | `api2skill-4zx` |
| Manifest dependency | specs/003-skill-update-command FR-001, FR-007 |
