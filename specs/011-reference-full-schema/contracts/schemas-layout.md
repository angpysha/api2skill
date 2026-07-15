# Contract — `reference/schemas/` layout

Emitted under every generated skill that uses named OpenAPI component schemas.

## Layout

```text
<skill>/
└── reference/
    ├── <tag>.md
    └── schemas/
        ├── Pet.json
        ├── PetInput.json
        └── …
```

## File rules

| Rule | Detail |
|------|--------|
| Naming | `<ExactComponentName>.json` matching `components.schemas` key |
| Content | Raw OpenAPI Schema Object as JSON (pretty, deterministic) |
| `$ref` | Keep `#/components/schemas/<Other>` form from source |
| Set | Only schemas reachable from emitted operations |
| Empty | If no named schemas used, omit `schemas/` directory (or leave empty — prefer omit) |
| Regenerate | Always rewritten on `generate` / `update` / `--force` (not user-authored; not preserved like `examples/`) |

## Cross-links

From `reference/<tag>.md`:

```markdown
- Schema: [`Pet`](schemas/Pet.json)
```

Relative path from tag MD to schema file: `schemas/<Name>.json`.
