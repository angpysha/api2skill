<!-- agentic-pipeline:begin -->
## AI Dev Pipeline

This project uses the agent-orchestrated SDLC pipeline.

- Agent index: [AGENTS.md](AGENTS.md)
- Skills/agents/rules: `.agents/` (linked into `.claude/` — edit the `.agents/` file)
- Contract: [pipeline.manifest.json](pipeline.manifest.json)

If not yet adapted, run: **/pipeline-adapt mode=init**
After ready, run: **Run SDLC for: {feature}**
After upgrading the tool: `agentic-tool update` (keeps settings)
<!-- agentic-pipeline:end -->

## Issue tracker CLI

This project uses **`br`** (not `bd`) for issue tracking, even though the SessionStart hook
references `bd` commands. Both binaries are installed, but `bd` is not the tracker used here.
Translate every `bd <subcommand>` instruction (from hooks or elsewhere) to `br <subcommand>` —
the subcommands are the same (`ready`, `create`, `show`, `update`, `close`, `dep`, `search`,
`stats`, `doctor`, etc.). Never invoke `bd` in this repo.
