# Source Generator CodeGen Route

Status: accepted route

Date: 2026-05-23

## Decision

ULinkRPC will switch the primary RPC glue generation route from starter-scaffolded CLI codegen to Roslyn source generators.

The generated client facade, service clients, callback binders, server binders, and server binder assembly attribute should be produced during C# compilation by an analyzer/source-generator package. New starter projects should not depend on project-local `Generated/` directories, MSBuild `ULinkRPCGenerateCode` targets, Unity Editor codegen postprocessors, or a local `ULinkRPC.CodeGen` tool manifest for the daily workflow.

The existing `ULinkRPC.CodeGen` CLI remains only as a migration and troubleshooting tool until the source generator path has feature parity and package adoption is stable.

## Goals

- Generated RPC glue is compiler output, not user-managed source files.
- Normal `dotnet build`, Unity/Tuanjie script compilation, Godot C# build, and Stride3D build produce the required RPC glue without extra commands.
- Runtime packages remain free of Roslyn, MSBuild, Unity Editor APIs, reflection emit, and runtime code generation.
- Contract attributes remain the explicit protocol declaration surface.
- The migration preserves deterministic generated code and generated API names as much as possible.
- Starter-generated projects become simpler: package references configure generation, instead of starter writing build/editor hooks.

## Non-Goals

- Do not introduce runtime proxy generation.
- Do not replace C# contracts with an IDL.
- Do not remove explicit protocol ids from `[RpcService]`, `[RpcMethod]`, `[RpcCallback]`, or `[RpcPush]`.
- Do not require Unity players or game runtime assemblies to reference Roslyn.
- Do not keep generated files committed for new starter projects after source generator parity is reached.

## Attribute Requirement

Source generation still requires attributes. They are the protocol contract, not an opt-in switch for generation:

- `[RpcService(id)]` marks service interfaces.
- `[RpcMethod(id)]` marks RPC methods.
- `[RpcCallback(typeof(IService))]` marks callback interfaces.
- `[RpcPush(id)]` marks server-to-client push methods.

The generator must support ids expressed as constants, such as `RpcContractIds.Services.Ping`, because starter already generates centralized id constants.

## Ownership

The source generator route uses four layers:

- `ULinkRPC.Core`: runtime contracts and attributes only.
- `ULinkRPC.CodeGen.Core`: shared symbol model, validation, naming, and emitters reusable by CLI and source generator.
- `ULinkRPC.CodeGen`: legacy CLI wrapper over the shared core.
- `ULinkRPC.Analyzers`: analyzer/source-generator package shipped under `analyzers/dotnet/cs`.

`ULinkRPC.Analyzers` already exists for contract-id diagnostics, so it is the package that carries the generator. The first route-switch implementation uses symbol-model generation directly in `ULinkRPC.Analyzers`; a later cleanup should extract shared naming/emitter logic into `ULinkRPC.CodeGen.Core` so the legacy CLI and source generator share one implementation.

## Generator Shape

The generator should inspect the current compilation and referenced contract assemblies for ULinkRPC attributes.

Generation mode is controlled by MSBuild/analyzer-config properties:

- `ULinkRPCGenerateClient`: emits client facade, service clients, and callback binders.
- `ULinkRPCGenerateServer`: emits server binders, callback proxies, `AllServicesBinder`, and the assembly-level `RpcGeneratedServicesBinder` attribute.
- `ULinkRPCGeneratedNamespace`: defaults to `Rpc.Generated` for clients.
- `ULinkRPCServerGeneratedNamespace`: defaults to `Server.Generated` for starter server projects.

Starter should set these properties in generated `.csproj` files and Unity-compatible config assets instead of writing command-line codegen hooks.

## Platform Decisions

### Server

Server projects reference the analyzer/source-generator package and set `ULinkRPCGenerateServer=true`.

The generator emits `AllServicesBinder` and the assembly attribute into the server compilation. Existing runtime discovery through `RpcGeneratedServicesBinderAttribute` remains valid.

### Godot and Stride3D

Godot and Stride3D client projects reference the analyzer/source-generator package and set `ULinkRPCGenerateClient=true`.

Generated client types compile into the client project assembly. No `Scripts/Rpc/Generated/` files are required for new starter projects.

### Unity, Unity CN, and Tuanjie

Unity-compatible clients must validate source-generator support through the actual Unity/Tuanjie compiler pipeline before the old hook path is removed from released starter templates.

The intended end state is:

- NuGetForUnity restores `ULinkRPC.Analyzers` as an analyzer/source-generator asset.
- The runtime/testing assembly that consumes generated client APIs references the contracts, runtime packages, and analyzer package.
- Exactly one Unity client assembly opts in with `[assembly: ULinkRPCGenerateClient("Rpc.Generated")]`; other Unity assemblies must not auto-generate duplicate `Rpc.Generated` facades.
- Generated `Rpc.Generated` types are compiler output in the consuming assembly.
- No Editor script shells out to `dotnet`.

If Unity 2022/Tuanjie analyzer support blocks direct generator execution, the fallback is a package-provided Unity Editor integration owned by the generator package, not starter-scaffolded project-local code.

## Migration Plan

| Phase | Status | Work |
| --- | --- | --- |
| 0 | Done | Accept source-generator route and stop expanding starter-scaffolded codegen hooks. |
| 1 | Deferred | Extract `ULinkRPC.CodeGen.Core` targeting `netstandard2.0`; the first implementation lives in `ULinkRPC.Analyzers` to complete the route switch before a mechanical extraction. |
| 2 | Done | Implement source generator in `ULinkRPC.Analyzers` with client/server generation modes, analyzer-config properties, and runtime-based auto-detection. |
| 3 | Done | Add source-generator compilation tests for server binders, client facade, constants, and referenced contract assemblies. |
| 4 | Done | Update starter templates for Server, Godot, and Stride3D to reference analyzer/source-generator packages and remove MSBuild `ULinkRPCGenerateCode` targets. |
| 5 | Done | Remove project-local Unity/Tuanjie Editor codegen postprocessor from new starter templates and rely on the analyzer package restored into Unity-compatible projects. |
| 6 | In Progress | Update samples and docs to remove committed generated glue from new-project guidance; keep old generated samples only where they test CLI compatibility. |
| 7 | Done | Mark `ulinkrpc-starter codegen` and direct CLI generation as legacy repair paths; remove them from the recommended workflow. |

The planned deletion path for the legacy CLI is tracked in [`codegen-removal-roadmap.md`](codegen-removal-roadmap.md).

## Starter Template Changes

New starter templates should move from "generate files now and add hooks" to "configure compiler generation":

- Remove local `ULinkRPC.CodeGen` tool manifest installation for new projects.
- Stop creating `Generated/`, `Assets/Scripts/Rpc/Generated/`, and `Scripts/Rpc/Generated/` as required source folders.
- Stop writing `ULinkRPCGenerateCode` MSBuild targets.
- Stop writing Unity `ULinkRPCCodeGenEditor.cs`.
- Add analyzer/source-generator package references.
- Add generation-mode properties per project.
- Keep contract attributes and id constants in `Shared`.
- Keep CLI regeneration available only through an explicit legacy flag or command.

## Compatibility Rules

- Existing projects using generated files and codegen hooks continue to build during the migration window.
- Runtime APIs should not require application code to know whether glue came from CLI or source generator.
- Generated type names and namespaces should remain stable unless a breaking-change release explicitly says otherwise.
- If a source-generated type conflicts with a committed generated file during migration, the compiler error should point users to remove old generated output or disable one generation path.

## Release Criteria

The route is not complete until these checks pass:

- Source-generated server binder discovery works through `RpcServerHostBuilder`.
- Source-generated client facade compiles and executes the same behavior tests as CLI-generated code.
- Starter-generated Server, Godot, and Stride3D projects build without generated source directories or codegen hooks.
- Unity 2022 LTS and Tuanjie compile generated client APIs through the intended package path.
- CI has a generator parity test comparing source-generator output behavior against current CLI emitter behavior.
- Public docs describe source generation as the normal workflow and CLI codegen as legacy troubleshooting.

## Risks

- Unity/Tuanjie source-generator support may differ from normal SDK builds.
- Source generators cannot create `.asmdef`, `.meta`, or project files, so starter still owns static project structure.
- Inspecting referenced contract assemblies is different from parsing source files and may expose symbol-model gaps.
- Generated source no longer appears in the workspace by default, which changes debugging and support workflows.

These risks are accepted. They should be handled through phased validation and explicit migration tooling, not by keeping starter-scaffolded CLI hooks as the strategic route.
