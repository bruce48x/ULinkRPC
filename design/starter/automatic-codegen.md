# Automatic CodeGen Design

Status: proposed

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

This works, but contract edits require a manual regeneration step. The goal is to make regeneration automatic without moving code generation into any runtime path.

## Decision

Automatic codegen is feasible without runtime overhead.

The first implementation should keep the existing CLI model and add build/editor triggers around it:

- Server, Godot, and Stride3D should run codegen from MSBuild before compilation.
- Unity and Tuanjie should run codegen from an Editor-only integration when shared contract sources change.
- CI should run codegen and fail if generated files are stale.

Do not start by replacing the CLI with a C# Source Generator. Source generators are compile-time tools and can avoid runtime overhead, but they introduce Unity/Roslyn version constraints, assembly definition boundaries, cross-project output questions, and migration risk that are not needed for the first automatic-codegen milestone.

## Runtime Boundary

Automatic codegen must preserve these rules:

- Runtime packages do not reference `ULinkRPC.CodeGen`.
- Unity client runtime assemblies do not reference Roslyn.
- No `System.Reflection.Emit`.
- No generated proxy creation during connection, call dispatch, serialization, or service dispatch.
- Generated output remains normal C# compiled by the target project.

The automation changes when codegen runs, not what runtime code does.

## Current Shape

`ULinkRPC.CodeGen` already has the required input and output model:

- Input is a contracts source directory.
- Output path and generated namespace are CLI options.
- Server mode generates `AllServicesBinder.cs` plus per-service binders and callback proxies.
- Client modes generate `RpcApi.cs`, service clients, callback binders, and Unity's default `ULinkRPC.Generated.asmdef`.
- `ULinkRPC.Starter` knows the generated workspace layout and can regenerate server + client output in one command.

The missing pieces are trigger integration and stale-output validation.

## Recommended Implementation

For .NET project based clients and servers, add MSBuild integration to starter-generated projects.

A minimal shape is:

```xml
<Target Name="ULinkRPCCodeGen" BeforeTargets="CoreCompile">
  <Exec Command="dotnet tool run ulinkrpc-codegen -- --contracts &quot;$(ULinkRPCContractsPath)&quot; --mode server --server-output &quot;$(ULinkRPCServerOutput)&quot; --server-namespace &quot;$(ULinkRPCServerNamespace)&quot;" />
</Target>
```

The production implementation should not stop at this minimal target. It needs:

- explicit properties such as `ULinkRPCContractsPath`, `ULinkRPCCodeGenMode`, `ULinkRPCOutputPath`, and `ULinkRPCGeneratedNamespace`;
- declared inputs and outputs so builds do not run codegen unconditionally;
- a clear restore policy so ordinary builds do not unexpectedly access NuGet feeds;
- actionable errors that point users to `dotnet tool restore` or `ulinkrpc-starter codegen`.

For Unity and Tuanjie, do not rely on Unity-generated `.csproj` files. Add an Editor-only integration instead:

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

## Phases

### Phase 1: Design and stale checks

- Record the automatic-codegen boundary.
- Add stale generated-output checks in CI.
- Keep `ulinkrpc-starter codegen` as the explicit fallback command.

### Phase 2: .NET build integration

- Generate MSBuild properties and targets in starter projects.
- Enable automatic codegen for server, Godot, and Stride3D builds.
- Avoid network restore during normal builds.

### Phase 3: Unity Editor integration

- Add an Editor-only auto-generation entry point.
- Add a menu item for manual regeneration.
- Keep all generator and Roslyn references out of runtime assemblies.

### Phase 4: Source generator evaluation

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
