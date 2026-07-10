# Contract — Generated skill output

The directory api2skill emits. This is the contract consumed by Claude when the skill is loaded,
and the surface golden tests assert against.

## Directory layout

```text
<slug>/                       # e.g. swagger-petstore/
├── SKILL.md                  # compact entry: overview + auth setup + tag-grouped op index
├── reference/
│   ├── <tag>.md              # full param/schema/response detail per tag (on-demand)
│   └── …
├── scripts/
│   └── call.<ext>            # single dispatcher (.cs | .fsx | .csx per --script)
├── secrets.example.json      # committed template (placeholder values)
└── .gitignore                # excludes secrets.json
```

`secrets.json` (real credentials) is **created by the user**, never by the generator, and is
gitignored.

## SKILL.md structure (contract)

1. **Frontmatter**: `name` (slug), `description` (one line from `info.title`/`info.description`).
2. **Overview**: what the API is; base URL (or "supply `baseUrl` in secrets.json").
3. **Setup**: which runner the emitter needs (FR-006b) + how to fill `secrets.json` (copy from
   `secrets.example.json`); dev-only `--insecure` note if relevant (FR-007).
4. **How to call**: the dispatcher usage line — `<runner> scripts/call.<ext> <operationId> [--param …]`.
5. **Operations** (the index): grouped by tag; each row = `operationId` — one-line summary →
   `reference/<tag>.md`. **No per-parameter detail here** (progressive disclosure, FR-004).

MUST stay compact — detail belongs in `reference/` (NFR-2 / SC-003).

## Dispatcher contract (call.<ext>)

Invoked: `<runner> scripts/call.<ext> <operationId> [--<param> <value> …] [--body <json|@file>]`.

- Resolves the operation by `operationId`; unknown id → non-zero + list of valid ids.
- Builds the request: path substitution, query/header params, body.
- Applies auth for the operation's scheme from `secrets.json` (apiKey/bearer/basic/oauth2 per R5);
  OAuth2 fetches+caches a client-credentials token in memory.
- Honors `API2SKILL_INSECURE=1` / built-with-`--insecure` for untrusted TLS (dev-only).
- Prints the HTTP response body to stdout; non-2xx → prints status + body, exits non-zero.
- Uses only `System.Net.Http` + `System.Text.Json` (Constitution II).

### Runner per emitter (FR-006b)

| `--script` | File | Runner | Extra install |
|------------|------|--------|---------------|
| `cs` (default) | `call.cs` | `dotnet run scripts/call.cs -- …` | none (.NET 10 SDK) |
| `fsx` | `call.fsx` | `dotnet fsi scripts/call.fsx …` | none (.NET SDK) |
| `csx` | `call.csx` | `dotnet script scripts/call.csx -- …` | `dotnet tool install -g dotnet-script` |

## secrets.example.json contract

One entry per security scheme id (see data-model.md "Derived: Secrets schema"). Placeholder values
only. `.gitignore` MUST contain `secrets.json`. On `--force`, this template + `.gitignore` are
refreshed but an existing `secrets.json` is left untouched (FR-009 / NFR-1).

## Determinism (NFR-4)

For a fixed (spec, options) pair, every emitted file is byte-identical across runs — enabling
golden-file tests and meaningful `--force` diffs.
