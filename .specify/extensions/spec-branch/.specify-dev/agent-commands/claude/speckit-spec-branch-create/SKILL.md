---
name: speckit-spec-branch-create
description: Create/checkout the feature git branch {type}/{week}-{spec-name}-{year}-{description} via a deterministic script
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: spec-branch:commands/create.md
---

# Spec branch: create

You **MUST** consider `$ARGUMENTS` (may contain `type`, `description`, or an explicit
spec name). The branch name is computed **only** by the script below — never compose,
shorten, or "fix" it by hand.

## Branch format

```text
{type}/{WW}-{spec-name}-{YYYY}-{description}
```

| Segment | Source (deterministic) |
|---------|------------------------|
| `type` | `feature` \| `fix` \| `chore` \| `docs` \| `refactor` \| `test` \| `spike` (from context or the user) |
| `WW` | ISO 8601 week of today, zero-padded (`01`–`53`) |
| `spec-name` | Basename of `feature_directory` in `.specify/feature.json` (e.g. `014-session-filters`) |
| `YYYY` | ISO week-based year matching `WW` |
| `description` | Short description from context or the user, slugified by the script |

Example: `feature/42-014-session-filters-2026-add-date-filter`

## Steps

1. Ensure `/speckit.specify` already wrote `.specify/feature.json` (this runs as an
   `after_specify` hook, so it should exist). If it is missing, stop and report.
2. Determine `-Type` and `-Description` from `$ARGUMENTS` / conversation. If either is
   missing or ambiguous → **ask the user** (do not invent).
3. Dry-run, then create (from the repository root):

```powershell
$SB = '.specify/extensions/spec-branch/scripts/create-spec-branch.ps1'
pwsh $SB -Type <type> -Description "<text>" -DryRun -Json
pwsh $SB -Type <type> -Description "<text>" -Json
# Re-entering an existing local branch:
pwsh $SB -Type <type> -Description "<text>" -AllowExisting -Json
```

4. Report `BRANCH_NAME` and `ACTION` from the JSON output. Uncommitted spec files carry
   over to the new branch; do not commit unless the user asks.

## Rules

- Do **not** push, and do **not** rename remote branches.
- Do **not** rename the Spec Kit feature directory to match the branch.
- This command supersedes the `branch-format` preset / skill naming (`{type}/{week}-{slug}`)
  for Spec Kit features when both are installed.
- If the script exits non-zero, show its `ERROR:` line and stop — do not fall back to a
  hand-made branch name.