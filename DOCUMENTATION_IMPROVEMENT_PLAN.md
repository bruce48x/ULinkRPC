# ULinkRPC Documentation Improvement Plan

This plan turns the documentation review into concrete work. The first goal is trustworthiness: existing docs must match the current repository before the documentation surface is expanded.

## Current Assessment

The project already has a useful narrative foundation:

- The starter tutorial explains the intended workflow clearly: edit `Shared`, run codegen, then implement server and client logic.
- The architecture article explains the contract / codegen / runtime split and the single-DTO method shape well.
- Starter design notes use an ADR-like structure and are valuable for maintainers.
- `CONTRIBUTING.md` captures important Unity, IL2CPP, async, testing, and release constraints.

The main gaps are:

- Some existing docs are stale or internally inconsistent.
- There is no API reference for the core runtime types.
- Several operational topics are missing: errors, security, lifecycle, DTO evolution, threading, and performance.
- The Hugo docs site is closer to a small article site than a structured framework manual.
- Package README files are too thin to stand alone on NuGet.
- The bilingual strategy is inconsistent across root docs, package docs, and the docs site.

## Priority Order

### P0: Restore Documentation Trust

These are correctness fixes. They block larger documentation work because users must be able to trust what is already written.

- Replace stale sample paths in `CONTRIBUTING.md` with the current `samples/RpcCall.<Variant>/...` layout.
- Remove or explain conflicts between coding rules and tutorial examples, especially `ValueTask.FromResult(...)`.
- Make root README quick-start examples complete enough to compile or clearly mark them as excerpts.
- Align project positioning text across README, docs homepage, and about page so Unity, Godot, and .NET support are described consistently.
- Add lightweight consistency checks for stale paths and known forbidden snippets.

Acceptance checks:

- `rg "samples/RpcCall/RpcCall|Game.Rpc.Runtime|ValueTask.FromResult" README.md README.zh-CN.md CONTRIBUTING.md docs -g "*.md"` has no hits except the deliberate `CONTRIBUTING.md` rule.
- README examples define every DTO type they use.
- The docs homepage and about page mention the same supported platforms as the root README.

### P1: Add Core Reference Material

These docs let users answer API questions without reading source code.

- Add API reference pages for:
  - `RpcClientOptions`
  - `RpcClientRuntime`
  - generated `RpcClient`
  - `RpcSession`
  - `RpcServerHostBuilder`
  - `ITransport`
  - `IRpcSerializer`
  - `RpcEnvelopeCodec` and envelope types
  - `RpcKeepAliveOptions`
  - `TransportSecurityConfig`
- Decide whether reference pages are generated from XML docs, hand-authored in Hugo, or both.
- Add XML documentation comments to public APIs that are part of the stable surface.
- Generate Hugo API reference pages from XML documentation comments instead of hand-maintaining API facts.

Acceptance checks:

- Each public runtime package has at least one reference entry that describes construction, lifecycle, cancellation, exceptions, and thread-safety expectations.
- NuGet package README files link to the relevant reference page.

### P2: Add Missing Guides

These docs cover the decisions teams need before production use.

- Error handling: `RpcStatus`, server exception propagation, response error payloads, and client failure modes.
- Security model: what `TransportSecurityConfig` protects, what it does not protect, and how it relates to TLS / WSS.
- DTO versioning: JSON and MemoryPack compatibility rules, field addition/removal guidance, and rollout constraints.
- Connection lifecycle: connect, disconnect, idle timeout, keepalive, shutdown, and reconnect ownership.
- Threading model: where callbacks run, Unity main-thread responsibilities, and user-code synchronization requirements.
- Performance tuning: serializer and transport tradeoffs, keepalive costs, payload size, and benchmarking status.
- Godot guide: a first-class path matching the Unity starter path.
- Selection guide: when to choose ULinkRPC, when not to, and how it compares with hand-written message dispatch or schema-first RPC tools.

Acceptance checks:

- Each missing guide has a standalone page under `docs/content`.
- The getting-started tutorial links to the relevant production-readiness guides at the end.

### P3: Improve Information Architecture

These changes make the docs easier to navigate and maintain.

- Split the docs site into stable sections:
  - Getting Started
  - Concepts
  - Guides
  - Reference
  - Samples
  - Troubleshooting
  - Contributing
- Make `src/ULinkRPC.Starter/README.md` a CLI reference and point workflow guidance to the docs site.
- Convert repeated design-boundary text into a canonical docs page and link to it from package README files.
- Define a language strategy:
  - root README: English plus Chinese mirror
  - package README: English reference summary
  - docs site: Chinese first, with English pages added intentionally rather than partially mixed

Acceptance checks:

- The docs homepage lists all major sections.
- Package README files are short, non-duplicative, and link to docs pages.
- Duplicated tutorial content has one canonical owner.

## Work Log

- Started P0 by documenting this plan.
- Started P0 fixes for stale sample paths, README example completeness, and docs-site platform wording.
- Started P1 by enabling XML documentation output for core runtime packages and adding a generator script.
- Generated the first API reference page from XML documentation comments.
- Added a lightweight docs consistency check for stale path/runtime snippets and the known forbidden `ValueTask.FromResult(...)` example pattern.
- Added P3 documentation section scaffolding for Getting Started, Concepts, Reference, Samples, Troubleshooting, and Contributing without moving existing posts.
- Expanded XML documentation and regenerated API reference coverage for envelope and codec types.
- Added API reference links to package README files.
- Added standalone P2 production-readiness guides and linked them from the getting-started tutorial.
