# api2skill — usage

Detailed CLI reference for `api2skill generate`. See [README.md](../README.md) for a quick
overview and [specs/001-openapi-to-skill/](../specs/001-openapi-to-skill/) for the full
spec/design trail (`contracts/cli.md` and `contracts/skill-output.md` are the authoritative
contracts this document summarizes).

## Invocation

```
api2skill generate <spec-source> [options]
```

`<spec-source>` is resolved by shape:

| Form | Meaning |
|------|---------|
| a file path | read directly (`.json`/`.yaml`/`.yml`, or format auto-detected from content) |
| `http://` / `https://` URL | fetched with api2skill's own `HttpClient` (honors `--insecure`) |
| `-` | read to end from stdin (buffered — required for OpenAPI parsing of non-seekable input) |

## Options

| Option | Alias | Default | Behavior |
|--------|-------|---------|----------|
| `--out <dir>` | `-o` | `./<slug-of-title>` | Output directory for the generated skill |
| `--name <name>` | | slug of `info.title` | Override the skill name / output-dir slug |
| `--script <kind>` | | `cs` | Emitter: `cs` (`dotnet run`), `fsx` (`dotnet fsi`), `csx` (`dotnet script`, needs a global-tool install) |
| `--include <sel>` | | (all operations) | Keep only matching selectors. Repeatable (`--include a --include b`) or comma-joined (`--include a,b`) |
| `--exclude <sel>` | | (none) | Drop matching selectors, applied **after** `--include`. Same forms as above |
| `--force` | `-f` | off | Regenerate over an existing output directory. **Preserves** a real `secrets.json` if present |
| `--insecure` | | off | **Dev-only.** Accept self-signed/invalid TLS certificates — both when fetching a spec URL and, baked as the default, in the generated dispatcher (still overridable per-call via `API2SKILL_INSECURE`) |
| `--format <fmt>` | | sniffed | Force the input format (`json`/`yaml`) instead of detecting it |
| `--base-url <url>` | | from the spec's `servers` | Supply a base URL when the spec has none |

### `--include` / `--exclude` selector grammar

```
tag:<tagName>        # e.g. tag:Charges
op:<operationId>      # e.g. op:createCharge
path:<glob>            # e.g. path:/v1/charges*  ('*' matches any run of characters)
```

`--exclude` is applied after `--include`, so `--include tag:pet --exclude op:deletePet` keeps
everything tagged `pet` except that one operation. Filtering also recomputes which
security schemes appear in `SKILL.md`/`secrets.example.json` — a scheme only referenced by
excluded operations won't show up in the filtered output.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Skill generated (stderr may still carry non-fatal warnings — e.g. an unsupported auth scheme, or an empty/operation-less spec) |
| `1` | Invalid or unparseable spec, or an unsupported version. No output directory is created |
| `2` | Usage error — bad flags/selectors, or an unknown `--script` value |
| `3` | Output directory already exists and `--force` was not given |
| `4` | Couldn't acquire the spec — file not found, or a URL fetch failed (connection, DNS, or TLS — see `--insecure` above) |

Generation is all-or-nothing: on failure, no output directory is created (or, for `--force`
regenerating an existing skill, the **old** skill directory — including any real
`secrets.json` — is left completely untouched). Content is staged in a temporary directory and
only swapped into place once every file has been written successfully.

## What gets generated

```
<slug>/
├── SKILL.md               # compact: overview, auth setup, tag-grouped operation index
├── reference/
│   └── <tag>.md            # full parameter/schema/response detail per tag, on demand
├── scripts/
│   └── call.<ext>          # the dispatcher — cs / fsx / csx per --script
├── secrets.example.json    # template — copy to secrets.json (gitignored) and fill in
└── .gitignore               # excludes secrets.json
```

`SKILL.md` deliberately never carries per-parameter detail — that keeps it small even for APIs
with hundreds of operations. Claude (or you) follow the `reference/<tag>.md` link only when a
specific operation's details are needed.

### Calling the dispatcher

```
<runner> scripts/call.<ext> <operationId> [--<param> <value> ...] [--body <json|@file>]
```

| `--script` | Runner |
|------------|--------|
| `cs` (default) | `dotnet run scripts/call.cs --` |
| `fsx` | `dotnet fsi scripts/call.fsx --` |
| `csx` | `dotnet script scripts/call.csx --` (requires `dotnet tool install -g dotnet-script`) |

- Path parameters substitute into the URL; query and header parameters are added from
  `--<param>` flags matching the operation's parameter names.
- A request body is passed via `--body '<json>'` or `--body @path/to/file.json`.
- The base URL comes from the spec's `servers`, `--base-url` at generation time, or the
  `API2SKILL_BASE_URL` environment variable at call time (checked in that priority, env var
  wins if set).

### Authentication

The dispatcher reads real credentials from **`secrets.json`** — a file you create yourself
(usually by copying `secrets.example.json`) — **never** from the OpenAPI spec, and it is never
written by the generator. One entry per security scheme id:

| Scheme | `secrets.json` keys |
|--------|----------------------|
| API key | `apiKey` |
| Bearer token | `bearerToken` |
| HTTP Basic | `username`, `password` |
| OAuth2 (client-credentials) | `clientId`, `clientSecret`, `tokenUrl` (falls back to the spec's token URL if omitted), optional `scopes` |

For OAuth2, the dispatcher performs the client-credentials token exchange itself and caches
the token in memory for that one invocation — nothing is written to disk.

If a required credential is missing, the dispatcher prints a warning to stderr and sends the
request unauthenticated rather than failing outright — useful against endpoints that don't
actually enforce the scheme, and to see the real HTTP error you're trying to fix.

An operation whose security scheme isn't one of the four above (e.g. `openIdConnect`,
`mutualTLS`) still gets a normal dispatcher entry; `SKILL.md` and the reference doc flag it as
needing manual auth.

### Untrusted HTTPS (`--insecure`)

Off by default. When set, self-signed or otherwise invalid TLS certificates are accepted —
**for local/dev use only, never in production**. It applies in two places:

1. **At generation time**, when fetching a spec from an `https://` URL.
2. **In the generated dispatcher**, where `--insecure` at generation time bakes a default
   (overridable per call via the `API2SKILL_INSECURE=1` / `API2SKILL_INSECURE=0` environment
   variable) so a skill generated for a dev environment doesn't require re-exporting the env
   var on every call.

## Regenerating a skill (`--force`)

Re-running `generate` with the same `--out` fails with exit `3` unless you pass `--force`.
With `--force`:

- `SKILL.md`, `reference/`, the dispatcher, `secrets.example.json`, and `.gitignore` are all
  regenerated from the (possibly updated) spec.
- A real `secrets.json`, if present, is preserved byte-for-byte — it is read into memory
  before anything is touched and written back only after the new content has been fully
  staged, so a failure partway through never loses it.
