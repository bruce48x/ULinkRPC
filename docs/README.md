# ULinkRPC Docs

This directory contains a Hexo site for GitHub Pages deployment.

## Local usage

```bash
cd docs
npm install
npm run server
```

## Build

```bash
cd docs
npm run build
```

## GitHub Pages

The repository workflow builds this site from `docs/` and deploys the generated `docs/public/` artifact to GitHub Pages.

Before the first deploy, update `docs/_config.yml`:

- Set `url` to your GitHub Pages domain, for example `https://bruce48x.github.io`
- Keep `root: /ULinkRPC/` when publishing this repository as a project site

If you later move this site to a user or organization site repository, change `root` to `/`.
