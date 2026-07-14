# Research: HTTPS OAuth callback listener

**Feature**: `009-oauth-https-callback`  
**Date**: 2026-07-14

## Current gap

Emitters already set HttpListener prefixes from `callbackUrl.Scheme`. Using `https://localhost:8400/...` therefore *attempts* HTTPS prefixes, but without certificate binding the listen/accept path fails or browsers reject TLS. HTTP path is proven on macOS (Entra debug session).

## Options (to choose in grill / plan)

| Option | Pros | Cons |
|--------|------|------|
| **A. Keep HttpListener; document OS SSL bind** | Minimal emitter rewrite | Windows-centric (`netsh`); weak/fragile on macOS/Linux |
| **B. HTTPS via Kestrel in generated script** | Cross-platform; `dotnet dev-certs` / explicit cert config well documented | Heavier generated code; dependency on ASP.NET Core shared framework or package |
| **C. Minimal `TcpListener` + `SslStream` + tiny HTTP parse** | Controlled TLS; no full ASP.NET | More custom code in emitters; security review of parser |
| **D. External helper process** | Isolates TLS | Extra binary/ship complexity |

**Lean recommendation for plan phase after grill:** prefer **B** or **C** for real cross-platform HTTPS; treat **A** as Windows-only fallback if needed.

## Cert sources

- `dotnet dev-certs https` / `--trust` (ASP.NET Core developer cert; localhost-oriented)
- File-based PEM/PFX path via `auth.json` (e.g. `callbackCertificatePath` + optional password secret)
- mkcert for trusted local CA (docs-heavy, good UX after install)

## Doc references

- GitHub Copilot / agent skills paths are unrelated; this is TLS for OAuth redirect only.
- ASP.NET Core HTTPS developer certificates and Kestrel certificate configuration (Microsoft Learn / Community practice for macOS Keychain trust).

## Decision log

_(fill during grill / `/speckit.plan`)_

| Decision | Choice | Date |
|----------|--------|------|
| | | |
