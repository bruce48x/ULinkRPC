# Starter-Generated CodeGen Hooks

Status: superseded by [Source Generator CodeGen Route](source-generator-codegen.md)

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

## Legacy Status

The CLI and starter codegen hooks can remain temporarily as compatibility tools while the source generator route is implemented and validated. They are no longer the strategic direction.

Do not add new feature work that deepens dependency on:

- generated source directories as committed output;
- starter-owned MSBuild codegen targets;
- Unity Editor asset postprocessors that shell out to `dotnet`;
- `ulinkrpc-starter codegen` as the normal daily workflow.
