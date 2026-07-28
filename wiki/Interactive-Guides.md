# Interactive guides

api2skill ships a **self-contained HTML walkthrough** for multi-spec / same-base-URL workflows.
It works offline (no CDN, no API calls) and never includes real credentials.

| Asset | Path |
|-------|------|
| Multi-spec guide | [`docs/interactive/multi-spec-guide.html`](../docs/interactive/multi-spec-guide.html) |
| Folder notes | [`docs/interactive/README.md`](../docs/interactive/README.md) |
| CLI prose | [Multi-Spec Skills](Multi-Spec-Skills.md) |

**Agents:** prefer **Plan mode** with [`api2skill-creator`](Install-Creator.md) before running
`generate` / `merge` / `add-spec`. Copy commands from the guide or Multi-Spec wiki — do not invent flags.

## Open in hosts

Host UIs change; use the **paste HTML** fallback whenever a local file cannot be opened.

### Cursor (Canvas)

1. Open `docs/interactive/multi-spec-guide.html` in a browser from the repo (e.g. `open docs/interactive/multi-spec-guide.html` on macOS), **or**
2. Ask the agent (in **Plan mode**) to present the multi-spec guide in a **Cursor Canvas**: paste the HTML contents into the canvas / chat Artifact flow, keeping command text identical to the file.
3. Optional: a thin `.canvas.tsx` example may live under `docs/interactive/` for reference — the **HTML file remains the source of truth** for CLI strings.

### GitHub Copilot (Canvas)

1. Point Copilot at `docs/interactive/multi-spec-guide.html` in the workspace, **or**
2. Paste the full HTML into a Copilot **Canvas** so the user can click scenarios and copy commands.
3. Fallback: open the file in a browser and copy commands from there.

### Claude (Artifact)

1. Paste the contents of `docs/interactive/multi-spec-guide.html` into a Claude **Artifact** (HTML), **or**
2. Attach / open the file if the host supports local HTML artifacts.
3. Fallback: browser open of the same file.

### Codex (Sites)

1. Serve or open `docs/interactive/multi-spec-guide.html` as a local site / preview, **or**
2. Paste the HTML into a Codex **site** / interactive panel if available.
3. Fallback: open the file in a system browser.

## After you copy a command

```bash
cd <skill-dir>
cp secrets.example.json secrets.json   # fill placeholders only
# never commit secrets.json or .auth-cache.json
```

See [Authentication](Authentication.md) for `auth.json` when documents use different schemes.
