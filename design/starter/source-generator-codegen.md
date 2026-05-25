# Source Generator Route

Status: implemented as the only starter generation route

Date: 2026-05-23

## Decision

ULinkRPC uses Roslyn source generators as the RPC glue generation route for starter-generated projects.

The generated client facade, service clients, callback binders, server binders, and server binder assembly attribute should be produced during C# compilation by an analyzer/source-generator package. New starter projects should not depend on project-local `Generated/` directories, MSBuild `ULinkRPCGenerateCode` targets, Unity Editor codegen postprocessors, or local generator tool manifests for the daily workflow.

The legacy CLI generator has been removed from the repository. New work must target `ULinkRPC.Analyzers`.

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

The source generator route uses these layers:

- `ULinkRPC.Core`: runtime contracts and attributes only.
- `ULinkRPC.Analyzers`: analyzer/source-generator package shipped under `analyzers/dotnet/cs`.

`ULinkRPC.Analyzers` carries contract-id diagnostics and source generation. Naming, validation, and emitter logic live in this package unless a future shared library has a runtime-independent reason to exist.

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

Unity-compatible clients must validate source-generator support through the actual Unity/Tuanjie compiler pipeline.

The intended end state is:

- NuGetForUnity restores `ULinkRPC.Analyzers` as an analyzer/source-generator asset.
- The runtime/testing assembly that consumes generated client APIs references the contracts, runtime packages, and analyzer package.
- Exactly one Unity client assembly opts in with `[assembly: ULinkRPCGenerateClient("Rpc.Generated")]`; other Unity assemblies must not auto-generate duplicate `Rpc.Generated` facades.
- Generated `Rpc.Generated` types are compiler output in the consuming assembly.
- No Editor script shells out to `dotnet`.

If Unity 2022/Tuanjie analyzer support regresses, fix analyzer compatibility, packaging, import metadata, or Unity compiler integration. Do not reintroduce starter-scaffolded project-local generated source.

## Migration Plan

| Phase | Status | Work |
| --- | --- | --- |
| 0 | Done | Accept source-generator route and stop expanding starter-scaffolded codegen hooks. |
| 1 | Not planned | Keep generator implementation in `ULinkRPC.Analyzers`; no CLI/shared-core split remains after legacy CLI deletion. |
| 2 | Done | Implement source generator in `ULinkRPC.Analyzers` with client/server generation modes, analyzer-config properties, and runtime-based auto-detection. |
| 3 | Done | Add source-generator compilation tests for server binders, client facade, constants, and referenced contract assemblies. |
| 4 | Done | Update starter templates for Server, Godot, and Stride3D to reference analyzer/source-generator packages and remove MSBuild `ULinkRPCGenerateCode` targets. |
| 5 | Done | Remove project-local Unity/Tuanjie Editor codegen postprocessor from new starter templates and rely on the analyzer package restored into Unity-compatible projects. |
| 6 | Done | Update samples and docs to remove committed generated glue from new-project guidance. |
| 7 | Done | Remove `ulinkrpc-starter codegen` and direct CLI generation from the product surface. |

The legacy CLI deletion path is tracked in [`codegen-removal-roadmap.md`](codegen-removal-roadmap.md).

## Starter Template Changes

New starter templates should move from "generate files now and add hooks" to "configure compiler generation":

- Do not create local generator tool manifests for new projects.
- Stop creating `Generated/`, `Assets/Scripts/Rpc/Generated/`, and `Scripts/Rpc/Generated/` as required source folders.
- Stop writing `ULinkRPCGenerateCode` MSBuild targets.
- Stop writing Unity `ULinkRPCCodeGenEditor.cs`.
- Add analyzer/source-generator package references.
- Add generation-mode properties per project.
- Keep contract attributes and id constants in `Shared`.

## Compatibility Rules

- Runtime APIs should not require application code to know whether glue came from CLI or source generator.
- Generated type names and namespaces should remain stable unless a breaking-change release explicitly says otherwise.
- If a source-generated type conflicts with a committed generated file during migration, remove the old generated output.

## Release Criteria

The route is not complete until these checks pass:

- Source-generated server binder discovery works through `RpcServerHostBuilder`.
- Source-generated client facade compiles and executes behavior tests for service calls and callbacks.
- Starter-generated Server, Godot, and Stride3D projects build without generated source directories or codegen hooks.
- Unity 2022 LTS and Tuanjie compile generated client APIs through the intended package path.
- Public docs describe source generation as the normal workflow.

## Risks

- Unity/Tuanjie source-generator support may differ from normal SDK builds.
- Source generators cannot create `.asmdef`, `.meta`, or project files, so starter still owns static project structure.
- Inspecting referenced contract assemblies is different from parsing source files and may expose symbol-model gaps.
- Generated source no longer appears in the workspace by default, which changes debugging and support workflows.

These risks are accepted. They should be handled through phased validation and explicit migration tooling, not by keeping starter-scaffolded CLI hooks as the strategic route.
