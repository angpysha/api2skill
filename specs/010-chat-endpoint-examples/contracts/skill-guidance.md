# Contract: Skill guidance for examples

Emitted into `SKILL.md` (and echoed briefly under each op’s examples table caption in reference MD).

## Prefer authored examples

When calling or testing an operation:

1. Look under `examples/<operationId>/` for named examples.
2. If one name → use its `request.json` as `--body` (or equivalent).
3. If several → ask the user which `name` to use (or pick `default` if present and user did not specify).
4. Do **not** invent a JSON body when an authored request example exists, unless the user explicitly overrides.

## Failure protocol (mandatory)

If a call that used an authored example fails (non-success HTTP, auth error, unexpected body):

1. **Ask** the user whether the example should be updated.
2. Optionally **propose** a concrete patch to `request.json` / `response.json` and/or a contract (OpenAPI / auth) change.
3. **Do not apply** any write to examples or contracts until the user **explicitly approves**.
4. Never “fix forward” by silently overwriting examples.

## Authorship via chat

To add an example without CLI:

1. Create `examples/<operationId>/<name>/request.json` and/or `response.json`.
2. Run `api2skill example sync --skill .` **or** ensure `reference/<tag>.md` links the files (CLI preferred).
3. Keep payloads secret-free.
