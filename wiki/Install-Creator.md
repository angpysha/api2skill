# Install Creator

```
api2skill install-creator [--target <skills-root>] [--force]
```

Installs the bundled **api2skill-creator** agent skill so an AI agent can interview you about
an OpenAPI API and emit the exact `api2skill generate` / `update` command (including auth,
`--login`, `callbackUrl`, script kind, and so on).

Layout on disk:

```text
<skills-root>/api2skill-creator/SKILL.md
```

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

**If the skill already exists:** refuses unless `--force`, or (on a TTY) you confirm overwrite.

## What the creator skill teaches

The installed `SKILL.md` tells the agent to ask about OpenAPI path/URL, `--out`, `--name`,
`--script`, `--auth` / `--auth-config`, `--login`, OAuth `callbackUrl` / token fields,
`--force`, and `--insecure` — then print copy-pasteable CLI commands. See also
[Generate Command](Generate-Command.md), [Update Command](Update-Command.md), and
[Authentication](Authentication.md).
