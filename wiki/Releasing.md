# Releasing

Package version for the `api2skill` .NET tool lives in
`src/Api2Skill/Api2Skill.csproj` (`<Version>`).

## Version bumps

Agents and contributors follow the project Cursor rule
[`.cursor/rules/version-bump.mdc`](../.cursor/rules/version-bump.mdc):

- Behavior or user-facing changes merging to `main` → bump `<Version>` (patch by default).
- Docs-only or `debug/`-only changes → no bump.
- Prefer a `chore(release): bump package version to X.Y.Z` commit, or include the bump in the
  same feature PR.
- Do not wait to be reminded — bump when the change qualifies.

## Tags

Git tags (and pushes of tags) are **human-gated**. Do not create or push tags unless a
maintainer asks.

Typical flow when a human requests a tagged release:

```bash
# After the Version bump is on main (or on the release commit)
git tag vX.Y.Z
git push origin vX.Y.Z
```

Publishing the tool package (e.g. NuGet) is separate from tagging and follows whatever
process maintainers use for that release.
