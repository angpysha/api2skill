# Feature Specification: OAuth tokenField selection

**Feature Branch**: `debug/oauth-callback-entra` (no new branch)

**Created**: 2026-07-14

**Status**: Approved (grilled)

**Input**: Allow oauth2 profiles to select which token-response JSON field becomes the Bearer credential (`access_token` default or `id_token`), for B2C / transitional APIs that currently expect an ID token.

## Clarifications (grill)

| Topic | Decision |
|-------|----------|
| JSON field name | `tokenField` |
| Allowed values | Exact keys only: `access_token`, `id_token` (no aliases) |
| Default | `access_token` when omitted |
| Scope | All oauth2 token POSTs: authorization_code exchange, refresh_token, client_credentials |
| Cache `access_token` key | Always stores the **selected** bearer value under `.auth-cache.json` → `access_token` |
| Cache `id_token` key | Also store IdP `id_token` when present in the token response |
| Missing selected field | Fall back to the other of `access_token`/`id_token` if present + **warning** (do not hard-fail) |
| Expiry | Use response `expires_in` when present, else 3600. **No JWT `exp` parsing in v1**; note follow-up if id_token expiry bugs appear |
| Validation | Reject unknown `tokenField`; reject `tokenField` on non-oauth2 profiles |

## User Scenarios & Testing

### User Story 1 - Prefer id_token as Bearer (Priority: P1)

A user sets `"tokenField": "id_token"` on an oauth2 profile (e.g. Azure AD B2C transitional API). After login/token exchange, API calls send `Authorization: Bearer <id_token value>`. The cache stores that value under `access_token` and also persists raw `id_token` when returned.

**Independent Test**: Stub token endpoint returns both tokens; profile uses `tokenField: id_token`; login completes; `.auth-cache.json` has selected value in `access_token` and `id_token` present; subsequent call sends that bearer.

### User Story 2 - Default unchanged (Priority: P1)

Profiles omitting `tokenField` behave exactly as today (use `access_token`).

**Independent Test**: Existing oauth login/client-credentials tests stay green without `tokenField`.

### User Story 3 - Soft fallback with warning (Priority: P2)

If configured field is missing but the other token is present, warn and use the other.

**Independent Test**: `tokenField: id_token` but response only has `access_token` → warning on stderr; login still succeeds; cache uses access_token value.

### Edge Cases

- Neither token present → fail exchange (same as today when access_token missing).
- Refresh / client_credentials honor `tokenField` the same way.
- Serialize omits `tokenField` when default `access_token`.

## Requirements

- **FR-001**: oauth2 profiles MUST support optional `tokenField` with values `access_token` \| `id_token`.
- **FR-002**: Default MUST be `access_token`.
- **FR-003**: Emitters (cs/csx/fsx) MUST read the selected field (with fallback+warning) for every token POST response parse path.
- **FR-004**: Token cache MUST store selected bearer under `access_token` and optional `id_token` sibling when present.
- **FR-005**: Generation validation MUST reject invalid values / wrong profile type (exit 5 pattern).
- **FR-006**: Docs MUST document `tokenField`, fallback, cache shape, and “no JWT exp in v1 / future note”.

## Success Criteria

- SC-001: User can set `tokenField: id_token` and complete Entra/B2C-style login using id_token as Bearer.
- SC-002: Omission of `tokenField` does not change existing behavior.
- SC-003: Missing selected field with other present warns and continues.
