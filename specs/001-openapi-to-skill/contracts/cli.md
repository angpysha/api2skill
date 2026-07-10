# Contract — CLI surface

The console app's external contract (System.CommandLine 2.0). Resolves OQ-3.

## Invocation

```
api2skill generate <spec-source> [options]
```

`generate` is the MVP verb (room for more later). `<spec-source>` positional resolves by shape:

| Source | Form | Notes |
|--------|------|-------|
| Local file | path ending `.json`/`.yaml`/`.yml`, or an existing file | opened directly |
| Remote URL | `http://` / `https://` | fetched with api2skill's HttpClient (honors `--insecure`) |
| stdin | `-` (dash) | read to end; format from `--format` or sniffed |

## Options

| Option | Alias | Type | Default | Maps | Behavior |
|--------|-------|------|---------|------|----------|
| `--out <dir>` | `-o` | dir | `./<slug>` | FR-008 | Output directory; skill dir created here |
| `--name <name>` | | string | slug of `info.title` | FR-008 | Override skill name/dir |
| `--script <kind>` | | enum `cs\|fsx\|csx` | `cs` | FR-006a/D3 | Emitter selection |
| `--include <sel>` | | multi | (all) | FR-004b | Keep only matching `tag:`/`path:`/`op:` selectors |
| `--exclude <sel>` | | multi | (none) | FR-004b | Drop matching selectors (applied after include) |
| `--force` | `-f` | flag | false | FR-009 | Regenerate over existing dir; **preserve** real `secrets.json` |
| `--insecure` | | flag | false | FR-007 | Accept untrusted HTTPS for spec fetch AND in generated dispatcher (dev-only); also via `API2SKILL_INSECURE=1` |
| `--format <fmt>` | | enum `json\|yaml` | sniffed | R2/R3 | Force input format (needed for some stdin cases) |
| `--base-url <url>` | | string | from `servers` | EC-7 | Supply base URL when the spec omits `servers` |

Selector grammar for `--include`/`--exclude`: `tag:<name>` | `path:<glob>` | `op:<operationId>`
(repeatable; comma-separated allowed).

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Skill generated (may include non-fatal warnings, e.g. unsupported scheme EC-6, empty spec OQ-4) |
| 1 | Invalid/unparseable spec or unsupported version — no partial output (FR-010, EC-1/EC-2) |
| 2 | Usage error (bad flags/selectors) |
| 3 | Output dir exists and `--force` not given (EC-10) |
| 4 | Input acquisition failure (file not found, HTTP/TLS error without `--insecure`) (EC-8) |

## Output streams

- **stdout**: the path of the generated skill dir + a short summary (op count, emitter, auth schemes).
- **stderr**: warnings and errors (actionable messages). No secrets ever printed.

## Examples

```bash
api2skill generate ./petstore.json                       # -> ./swagger-petstore/ (.cs emitter)
api2skill generate https://svc.local/swagger.json --insecure -o ./out --name pets
curl -s https://api.example.com/openapi.yaml | api2skill generate - --format yaml
api2skill generate ./stripe.json --include tag:Charges --script fsx
api2skill generate ./petstore.json --force               # regenerate; keep secrets.json
```
