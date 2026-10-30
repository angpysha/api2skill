# Spec Branch Extension

Spec Kit extension that creates the feature **git branch** right after `/speckit.specify`
using a deterministic script — the agent supplies only `type` and `description`; every
other segment is computed.

## Format

```text
{type}/{WW}-{spec-name}-{YYYY}-{description}
```

Example: `feature/42-014-session-filters-2026-add-date-filter`

| Segment | Rule |
|---------|------|
| `type` | `feature` \| `fix` \| `chore` \| `docs` \| `refactor` \| `test` \| `spike` |
| `WW` | ISO 8601 week, zero-padded (`01`–`53`) |
| `spec-name` | Basename of `feature_directory` in `.specify/feature.json`, slugified |
| `YYYY` | ISO week-based year (so `2027-01-01` → `53` / `2026`, never `53` / `2027`) |
| `description` | Slugified description; the only segment truncated to keep ≤ 244 chars |

## Why an extension (not a preset)

Presets can only replace/append command and template text. An extension can register a
**mandatory `after_specify` hook** and ship a script, so the branch is created by code at a
fixed point in the flow (after the spec directory and `feature.json` exist) instead of by
agent judgement.

## Script

```powershell
$SB = '.specify/extensions/spec-branch/scripts/create-spec-branch.ps1'
pwsh $SB -Type feature -Description 'add date filter' -DryRun        # preview
pwsh $SB -Type feature -Description 'add date filter' -Json          # create + checkout
pwsh $SB -Type feature -Description 'add date filter' -AllowExisting # re-enter
pwsh $SB -Help
```

Options: `-SpecName` (override `feature.json`), `-Date yyyy-MM-dd` (pin week/year for
reproducibility), `-Json` (single JSON object with `BRANCH_NAME`, `ACTION`, segments).

`ACTION` is `created`, `checked_out`, or `already_on`. The script never pushes and exits
non-zero with an `ERROR:` line on invalid input.

## Install / remove

```bash
specify extension add --dev /path/to/ai-dev-pipeline/speckit-tools/extensions/spec-branch
specify extension list
specify extension remove spec-branch
```

## Relation to `branch-format` preset

This extension supersedes the `{type}/{week}-{slug}` naming of the `branch-format` preset
for Spec Kit features. If both are installed, the `after_specify` hook of this extension
is the branch source of truth.
