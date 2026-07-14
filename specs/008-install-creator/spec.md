# Feature Specification: install-creator (api2skill-creator skill)

**Feature Branch**: `debug/oauth-callback-entra` (no new branch)

**Created**: 2026-07-14

**Status**: Approved (grilled; paths rechecked against official docs)

**Input**: CLI command that installs a packaged `api2skill-creator` agent skill into one or more project skill roots so an agent can interview the user about an API and assemble correct `api2skill` CLI arguments.

## Supported agent skill roots (project-scoped) — count: 4

Documented from official sources (2026-07-14):

| # | Host | Project path | Doc basis |
|---|------|--------------|-----------|
| 1 | Cursor | `.cursor/skills/` | [cursor.com/docs/skills](https://cursor.com/docs/skills) |
| 2 | Claude Code | `.claude/skills/` | [code.claude.com/docs/en/skills](https://code.claude.com/docs/en/skills.md) |
| 3 | GitHub Copilot | `.github/skills/` | [docs.github.com Copilot agent skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills) |
| 4 | Agentic / shared | `.agents/skills/` | Copilot + Cursor also load this root |

**Overlap note:** Copilot also discovers skills under `.claude/skills` and `.agents/skills`. Cursor also loads `.agents/skills` and compatibility `.claude/skills`. Installing only to `.github/skills` is the Copilot-primary target; multi-select may intentionally install to several roots.

**Out of scope for v1 project picker:** personal dirs (`~/.cursor/skills`, `~/.claude/skills`, `~/.copilot/skills`, `~/.agents/skills`).

Install layout: `<root>/api2skill-creator/SKILL.md` (+ optional references).

## Clarifications (grill)

| Topic | Decision |
|-------|----------|
| Command | `api2skill install-creator` |
| `--target <dir>` | Install into that skills root (directory that will contain `api2skill-creator/`) |
| No `--target` + TTY | Interactive multi-select: ↑↓ move, Space toggle, Enter confirm — 4 hosts above |
| No `--target` + non-TTY | Error: require `--target` |
| Exists already | Refuse unless `--force` or interactive overwrite confirm |
| Multi-select | Yes (Space) |
| Template source | Bundled in the NuGet/tool package (e.g. `templates/api2skill-creator/`) |

## User Scenarios & Testing

### User Story 1 - Interactive install (Priority: P1)

Developer runs `api2skill install-creator` in a repo with a TTY, selects Cursor + Copilot, confirms. Skill appears under `.cursor/skills/api2skill-creator/` and `.github/skills/api2skill-creator/`.

### User Story 2 - Non-interactive `--target` (Priority: P1)

CI/script: `api2skill install-creator --target .github/skills --force` installs without prompts.

### User Story 3 - Creator skill content (Priority: P1)

Installed skill teaches the agent to ask about OpenAPI path, out dir, auth, script kind, login, etc., then emit the exact `api2skill generate|update …` command.

### Edge Cases

- None of the 4 roots exist → picker still lists them; install creates the chosen directory tree.
- `--target` points at wrong level (e.g. repo root without `/skills`) → validate/normalize or clear error that path must be a skills root.
- Partial failure on multi-target → report per-target success/failure.

## Requirements

- **FR-001**: Command `install-creator` with `--target`, `--force`.
- **FR-002**: Spec MUST list exactly **4** supported project agents/roots (table above).
- **FR-003**: Interactive picker with ↑↓ / Space / Enter when TTY and no `--target`.
- **FR-004**: Non-TTY without `--target` → non-zero exit + message.
- **FR-005**: Ship template `api2skill-creator` with SKILL.md covering generate/update/auth/login.
- **FR-006**: Docs in README + wiki for the command and the 4 hosts.

## Success Criteria

- SC-001: Interactive and `--target` paths both install a loadable `SKILL.md`.
- SC-002: Copilot path is `.github/skills` per GitHub docs.
- SC-003: Spec Kit artifact retains the supported-agent count (4) and paths.
