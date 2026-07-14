# How to Read These Docs

Documentation for **api2skill** lives **in this repository** under the `wiki/` folder. There is
no separate GitHub wiki git repository to clone or sync.

## Browse on GitHub

1. Open the repository on GitHub.
2. Go to the **`wiki/`** folder in the file tree.
3. Start at **[Home.md](Home.md)** — it links to every topic page.

GitHub renders Markdown (including [Mermaid](https://github.blog/2022-02-14-include-diagrams-markdown-files-mermaid/) diagrams) when you view `.md` files in the repo.

## Read locally

Clone or download this repository and open any file under `wiki/` in your editor or Markdown viewer:

```bash
git clone https://github.com/{owner}/api2skill.git
cd api2skill/wiki
# open Home.md
```

Relative links between pages work the same locally and on GitHub.

## Edit workflow

1. Change pages under `wiki/` on the main branch (pull requests welcome).
2. Commit and push — documentation updates ship with the code.
3. No sync script or `{repo}.wiki.git` clone is required.

## Optional: publish as a website

If you want a browsable site instead of the GitHub file tree, you can later enable
[GitHub Pages](https://docs.github.com/en/pages) from `/docs` or `/wiki` (for example with
Jekyll or another static-site generator). That is optional — the in-repo Markdown is the
authoritative source.

## Page index

| File | Topic |
|------|-------|
| [Home.md](Home.md) | Wiki home — quick links and overview |
| [Getting-Started.md](Getting-Started.md) | Install, first skill, Claude setup |
| [Generate-Command.md](Generate-Command.md) | `generate` reference |
| [Update-Command.md](Update-Command.md) | `update` reference |
| [Authentication.md](Authentication.md) | Auth profiles, OAuth, HTTPS loopback / `dev-certs`, script auth |
| [Examples.md](Examples.md) | Authored request/response examples |
| [Releasing.md](Releasing.md) | Version bumps, tags, release flow |
