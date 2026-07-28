# Contract — Host open instructions (wiki)

Wiki MUST document opening `docs/interactive/multi-spec-guide.html` on each host.

## Required hosts

| Host | Documented surface | Minimum content |
|------|--------------------|-----------------|
| Cursor | Canvas | How to open the HTML (browser) and/or present content in a Canvas; paste-HTML fallback |
| GitHub Copilot | Canvas | Path/link + paste-HTML fallback |
| Claude | Artifact | Paste HTML as Artifact (or attach file if supported) |
| Codex | Sites | Open as site / paste fallback |

## Placement

Either:

- Section **Open interactive guide in hosts** inside `wiki/Multi-Spec-Skills.md`, or  
- Dedicated `wiki/Interactive-Guides.md` linked from Multi-Spec + Home + README  

## Agent guidance

Instructions MUST tell agents: do not invent CLI flags; copy from the guide/wiki; prefer Plan mode before running `generate`/`merge`/`add-spec`.
