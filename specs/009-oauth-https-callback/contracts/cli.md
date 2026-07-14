# Contract: CLI — OAuth capture / login / protocol

Additive root commands for `api2skill` (alongside `generate`, `update`, `install-creator`).

## Commands

### `api2skill oauth-capture`

Thin redirect capture. Does **not** perform token exchange.

| Option | Arg | Required | Description |
|--------|-----|----------|-------------|
| `--callback-url` | `<url>` | yes* | Redirect URI registered at IdP (*or from profile when invoked via `login`) |
| `--mode` | `auto\|http\|https\|scheme\|hosted` | no | Default `auto` (infer) |
| `--timeout` | `<seconds>` | no | Default `180` |
| `--cert` | `<path.pfx>` | conditional | Required for HTTPS loopback unless PEM pair set |
| `--cert-password` | `<pwd>` | no | Prompt (colored) if needed and TTY |
| `--cert-pem` | `<path>` | conditional | With `--cert-key` |
| `--cert-key` | `<path>` | conditional | With `--cert-pem` |
| `--relay-base` | `<url>` | no | Override hosted relay base |
| `--state` | `<string>` | no | Expected state; mismatch → error |
| `--json` | flag | no | Force JSON stdout (default for scripting) |

**Stdout**: single JSON [CaptureResult](./oauth-capture.md).  
**Stderr**: human progress + colored warnings.

### `api2skill login`

End-to-end interactive login for a generated skill directory.

| Option | Arg | Required | Description |
|--------|-----|----------|-------------|
| `--skill` | `<dir>` | yes | Skill root (contains `auth.json`) |
| `--profile` | `<name>` | no | OAuth2 authorization_code profile |
| (same cert/timeout/relay flags as `oauth-capture`) | | | Forwarded to capture |

Writes/updates `<skill>/.auth-cache.json` using the same cache schema as generated dispatchers
(spec 002).

### `api2skill register-protocol`

| Option | Arg | Description |
|--------|-----|-------------|
| `--scheme` | `<name>` | Default `api2skill` |
| `--force` | flag | Overwrite existing registration |

### `api2skill unregister-protocol`

| Option | Arg | Description |
|--------|-----|-------------|
| `--scheme` | `<name>` | Default `api2skill` |

## Exit codes

| Code | When |
|------|------|
| `0` | Success |
| `2` | Usage / validation (missing cert on non-TTY, bad URL, unknown mode) |
| `4` | Skill/auth/secrets file acquisition failure (`login`) |
| `6` | Capture timeout / hosted unreachable / protocol not registered |
| `7` | OAuth error returned on redirect (`error=` query) |

(Existing generate codes 0–5 unchanged.)

## Colored output

Warnings and trust/cert/protocol prompts use **bright yellow**; failures **bright red**; success
ack for register **green**. Disabled when `NO_COLOR` is set or stdout is redirected (JSON path
still valid).
