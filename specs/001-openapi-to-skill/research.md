# Phase 0 Research — OpenAPI → Claude Skill generator

Resolves the Technical Context unknowns and the REQ-0001 deferred open questions (OQ-1..5).
Format per decision: **Decision / Rationale / Alternatives**.

## R1 — CLI parsing framework

**Decision**: **System.CommandLine 2.0.x** (GA Nov 2025, latest 2.0.9).

**Rationale**: First-party, now stable (no longer beta), targets .NET 8+. Cleanly models our flag
set (`-o`, `--name`, `--script`, `--include`/`--exclude`, `--force`, `--insecure`) with subcommand
room to grow. Uses the GA API (`SetAction`, `SynchronousCommandLineAction`) — not the old beta4
surface.

**Alternatives**: *Spectre.Console.Cli* — nicer help rendering but third-party; *Cocona* —
convention-based, third-party; *manual `args` parsing* — no dependency but reinvents help/validation.
Rejected in favour of the first-party GA option.

## R2 — OpenAPI parsing

**Decision**: **Microsoft.OpenApi 3.8.0**, read via `OpenApiDocument.LoadAsync(stream, format,
settings, ct)` → `ReadResult` (document + `OpenApiDiagnostic`). One normalized model for 2.0/3.0/
3.1 (3.2 best-effort).

**Rationale**: Single object model across versions on `System.Text.Json`; diagnostics give us the
actionable parse errors FR-010 requires.

**Alternatives**: *NSwag* / *Kiota* parsers — heavier, aimed at client codegen; *hand-rolled* —
untenable across spec versions. The legacy `Microsoft.OpenApi.Readers` 1.6.x package is superseded
by the integrated 3.x readers (`OpenApiModelFactory`/`LoadAsync`).

**Constraint captured**: `LoadAsync` on a **non-seekable JSON stream without an explicit `format`
fails** ([OpenAPI.NET#2638](https://github.com/Microsoft/OpenAPI.NET/issues/2638)). stdin is
non-seekable → we always buffer input to a `MemoryStream` and detect/pass the format (see R3).

## R3 — Input acquisition & untrusted-HTTPS ownership

**Decision**: api2skill **owns input acquisition** rather than delegating to `LoadAsync`'s URL
loader. For each source: **file** → open stream; **URL** → fetch bytes with *our* `HttpClient`;
**stdin** → read to end. All three are buffered into a `MemoryStream`, format is sniffed
(JSON `{`/`[` vs YAML) or taken from an extension/`--format`, then handed to `LoadAsync`.

**Rationale**: Owning the fetch is what lets the **untrusted-HTTPS opt-in (FR-007)** apply to spec
fetching (self-signed dev hosts, EC-8) — we set `HttpClientHandler.ServerCertificateCustomValidation
Callback = DangerousAcceptAnyServerCertificateValidator` only when opted in. Buffering also fixes the
non-seekable-stdin issue (R2).

**Alternatives**: `LoadAsync(url)` direct — can't inject our TLS policy; rejected.

## R4 — Generator architecture (pluggable emitter)

**Decision**: Three-stage pipeline **Parse → SkillModel (intermediate) → Emit**, with an
`IScriptEmitter` abstraction. `SkillModel` is emitter-agnostic (operations, params, security,
servers, tags). Concrete emitters: `CsFileEmitter` (default), `FsxEmitter`, `CsxEmitter`; each
consumes `SkillModel` and writes its dispatcher script. `SKILL.md`/`reference/`/secrets-template/
`.gitignore` writers are shared and emitter-independent.

**Rationale**: Directly satisfies Constitution III / FR-006 — a new emitter (bash, Python, compiled
client) is a new `IScriptEmitter` with zero changes to parse or model. Validated by shipping three.

**Alternatives**: per-format branches inside one method — violates the extensibility principle;
rejected.

## R5 — Generated auth (all four schemes) & OAuth2 client-credentials

**Decision**: The dispatcher resolves the operation's required security scheme from `SkillModel`
and applies it: `apiKey` → header/query; bearer → `Authorization: Bearer`; basic →
`Authorization: Basic base64`. **OAuth2** → dispatcher POSTs `grant_type=client_credentials`
(form-urlencoded) with `client_id`/`client_secret`(+scopes) to the spec's token URL, reads the
`access_token`, caches it **in memory for the process run**, applies as bearer.

**Rationale**: Covers the four schemes (FR-003a) with only `HttpClient`/`System.Text.Json`
(Constitution II). In-memory token honours the "no token written to disk" safety note (D4).

**Alternatives**: full OAuth2 authorization-code/PKCE — deferred (needs a browser/redirect);
out of MVP scope.

## R6 — Testing strategy

**Decision**: **xUnit** (matches manifest `dotnet-unit-tests`). Layers: (a) **unit** tests for
parse→model mapping and per-scheme auth code generation; (b) **golden/snapshot** tests per emitter —
generate a skill from a checked-in fixture spec (Swagger Petstore + a small multi-auth spec) and
diff against an approved tree (supports NFR-4 determinism); (c) **integration** smoke — run each
emitted dispatcher with its runner against a stub HTTP server for one op per auth scheme (AC-2).

**Rationale**: Golden tests are the natural fit for a code generator and enforce byte-stable output.

**Alternatives**: assertion-only unit tests — miss whole-file regressions; rejected as sole method.

## R7 — Target framework

**Decision**: Generator targets **.NET 10** (`net10.0`). The `.cs` emitter produces **.NET 10
file-based apps** (`dotnet run app.cs`, `#:package`); `.fsx` uses `dotnet fsi`; `.csx` uses the
`dotnet-script` global tool.

**Rationale**: Matches the environment SDK and D3; file-based apps give the default emitter an
SDK-native runner with no extra install.

## Resolved REQ open questions

- **OQ-1 (secrets file)** → **`secrets.json`** (JSON via `System.Text.Json`); committed template
  is **`secrets.example.json`**; `.gitignore` excludes `secrets.json`.
- **OQ-2 (reference granularity)** → **per-tag** `reference/<tag>.md`. A soft size threshold to split
  a very large single tag is deferred (non-blocking; per-tag is the MVP rule).
- **OQ-3 (CLI flags)** → concrete surface defined in `contracts/cli.md`.
- **OQ-4 (empty/operation-less spec)** → generate a **valid minimal skill** (overview + "no callable
  operations" note), exit 0 with a warning (not an error — the spec parsed fine).
- **OQ-5 (multi-scheme credential scoping)** → **per-skill `secrets.json`** with a named entry per
  scheme id; the dispatcher selects the entry for the scheme the operation requires. No per-operation
  secret files.
