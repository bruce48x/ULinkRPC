# ULinkRPC Docs

This directory contains a Hugo site for GitHub Pages deployment.

## Local usage

```bash
cd docs
hugo server
```

## Build

```bash
cd docs
hugo
```

## GitHub Pages

The repository workflow builds this site from `docs/` and deploys the generated `docs/public/` artifact to GitHub Pages.

The site base URL is configured in `docs/hugo.toml` for the repository project site:

- `https://bruce48x.github.io/ULinkRPC/`

If you later move this site to a user or organization site repository, update `baseURL` accordingly.
