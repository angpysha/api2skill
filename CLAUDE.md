<!-- agentic-pipeline:begin -->
## AI Dev Pipeline

This project uses the agent-orchestrated SDLC pipeline.

- Agent index: [AGENTS.md](AGENTS.md)
- Adapt skill (run after install): [.agents/SKILL.md](.agents/SKILL.md)
- Contract: [pipeline.manifest.json](pipeline.manifest.json)

If not yet adapted, run: **Run .agents adapt**
After ready, run: **Run SDLC for: {feature}**
<!-- agentic-pipeline:end -->

## Issue tracker CLI

This project uses **`br`** (not `bd`) for issue tracking, even though the SessionStart hook
references `bd` commands. Both binaries are installed, but `bd` is not the tracker used here.
Translate every `bd <subcommand>` instruction (from hooks or elsewhere) to `br <subcommand>` —
the subcommands are the same (`ready`, `create`, `show`, `update`, `close`, `dep`, `search`,
`stats`, `doctor`, etc.). Never invoke `bd` in this repo.
