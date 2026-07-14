# Roadmap

Future directions for api2skill. These are **docs-only intentions** — not scheduled
commits and not a promise of ship dates. In-flight work lives in feature branches and
`specs/`.

## Near-term

Current product focus remains OpenAPI → Claude Skill generation (auth, update, creator
skill, examples). Track concrete work under `specs/` and open PRs.

## Medium-term

Additional **API kinds** / ingest formats and emitters beyond OpenAPI REST:

- **OData** — consume OData metadata / service definitions; emit skills for OData endpoints
- **GraphQL** — ingest GraphQL schemas; emit skills that call GraphQL APIs
- **gRPC** — ingest Protocol Buffers / gRPC service definitions; emit skills for RPC-style APIs

## Long-term

- **Native AOT for the CLI** — AOT-publish `api2skill` as a native binary so installing and
  running the **tool** can reduce or eliminate the need for a full .NET SDK/runtime on the
  machine where possible.

  **Honest framing:** Native AOT targets the generator/CLI (and possibly AOT-published
  helpers), not an automatic “no .NET ever” world for end users. Generated skills today are
  scripts (`call.cs` / `fsx` / `csx`) that invoke `dotnet`. Reducing or removing that runtime
  dependency for **skills** is a separate, optional future investigation — AOT-publishing the
  CLI alone does not remove it.
