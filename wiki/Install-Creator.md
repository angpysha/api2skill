# Install Creator

```
api2skill install-creator [--target <skills-root>] [--force]
```

Installs the bundled **api2skill-creator** agent skill so an AI agent can interview you about
an OpenAPI API and emit the exact `api2skill generate` / `update` / `merge` / `add-spec`
command (including auth, base URL, secrets placement, `--login`, `callbackUrl`, script kind,
and so on).

Layout on disk:

```text
<skills-root>/api2skill-creator/SKILL.md
<skills-root>/api2skill-creator/.creator-version.json   # matches package <Version>
```

## Prefer Plan mode for multi-spec

When setting up **several** Swagger docs (or deciding merge vs generate), use **Cursor Plan
mode** (or another plan-first agent workflow) with the creator skill loaded. The agent should
plan interview questions — auth per part, shared base URL, secrets placement, merge vs
`generate --spec` vs `add-spec` — **before** emitting shell commands. See
[Multi-Spec Skills](Multi-Spec-Skills.md) and the interactive walkthrough
[`docs/interactive/multi-spec-guide.html`](../docs/interactive/multi-spec-guide.html)
([Interactive Guides](Interactive-Guides.md) for Canvas / Artifact / Sites).

## Supported project skill roots (exactly 4)

| # | Host | Project path |
|---|------|--------------|
| 1 | Cursor | `.cursor/skills/` |
| 2 | Claude Code | `.claude/skills/` |
| 3 | GitHub Copilot | `.github/skills/` |
| 4 | Agentic / shared | `.agents/skills/` |

Personal home directories (`~/.cursor/skills`, `~/.claude/skills`, etc.) are out of scope for
this command’s interactive picker. Pass `--target` if you need a custom path.

**Overlap:** Copilot also discovers `.claude/skills` and `.agents/skills`; Cursor also loads
`.agents/skills` and compatibility `.claude/skills`. Multi-select can install into several
roots on purpose.

## Options

| Option | Description |
|--------|-------------|
| `--target <dir>` | Skills root that will contain `api2skill-creator/` (e.g. `.cursor/skills`) |
| `--force` / `-f` | Overwrite an existing `api2skill-creator/SKILL.md` without prompting |

## Modes

**With `--target`** (scripts / CI):

```bash
api2skill install-creator --target .github/skills --force
```

**Without `--target` on a TTY:** interactive multi-select of the four hosts above
(↑↓ move, Space toggle, Enter confirm). Missing root directories are created as needed.

**Without `--target` when stdin/stdout is not a TTY:** exits non-zero and asks for `--target`.

**If the skill already exists:** refuses unless `--force`, or (on a TTY) you confirm overwrite. When the bundled version is **newer** than `.creator-version.json`, the prompt says *"Update available … Install update? [y/N]"*.

**Check for updates without installing:**

```bash
api2skill creator check-update
```

Scans `.cursor/skills`, `.claude/skills`, `.github/skills`, and `.agents/skills` under the
current directory. Exit code **8** when any installed copy is older than the bundled skill
(exit **0** when up to date or not installed). The bundled version always matches the
api2skill package `<Version>` in `Api2Skill.csproj`.

## What the creator skill teaches

The installed `SKILL.md` tells the agent to interview on OpenAPI path/URL(s), base URL,
secrets placement, `--out`, `--name`, `--script`, `--auth` / `--auth-config`, `--login`,
OAuth `callbackUrl` / token fields, `--force`, and `--insecure` — including multi-spec
`generate --spec`, `merge --into`, `add-spec`, and multi-part `update` — then print
copy-pasteable CLI commands. Prefer Plan mode for that interview on multi-spec setups.

See also [Multi-Spec Skills](Multi-Spec-Skills.md), [Generate Command](Generate-Command.md),
[Update Command](Update-Command.md), and [Authentication](Authentication.md).
