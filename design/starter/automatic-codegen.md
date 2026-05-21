# Starter-Generated CodeGen Hooks

Status: implemented as starter scaffolding

Date: 2026-05-20

## Context

`ULinkRPC.CodeGen` currently generates RPC client stubs, callback binders, and server binders from shared contract source code. It parses `[RpcService]`, `[RpcMethod]`, `[RpcCallback]`, and `[RpcPush]`, then writes normal C# files into each project's generated directory.

The current explicit entry point is:

```bash
ulinkrpc-starter codegen
```

or direct tool usage:

```bash
ulinkrpc-codegen --contracts ./Shared --mode server
ulinkrpc-codegen --contracts ./Shared --mode unity
```

This works, but contract edits require a manual regeneration step unless the project has build/editor hooks. The goal for this milestone is to have `ULinkRPC.Starter` scaffold those hooks into generated projects without moving code generation into any runtime path.

## Decision

Starter-generated automatic codegen hooks are feasible without runtime overhead.

The first implementation should keep the existing CLI model and add build/editor triggers around it:

- Server, Godot, and Stride3D should run codegen from MSBuild before compilation.
- Unity and Tuanjie should run codegen from an Editor-only integration when shared contract sources change.
- CI should run codegen and fail if generated files are stale.

Do not start by replacing the CLI with a C# Source Generator. Source generators are compile-time tools and can avoid runtime overhead, but they introduce Unity/Roslyn version constraints, assembly definition boundaries, cross-project output questions, and migration risk that are not needed for the first automatic-codegen milestone.

## Ownership Boundary

`ULinkRPC.Server` and `ULinkRPC.Client` are runtime packages. They should consume generated binders and generated clients, but they should not own code generation or automatic generation triggers.

Ownership is split this way:

- `ULinkRPC.CodeGen`: owns parser, validation, and emitter behavior.
- `ULinkRPC.Starter`: currently scaffolds project-local hooks because it knows starter-generated project layout.
- future `ULinkRPC.CodeGen.Build`: should own reusable MSBuild integration for non-starter projects.
- future `ULinkRPC.CodeGen.Unity.Editor`: should own reusable Unity/Tuanjie editor integration.
- `ULinkRPC.Server` / `ULinkRPC.Client`: remain runtime-only and do not reference Roslyn, MSBuild, dotnet tool APIs, or Unity Editor APIs.

The current starter implementation is therefore a bridge: starter writes the hooks into new projects, but it is not the long-term owner of build/editor integration logic.

## Runtime Boundary

Starter-generated hooks must preserve these rules:

- Runtime packages do not reference `ULinkRPC.CodeGen`.
- Unity client runtime assemblies do not reference Roslyn.
- No `System.Reflection.Emit`.
- No generated proxy creation during connection, call dispatch, serialization, or service dispatch.
- Generated output remains normal C# compiled by the target project.

The hooks change when codegen runs, not what runtime code does.

## Current Shape

`ULinkRPC.CodeGen` already has the required input and output model:

- Input is a contracts source directory.
- Output path and generated namespace are CLI options.
- Server mode generates `AllServicesBinder.cs` plus per-service binders and callback proxies.
- Client modes generate `RpcApi.cs`, service clients, callback binders, and Unity's default `ULinkRPC.Generated.asmdef`.
- `ULinkRPC.Starter` knows the generated workspace layout and can regenerate server + client output in one command.

The missing pieces were trigger integration and stale-output validation.

## Recommended Implementation

For .NET project based clients and servers, starter-generated projects include MSBuild integration.

A minimal shape is:

```xml
<Target Name="ULinkRPCCodeGen" BeforeTargets="CoreCompile">
  <Exec Command="dotnet tool run ulinkrpc-codegen -- --contracts &quot;$(ULinkRPCContractsPath)&quot; --mode server --server-output &quot;$(ULinkRPCServerOutput)&quot; --server-namespace &quot;$(ULinkRPCServerNamespace)&quot;" />
</Target>
```

The current starter-scaffolded implementation includes the core shape, but the long-term reusable implementation should move into a dedicated `ULinkRPC.CodeGen.Build` package. That future package should provide:

- explicit properties such as `ULinkRPCContractsPath`, `ULinkRPCCodeGenMode`, `ULinkRPCOutputPath`, and `ULinkRPCGeneratedNamespace`;
- declared inputs and outputs so builds do not run codegen unconditionally;
- a clear restore policy so ordinary builds do not unexpectedly access NuGet feeds;
- actionable errors that point users to `dotnet tool restore` or `ulinkrpc-starter codegen`.

For Unity and Tuanjie, do not rely on Unity-generated `.csproj` files. Starter-generated projects include an Editor-only integration instead:

- watch the shared contracts package, such as `Packages/com.samples.contracts/**/*.cs`;
- run before script compilation or from an asset-change callback;
- write only to `Assets/Scripts/Rpc/Generated/`;
- surface codegen failures in the Unity Console;
- keep a menu command for manual force-regeneration.

For CI, add a stale generated-output check:

```bash
ulinkrpc-starter codegen --no-restore
git diff --exit-code
```

This catches forgotten regenerated output and nondeterministic generator output.

## Why Not Source Generator First

Source generators remain a possible later optimization, but they are not the lowest-risk first step:

- The current generator targets `net10.0` and uses newer Roslyn packages than Unity 2022's conservative analyzer/source-generator path.
- Source generators do not naturally produce Unity `.asmdef` files, Godot project files, or cross-project directory output.
- Server binder discovery currently relies on generated types and an assembly attribute in the entry assembly.
- Starter projects have separate `Shared`, `Server`, and `Client` areas; the contracts and generated output are not always in the same compilation.
- Existing users benefit from visible, diffable generated files.

If source generation is revisited, first extract a shared `ULinkRPC.CodeGen.Core` library so the CLI, MSBuild integration, Unity Editor integration, and any future source generator can reuse the parser/emitter logic.

## Development Plan

| Status | Task | Notes |
| --- | --- | --- |
| 已完成 | Record the starter-generated codegen hook boundary | Captured in this design note. |
| 已完成 | Move internal planning out of public Hugo content | Internal design notes live under `design/`; public Hugo content lives under `blog/`. |
| 已完成 | Add stale generated-output checks in CI | `scripts/check-generated-code.ps1` runs sample codegen and `.github/workflows/codegen-check.yml` fails when generated files differ from committed output. |
| 已完成 | Keep `ulinkrpc-starter codegen` as the explicit fallback command | Existing explicit regeneration command remains available while automatic triggers are added. |
| 已完成 | Add starter-scaffolded .NET build integration | Starter-generated server, Godot, and Stride3D `.csproj` files include a pre-compile `ULinkRPCGenerateCode` target. |
| 已完成 | Add starter-scaffolded Unity/Tuanjie Editor integration | Starter-generated Unity-compatible clients include an Editor-only `ULinkRPCCodeGenEditor` asset postprocessor and menu command. |
| 待办 | Extract reusable build/editor integration ownership | Move common integration out of starter into future `ULinkRPC.CodeGen.Build` and `ULinkRPC.CodeGen.Unity.Editor` packages. |
| 已完成 | Evaluate source generator support | Deferred. Source generators remain a possible future optimization after `ULinkRPC.CodeGen.Core` extraction and compatibility validation. |

## Source Generator Evaluation

Source Generator support is explicitly deferred.

The current starter-generated hook milestone is complete when starter-generated projects can regenerate ordinary C# from build/editor hooks and CI can detect stale output. That path preserves generated files, Unity `.asmdef` output, server binder discovery, and the existing `ulinkrpc-starter codegen` repair workflow.

A future Source Generator effort must start with a separate design and at least these prerequisites:

- extract parser/emitter code into a shared `ULinkRPC.CodeGen.Core` library;
- define how Unity `.asmdef` and project-local generated directories are handled when source generators cannot create assets;
- validate Unity 2022/Tuanjie Roslyn analyzer package constraints separately from current `net10.0` tool dependencies;
- decide whether generated files remain checked in or become compiler-only output;
- preserve server binder discovery semantics without adding runtime code generation.

Until those questions are resolved, do not add a `ULinkRPC.CodeGen.SourceGenerator` package or source-generator analyzer reference to starter-generated projects. This does not block lightweight contract analyzers that validate explicit ids without generating code.

## Phases

### Phase 1: Design and stale checks

- Record the starter-generated hook boundary.
- Add stale generated-output checks in CI.
- Keep `ulinkrpc-starter codegen` as the explicit fallback command.

### Phase 2: Starter-scaffolded .NET build integration

- Generate MSBuild properties and targets in starter projects.
- Enable automatic codegen for server, Godot, and Stride3D builds.
- Avoid network restore during normal builds.

### Phase 3: Starter-scaffolded Unity Editor integration

- Add an Editor-only auto-generation entry point.
- Add a menu item for manual regeneration.
- Keep all generator and Roslyn references out of runtime assemblies.

### Phase 4: Reusable integration and source generator evaluation

- Extract reusable MSBuild and Unity Editor integration packages from starter-scaffolded templates.
- Extract shared codegen core.
- Prototype `ULinkRPC.CodeGen.SourceGenerator` only after the build/editor integration is stable.
- Decide whether removing checked-in generated files is worth the extra support burden.

## Non-goals

- Do not migrate RPC contracts to an IDL.
- Do not generate proxies at runtime.
- Do not change serializer selection or DTO versioning policy.
- Do not replace application-level service lifecycle or dependency injection decisions.

## User Experience Goal

After this work, users should be able to edit shared contracts and rely on the normal build/editor flow:

- .NET server builds refresh server binders.
- Godot and Stride3D builds refresh client stubs.
- Unity and Tuanjie refresh generated code from editor-time tooling.
- CI fails clearly when generated output is stale.

`ulinkrpc-starter codegen` should remain available for troubleshooting, CI, explicit local repair, and non-standard project layouts.
