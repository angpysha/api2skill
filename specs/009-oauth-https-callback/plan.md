# Implementation Plan: App-owned OAuth redirect capture (multi-mode)

**Branch**: `feature/009-oauth-https-callback` | **Date**: 2026-07-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/009-oauth-https-callback/spec.md`

## Summary

Move OAuth **redirect capture** into the **api2skill CLI** so login supports **HTTP**, **HTTPS**
(local loopback + cert param), **custom URL schemes** (explicit `register-protocol`), and a
**hosted HTTPS relay** (Postman-style) for non-loopback callbacks. Expose `api2skill oauth-capture`
(thin) and `api2skill login --skill` (end-to-end); generated skill `login` shells out to capture.
Token exchange and `.auth-cache.json` remain skill-directory scoped. Colored prompts for cert
trust and protocol registration. See [research.md](./research.md).

## Technical Context

**Language/Version**: C# on **.NET 10** (`net10.0`); generated `.cs` / `.fsx` / `.csx` still emit
login helpers but prefer Process-invoke of the tool for capture.

**Primary Dependencies**: Existing `System.CommandLine`, `Microsoft.OpenApi`. **App-only** additions
allowed for local HTTPS (e.g. Kestrel / ASP.NET Core minimal hosting packages) — **not** emitted into
skills. Hosted relay under `hosting/oauth-relay/` (Cloudflare Worker **or** Azure Functions — pick one
in implement; worker preferred for zero ASP.NET ops cost).

**Storage**: Filesystem — skill `.auth-cache.json` unchanged. Relay: ephemeral in-memory/KV session
store (code + error + expiry only). Optional local protocol-registration state is OS-owned (registry /
LaunchServices / `.desktop`).

**Testing**: xUnit — unit (mode routing, cert flag validation, CaptureResult JSON), CLI tests for
new commands (TTY/non-TTY cert fail paths), integration with stub IdP + stub relay (loopback HTTP/
HTTPS with test cert; hosted poll against in-process stub). Protocol registration tests are
OS-conditional / best-effort.

**Target Platform**: Cross-platform .NET tool (macOS/Linux/Windows). Interactive browser or
`browserLaunch: clipboard` unchanged.

**Project Type**: CLI tool (`src/Api2Skill`) + generated skill emitters + optional `hosting/oauth-relay`.

**Performance Goals**: Capture timeout default **180s** (configurable); hosted poll interval ~1s;
no change to generation throughput.

**Constraints**: Constitution I–V; secrets never in relay logs; generated scripts remain BCL-only
(Constitution II) — they **call** the tool rather than embedding HTTPS/TLS; colored UX without
Spectre; no silent protocol registration.

**Scale/Scope**: 3 new root commands (`oauth-capture`, `login`, `register-protocol` +
`unregister-protocol`), capture engine module in app, emitter login path change (3 emitters),
hosted relay package, wiki Authentication updates, version **0.6.0** (minor).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Plan compliance | Status |
|-----------|-----------------|--------|
| I. Scripts, not compiled clients | Skills still call via scripts; capture moves to installed tool already required to generate the skill | ✅ |
| II. .NET-native, zero unnecessary deps **in emitted code** | Emitters do not add HTTP libraries; app may add Kestrel/hosting packages | ✅ |
| III. Pluggable emitters | Capture API is CLI/Process contract; each emitter shells out the same way | ✅ |
| IV. Secrets never committed | Cert paths/passwords only via CLI/prompt; relay stores code not tokens; no secrets in templates | ✅ |
| V. Progressive disclosure | SKILL.md gains short pointers to `login --skill` / register-protocol / hosted URL | ✅ |
| Untrusted HTTPS opt-in | Local HTTPS uses **user-supplied** cert (explicit); does not weaken `--insecure` semantics for API calls | ✅ |
| Test-first default | Tasks require failing tests for mode routing / CLI before impl | ✅ |

**Result: PASS — no violations.** App-side HTTPS hosting packages are justified under Complexity
Tracking (not a principle break).

### Constitution Check (post–Phase 1)

Contracts keep generated dispatchers BCL-only; hosted relay is out-of-band infra; CLI contracts do
not embed credentials. **PASS unchanged.**

## Project Structure

### Documentation (this feature)

```text
specs/009-oauth-https-callback/
├── spec.md
├── plan.md              # This file
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli.md
│   ├── oauth-capture.md
│   ├── hosted-relay.md
│   └── dispatcher-login.md
└── tasks.md             # /speckit-tasks (not this command)
```

### Source Code (repository root)

```text
src/Api2Skill/
├── Program.cs                    # + login, oauth-capture, register-protocol, unregister-protocol
├── Cli/
│   ├── OAuthCaptureCommand.cs    # NEW
│   ├── LoginCommand.cs           # NEW
│   ├── RegisterProtocolCommand.cs# NEW (+ unregister)
│   └── ConsoleColorWriter.cs     # NEW — colored warning/error helpers
├── OAuth/                        # NEW — app-owned capture
│   ├── CaptureMode.cs
│   ├── CaptureOptions.cs
│   ├── CaptureResult.cs
│   ├── IRedirectCapture.cs
│   ├── LoopbackHttpCapture.cs
│   ├── LoopbackHttpsCapture.cs
│   ├── CustomSchemeCapture.cs
│   ├── HostedRelayCapture.cs
│   ├── CertMaterial.cs           # PFX / PEM load + prompt
│   └── ProtocolRegistration.cs  # OS register/unregister
├── Emit/
│   ├── CsFileEmitter.cs          # login shells to oauth-capture
│   ├── CsxEmitter.cs
│   └── FsxEmitter.cs
hosting/oauth-relay/              # NEW — deployable relay (Worker or Azure Function)
wiki/Authentication.md            # update OAuth / callback modes
tests/Api2Skill.Tests/
├── Cli/…                         # new command tests
└── OAuth/…                       # capture unit + stub relay
```

**Structure Decision**: Single CLI project plus a separate `hosting/oauth-relay` folder (not packed
into the tool). Capture engine is a new `OAuth/` namespace in the tool; emitters only gain a thin
Process handoff (Dedupe: extend emitters; new modules for capture — **new** for `OAuth/*`,
**extend** `Program.cs` / emitters).

## Complexity Tracking

| Violation / tension | Why Needed | Simpler Alternative Rejected Because |
|---------------------|------------|-------------------------------------|
| App may reference ASP.NET Core / Kestrel for local HTTPS | Reliable HTTPS listen cross-platform | Pure `HttpListener` HTTPS is fragile / OS-specific and blocked skill emission under Const. II |
| Hosted relay infra in-repo | Grill B requires v1 hosted URL | Document-only “bring your own HTTPS” fails Postman-like UX |
| Dual path: tool capture + temporary in-script HTTP fallback | Soft migration | Hard-break login for users without upgraded tool mid-skill-lifetime |

## Phase 0 / 1 outputs

| Artifact | Path |
|----------|------|
| Research | [research.md](./research.md) |
| Data model | [data-model.md](./data-model.md) |
| Contracts | [contracts/](./contracts/) |
| Quickstart | [quickstart.md](./quickstart.md) |

**Agent context update script**: not present under `.specify/scripts/` — skipped.

## Next command

`/speckit-tasks` (then `/speckit-analyze` before implement gate).
