# Phase 0 Research: Explicit Auth Configuration

**Feature**: `002-explicit-auth-config` · **Spec**: [spec.md](./spec.md)

This resolves the technical unknowns behind the spec's resolved decisions before design. Each
entry: **Decision → Rationale → Alternatives considered**.

---

## R1 — Where auth structure lives: runtime-read `auth.json` vs. baked codegen

**Decision**: The emitted dispatcher carries a **fixed, API-independent auth engine** and reads
the **committed `auth.json` at runtime** for profile definitions. Per-API codegen adds only, per
operation, the ordered list of **applicable profile names** (resolved from attachments at
generation). The generator still fully parses and validates `auth.json` at generation time (for
collision/unknown-type/unused-tag checks and to scaffold `secrets.example.json`), then copies it
verbatim into the skill directory.

**Rationale**:
- The auth logic (bearer prefix, basic, custom headers, script exec, OAuth authorize/token/
  refresh, PKCE, `state`, token cache, file locking) is large and identical across every
  generated skill. Baking it per-API and per-language would triple an already-large body of
  string-emitted code and invite divergence. Making the engine API-independent means it is
  **written and tested once per language** (see [R8](#r8)).
- Reading `auth.json` at runtime lets a user tweak non-secret structure (add a scope, change a
  callback port, add a token-request header) **without regenerating** — matching the "auth.json
  is a committed, editable artifact" decision in the spec.
- Generation stays **deterministic/byte-stable** (NFR-4): `auth.json` is copied verbatim and the
  per-operation profile-name lists are computed deterministically; PKCE/`state` are runtime-only.

**Alternatives considered**:
- *Bake all resolved auth config into each dispatcher* — rejected: per-API × per-language code
  explosion, harder to keep the three emitters in parity, no edit-without-regen.
- *Ship a shared compiled auth library the dispatcher references* — rejected: violates
  Constitution I (no compiled client / build step at call time) and II (zero third-party dep).

---

## R2 — Browser + loopback callback + PKCE + `state` in a zero-dependency .NET script

**Decision**: Use **`System.Net.HttpListener`** bound to the configured loopback callback (default
`http://localhost:8400/callback`) to receive the redirect; launch the browser with
`Process.Start(new ProcessStartInfo(url){ UseShellExecute = true })`, with per-OS fallbacks
(`open` on macOS, `xdg-open` on Linux) and, if none succeeds, **print the URL** for manual paste
(headless path, FR US3-6). PKCE uses a cryptographically random `code_verifier` (43–128 chars from
the unreserved set) and `code_challenge = base64url(SHA256(verifier))` with `method=S256`. `state`
is a random URL-safe token generated with `RandomNumberGenerator`, sent on the authorize request
and compared on the callback (FR-016a); mismatch ⇒ reject, store nothing.

**Rationale**: `HttpListener`, `Process`, `System.Security.Cryptography`, and
`RandomNumberGenerator` are all in the base class library — no third-party dependency
(Constitution II). This is the same loopback-redirect pattern RFC 8252 recommends for native apps
and that Entra permits for public clients on `http://localhost` with any port.

**Alternatives considered**:
- *Out-of-band / device-code flow* — rejected for this feature (device code is a later preset
  concern; loopback is simpler and matches Postman's desktop flow).
- *Embedded web view* — rejected: pulls a UI dependency, breaks zero-dep and headless use.

---

## R3 — Token cache format, location, permissions, and concurrency

**Decision**: A git-ignored `.auth-cache.json` next to `secrets.json`, a JSON object keyed by
profile name, each value `{ access_token, expires_at (UTC ISO-8601), refresh_token? }`. On POSIX,
set mode `0600` via `File.SetUnixFileMode`; on Windows, rely on the user-profile ACL (best-effort,
documented). Concurrency (FR-019a) is guarded by an **inter-process lock**: open a sibling
`.auth-cache.json.lock` with `FileShare.None` in a short bounded retry loop; the full
read-modify-write (including a **post-lock re-check** of validity) happens under the lock, so a
process reuses a token another just refreshed and a rotated refresh token is never clobbered.

**Rationale**: `FileShare.None` gives a portable advisory lock without extra dependencies; a
separate `.lock` file keeps the JSON file itself free of partial writes (write to a temp file then
atomic `File.Move`). Storing `expires_at` (absolute) rather than `expires_in` avoids clock drift
between processes. `File.SetUnixFileMode` is BCL (.NET 7+), no P/Invoke.

**Alternatives considered**:
- *No locking / last-writer-wins* — rejected in clarification: concurrent refresh wastes token
  calls and can invalidate a rotated refresh token (real with Entra).
- *OS keychain storage* — rejected: platform-specific, dependency-heavy, and overkill for a local
  dev artifact whose `secrets.json` is already plaintext-on-disk.

---

## R4 — OAuth grants, client authentication, and Postman-parity request customization

**Decision**: Support **`authorization_code` + PKCE** (client secret optional → public client) and
retain **`client_credentials`**. `auth.json` per-oauth-profile fields: `grant`, `authUrl`,
`tokenUrl`, `scopes[]`, `callbackUrl`, `clientAuth` (`body` [default] | `basic`), and
`authorizeRequest`/`tokenRequest` objects each with `headers{}` and `body{}` for extra
parameters (Postman parity). `clientId`/`clientSecret` are secret references. At the token
endpoint, `clientAuth=body` sends `client_id`/`client_secret` as form fields; `basic` sends an
`Authorization: Basic base64(client_id:client_secret)` header and omits them from the body.

**Rationale**: This mirrors Postman's OAuth 2.0 editor (Auth URL, Access Token URL, Client
ID/Secret, Scope, Client Authentication, plus advanced custom headers/body on the token request),
which is the parity target the user asked for. `client_credentials` is already implemented today
and is the correct unattended path — kept, not replaced.

**Alternatives considered**:
- *Implicit grant* — rejected: deprecated by OAuth 2.1; PKCE supersedes it.
- *Only `authorization_code`* — rejected: drops working machine-to-machine support.

---

## R5 — `entra` preset

**Decision**: `type: "oauth2"`, `preset: "entra"`, `tenant: "<id|domain|common|organizations>"`
expands to `authUrl = https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize`,
`tokenUrl = https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token`, and ensures
`offline_access` is present in `scopes` (so a refresh token is returned). Any explicit
`authUrl`/`tokenUrl`/`scopes` in the profile override the preset expansion. `clientSecret` remains
optional (public client). This is the only preset shipped; other providers use a fully-manual
`oauth2` profile.

**Rationale**: Entra's v2.0 endpoints are tenant-templated and stable; `offline_access` is the
documented way to obtain refresh tokens; `http://localhost` loopback redirects are allowed for
public-client app registrations. A tiny preset removes the most error-prone hand configuration
while leaving full manual control.

**Alternatives considered**: Shipping Google/Auth0/Okta presets now — deferred per spec scope
(they are pure additive data once the generic `oauth2` engine exists).

---

## R6 — Config surface: `--auth`, `--auth-config`, `--login`, and validation ordering

**Decision**:
- `--auth-config <file>`: path to an `auth.json`; parsed and validated, then copied into the skill
  dir.
- `--auth <type>`: shorthand that **scaffolds a single global profile** for the structure-free
  types **only** (`bearer` | `basic` | `custom`); `oauth2`/`entra` via `--auth` is a **usage
  error** pointing to `--auth-config` (clarification). `--auth` and `--auth-config` are mutually
  exclusive.
- `--login`: after a successful write, run the interactive login once for each authorization-code
  profile to prime `.auth-cache.json`; without it, generation is non-interactive.
- New exit code **`5` (AuthConfigError)** for malformed `auth.json`, unknown profile `type`, or a
  header collision (FR-021a). Missing `auth.json` file reuses `4` (AcquisitionFailure) semantics
  or a dedicated message — decided in [contracts/cli.md](./contracts/cli.md).

**Rationale**: Keeps the existing option/exit-code contract additive; validation failures never
write partial output (reuses `SkillWriter`'s staging-then-move guarantee).

**Alternatives considered**: Folding auth errors into the generic usage error `2` — rejected: a
distinct code makes CI/automation able to distinguish "your auth.json is wrong" from "bad CLI
usage."

---

## R7 — Secret references and resolution

**Decision**: `auth.json` values that are secrets use the placeholder form `{secret:NAME}`;
literal non-secret strings are allowed elsewhere. Resolution happens **at call time** in the
dispatcher against `secrets.json`. A referenced key missing at call time ⇒ the call fails naming
the key (never sends an empty credential). The generator collects every `{secret:NAME}` across
profiles and scaffolds those keys into `secrets.example.json` (Constitution IV — placeholders
only, never real values).

**Rationale**: Separates committed structure from git-ignored secrets exactly as the spec
requires; a single, greppable placeholder syntax keeps parsing trivial and AOT/trim-safe.

**Alternatives considered**: Env-var interpolation (`${ENV}`) — deferred; `secrets.json` is the
established store in this codebase and keeps one resolution path.

---

## R8 — Multi-emitter strategy (cs / fsx / csx parity)

**Decision**: The auth engine is emitted as a **fixed per-language block** (one C# block shared in
spirit by the `.cs` and `.csx` emitters since both are C#, and one F# translation for `.fsx`),
parameterized only by nothing API-specific. Behavioral parity across emitters is enforced by
**integration tests** that run each generated dispatcher against a stub IdP + stub API and assert
identical auth behavior (extends the existing `Integration/Dispatcher*Tests`), plus golden-file
tests for the emitted text.

**Rationale**: Because the engine is API-independent (R1), it is authored once per language and
changes rarely; the risk is cross-language drift, which behavioral integration tests catch
directly. `.cs` and `.csx` share the same C# source text, halving the real surface to two
implementations.

**Alternatives considered**:
- *Single shared auth source file `#load`-ed by all three* — rejected: `.fsx` cannot load C#, and
  .NET file-based `.cs` apps do not portably `#load` sibling files the way `.csx` does.
- *Drop fsx/csx auth parity* — rejected: Constitution III / FR-023 require all emitters behave
  equivalently.

---

## R9 — AOT / trim posture

**Decision**: The generator currently ships as a **dotnet tool (JIT)**, not Native-AOT
(`Api2Skill.csproj` has no `PublishAot`). Keep `auth.json` (de)serialization **source-generator-
friendly** — use `System.Text.Json` with a `JsonSerializerContext` for the auth config types — so
AOT stays a viable future switch and the `aot-safety` rule is honored preemptively. The **generated
dispatcher** runs under `dotnet run` (JIT), so its `HttpListener`/`Process`/reflection-free JSON
usage has no AOT constraint.

**Rationale**: Cheap insurance: source-gen JSON is good practice regardless, and avoids a later
retrofit if the tool adopts AOT. No behavior cost.

**Alternatives considered**: Reflection-based `JsonSerializer` for the config — works today but
would trip IL2xxx/IL3xxx if AOT is later enabled; avoided.

---

## Open questions remaining

None blocking. All spec `[NEEDS CLARIFICATION]` were resolved in the clarify session; remaining
choices (exact exit-code for missing file, Windows ACL best-effort wording) are contract-level and
settled in [contracts/](./contracts/).
