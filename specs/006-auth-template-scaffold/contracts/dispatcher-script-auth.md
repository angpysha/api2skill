# Contract: Script auth working directory

Applies to all generated dispatchers (`.cs`, `.fsx`, `.csx`) after feature 006.

## Resolution

```text
scriptDir  = directory containing scripts/call.{cs,fsx,csx}
skillRoot  = Path.GetFullPath(Path.Combine(scriptDir, ".."))
```

`skillRoot` is the directory containing `auth.json`, `secrets.json`, and `SKILL.md`.

## Process start

When executing a `script` auth profile's `command`:

```text
ProcessStartInfo.WorkingDirectory = skillRoot
```

All other `ProcessStartInfo` fields unchanged (shell wrapper on Windows/Unix, stdout/stderr
redirect, non-zero exit fails the call).

## Parity

Identical behavior across `cs`, `fsx`, and `csx` emitters (FR-008).

## Rationale

Relative paths in user commands (e.g. `./get-token.sh`, `az account …` with local config files
colocated in the skill folder) must not depend on the caller's cwd when running
`dotnet run` / `dotnet fsi` / `dotnet script` from an arbitrary directory.
