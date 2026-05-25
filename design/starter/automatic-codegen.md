# Starter-Generated CodeGen Hooks (Archived)

Status: archived; superseded by [Source Generator CodeGen Route](source-generator-codegen.md) and [ULinkRPC.CodeGen Removal Roadmap](codegen-removal-roadmap.md)

Date: 2026-05-20

Superseded: 2026-05-23

## Context

This note captured the previous starter-generated codegen hook route:

- Server, Godot, and Stride3D projects ran `ULinkRPC.CodeGen` from MSBuild before compilation.
- Unity, Unity CN, and Tuanjie projects used an Editor-only asset-change hook and `ULinkRPC/Regenerate RPC Code` menu item.
- `ulinkrpc-starter codegen` remained the explicit repair command.

That route solved stale generated files without adding runtime code generation, but it still left generated `.cs` files as project artifacts and required starter-owned build/editor integration.

## Superseding Decision

The current product direction is to abandon starter-scaffolded CLI codegen as the primary route and move to Roslyn source generation.

The new route is documented in [Source Generator CodeGen Route](source-generator-codegen.md). Future work should use that document as the source of truth for architecture, migration phases, starter changes, and release criteria.

## Current Status

This document is historical context only. Do not use it as current implementation guidance.

`ULinkRPC.CodeGen` is planned for removal after source-generator validation is complete. The active removal plan is documented in [ULinkRPC.CodeGen Removal Roadmap](codegen-removal-roadmap.md).

Starter-generated codegen hooks are no longer an acceptable fallback for new starter projects or Unity/Tuanjie compile failures.

Do not add new feature work that deepens dependency on:

- generated source directories as committed output;
- starter-owned MSBuild codegen targets;
- Unity Editor asset postprocessors that shell out to `dotnet`;
- `ulinkrpc-starter codegen` as the normal daily workflow.
