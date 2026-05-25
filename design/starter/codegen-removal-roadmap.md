# ULinkRPC.CodeGen Removal Roadmap

Status: in progress, gated by source-generator validation

Date: 2026-05-25

Progress:

- 2026-05-25: Steps 1-3 landed for source-generator validation and sample migration. Samples no longer keep committed generated RPC glue and .NET/Godot sample builds rely on `ULinkRPC.Analyzers`.
- 2026-05-25: Step 4 landed. `ulinkrpc-starter codegen`, starter legacy project-tool codegen execution, and bundled CodeGen version resolution were removed.
- 2026-05-25: Step 5 landed. The `ULinkRPC.CodeGen` project, its test project, and test solution references were removed.
- Remaining removal work starts at step 6. Docs cleanup is still required to remove normal-workflow references to legacy CLI codegen.

## Decision

`ULinkRPC.CodeGen` is a legacy migration tool and should be removed once the source-generator path has enough test coverage and Unity/Tuanjie validation.

ULinkRPC is still in internal development, so this removal does not need a long compatibility window. Do not design new features around preserving the CLI codegen path.

## Removal Gate

Do not delete `ULinkRPC.CodeGen` until these checks are true:

- `ULinkRPC.Analyzers` is the only generator implementation used by starter-generated projects.
- Unity 2022 LTS, Unity CN, and Tuanjie compile `Rpc.Generated` client APIs through the analyzer/source-generator path.
- Server, Godot, Stride3D, Unity, Unity CN, and Tuanjie starter smoke tests cover generated client/server glue without committed generated source.
- Source-generator tests cover the behavior currently protected by CLI emitter tests: service clients, callback binders, facade shape, server binders, callback proxies, id constants, referenced contract assemblies, and failure diagnostics.
- Public docs and starter README no longer teach `ULinkRPC.CodeGen` as a normal workflow.

## Steps

1. Expand analyzer coverage
   - Move remaining behavior assertions from `tests/ULinkRPC.CodeGen.Tests` to source-generator-focused tests.
   - Keep parser/emitter tests only while they protect code still used by the analyzer path.

2. Validate Unity analyzer delivery
   - Prove the Unity-compatible starter imports `ULinkRPC.Analyzers` as a Roslyn analyzer/source-generator asset.
   - Add a Unity/Tuanjie smoke test that fails if `Rpc.Generated.RpcClient` is missing.
   - Do not add generated-source fallback files.

3. Migrate samples
   - Remove committed generated RPC glue from samples unless a sample is explicitly testing legacy migration.
   - Make sample builds rely on analyzer output.

4. Remove starter legacy command surface
   - Delete `ulinkrpc-starter codegen`.
   - Delete `StarterProjectTool`, `StarterCodeGenCommandOptions`, `ClientCodeGenMode`, and `ClientCodeGenOutput`.
   - Remove `CodeGen` from `ReleaseVersions.json` and version resolution.

5. Delete CLI package
   - Remove `src/ULinkRPC.CodeGen`.
   - Remove CLI-only tests and `tests/ULinkRPC.CodeGen.Tests` cases that no longer apply.
   - Remove `ULinkRPC.CodeGen` from `tests/Tests.slnx`.

6. Clean docs
   - Remove normal-workflow references to `ULinkRPC.CodeGen`.
   - Keep a short migration note only if there are still internal projects requiring manual cleanup.

## Non-Goals

- Do not preserve `ULinkRPC.CodeGen` for external compatibility.
- Do not introduce a second generator implementation for modern .NET and Unity separately.
- Do not reintroduce generated-source starter workflows to bridge temporary Unity import issues.

## Completion Criteria

The removal is complete when a clean checkout can run all tests and generate every starter target without `src/ULinkRPC.CodeGen`, `ulinkrpc-starter codegen`, generated RPC source directories, or local tool manifests.
