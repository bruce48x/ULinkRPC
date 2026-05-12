# ULinkRPC Docs

This directory contains a Hugo site for GitHub Pages deployment.

## Documentation sections

The site is organized around stable entry points:

- [Getting Started](content/getting-started/_index.md)
- [Concepts](content/concepts/_index.md)
- [Guides](content/guides/_index.md)
- [Reference](content/reference/_index.md)
- [Samples](content/samples/_index.md)
- [Troubleshooting](content/troubleshooting/_index.md)
- [Contributing](content/contributing/_index.md)

Canonical pages that package READMEs and root docs should link to instead of duplicating long explanations:

- [Design boundary](content/concepts/design-boundary.md)
- [Generated RpcClient](content/reference/generated-client.md)

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
