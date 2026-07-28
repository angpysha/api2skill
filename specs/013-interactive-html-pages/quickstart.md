# Quickstart — Interactive HTML multi-spec guide

Validate feature `013-interactive-html-pages` after implementation.

## Prerequisites

- Repo checkout on branch with 013 assets
- Any modern browser (Chrome/Firefox/Safari/Edge)

## 1. Open offline

```bash
# From repo root — open in default browser (macOS)
open docs/interactive/multi-spec-guide.html
# Or: xdg-open / start per OS
```

**Expect**: Page loads; scenario picker visible; no console errors requiring network.

Optional: DevTools → Network → Offline → reload → copy button still works (SC-003).

## 2. Scenario commands

| Select | Expect command contains |
|--------|-------------------------|
| New composite | `generate` and `--spec` and ideally `--base-url` |
| Merge | `merge --into` |
| Add-spec | `add-spec` |
| Update | `update` (and mention of `--spec-id` for multi-part) |

Compare wording to `wiki/Multi-Spec-Skills.md`.

## 3. Wiki hosts section

Open `wiki/Multi-Spec-Skills.md` or `wiki/Interactive-Guides.md` and confirm subsections for Cursor, Copilot, Claude, Codex exist with paste fallback.

## 4. README + creator

```bash
grep -n 'docs/interactive/multi-spec-guide' README.md wiki/Multi-Spec-Skills.md
grep -n 'Plan mode\|multi-spec-guide' src/Api2Skill/templates/api2skill-creator/SKILL.md
```

**Expect**: Links/paths present; Plan mode mentioned for multi-spec.

## 5. Generate unchanged

```bash
# Smoke: generate still does not emit HTML into skill dir by default
dotnet run --project src/Api2Skill -- generate tests/Api2Skill.Tests/fixtures/petstore.json -o /tmp/a2s-013-smoke --force
test ! -f /tmp/a2s-013-smoke/*.html
```

(Adjust fixture path if needed.)
