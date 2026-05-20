# Starter Dependency Planning

Status: proposed

Date: 2026-05-09

## Context

`ULinkRPC.Starter` generates multiple projects from the same user choices:

- `Shared/Shared.csproj`
- `Server/Server/Server.csproj`
- Unity / Tuanjie client `Assets/packages.config`
- Godot client `Client.csproj`
- Stride3D client `Client.csproj`

Those projects do not consume dependencies through the same mechanism.

- Server, Godot, and Stride3D consume `Shared` through SDK-style `.csproj` `ProjectReference`.
- Unity and Tuanjie consume `Shared` as a local UPM source package and restore runtime DLLs through NuGetForUnity `packages.config`.

This means dependency ownership is not purely feature-based. It also depends on the consumer model.

The immediate issue found in `memorypack` projects was:

- `Shared.csproj` directly references `ULinkRPC.Serializer.MemoryPack`.
- Server, Godot, and Stride3D also referenced `ULinkRPC.Serializer.MemoryPack`.
- Because Server, Godot, and Stride3D reference `Shared.csproj`, those serializer references were redundant.

The current fix removes the redundant `memorypack` serializer references from Server, Godot, and Stride3D while keeping JSON references explicit where `Shared.csproj` does not provide them.

## Current Rules

### Shared

`Shared.csproj` always directly references:

- `ULinkRPC.Core`

When `memorypack` is selected, `Shared.csproj` also directly references:

- `ULinkRPC.Serializer.MemoryPack`
- `MemoryPack`
- `MemoryPack.Generator`

When `json` is selected, `Shared.csproj` does not reference `ULinkRPC.Serializer.Json`.

### Server

Server consumes `Shared.csproj` through `ProjectReference`.

Server directly references:

- `ULinkRPC.Server`
- selected transport package

When `json` is selected, Server also directly references:

- `ULinkRPC.Serializer.Json`

When `memorypack` is selected, Server does not repeat the serializer/runtime packages already provided by `Shared.csproj`.

### Godot Client

Godot consumes `Shared.csproj` through `ProjectReference`.

Godot directly references:

- `ULinkRPC.Core`
- `ULinkRPC.Client`
- selected transport package

When `json` is selected, Godot also directly references:

- `ULinkRPC.Serializer.Json`

When `memorypack` is selected, Godot does not repeat the serializer/runtime packages already provided by `Shared.csproj`.

### Stride3D Client

Stride3D consumes `Shared.csproj` through `ProjectReference`.

Stride3D directly references:

- `Stride.CommunityToolkit.Windows`
- `Stride.CommunityToolkit.Bepu`
- `ULinkRPC.Core`
- `ULinkRPC.Client`
- selected transport package

When `json` is selected, Stride3D also directly references:

- `ULinkRPC.Serializer.Json`

When `memorypack` is selected, Stride3D does not repeat the serializer/runtime packages already provided by `Shared.csproj`.

### Unity / Tuanjie Client

Unity and Tuanjie consume `Shared` through a local UPM source package, not through SDK-style transitive restore.

Their `Assets/packages.config` must keep explicit runtime packages needed by Unity compilation and play mode, including:

- `ULinkRPC.Core`
- `ULinkRPC.Client`
- selected transport package
- selected serializer package
- serializer-specific Unity runtime dependencies

This is intentionally different from Server, Godot, and Stride3D. Do not remove Unity/Tuanjie serializer entries just because `Shared.csproj` contains a matching package reference.

## Problem With Scattered Template Logic

The current implementation still expresses these rules inside individual template builders:

- `StarterTemplateGenerator.Shared.cs`
- `StarterTemplateGenerator.Server.cs`
- `StarterGodotTemplate.cs`
- `StarterStrideTemplate.cs`
- `StarterTemplateGenerator.Unity.cs`

That is serviceable for the current two serializers, but it has clear maintenance risks:

- Adding a new serializer requires touching multiple templates.
- It is easy to accidentally duplicate a dependency in one generated project but not another.
- Tests mostly verify rendered strings instead of the dependency ownership model.
- Unity-specific package rules can leak into Server/Godot/Stride3D if the distinction is not explicit.

## Proposed Design

Introduce a centralized dependency planner for starter-generated projects.

Suggested shape:

```csharp
internal enum StarterProjectRole
{
    Shared,
    Server,
    UnityClient,
    GodotClient,
    StrideClient
}

internal sealed record StarterPackageReference(
    string Id,
    string Version,
    bool ManuallyInstalled = false,
    string? PrivateAssets = null,
    string? IncludeAssets = null);

internal sealed record StarterDependencyPlan(
    IReadOnlyList<StarterPackageReference> PackageReferences);

internal static class StarterDependencyPlanner
{
    public static StarterDependencyPlan Create(StarterTemplateContext context, StarterProjectRole role);
}
```

The planner should own decisions about:

- which packages are direct dependencies of each generated project
- which serializer dependencies are provided by `Shared`
- which consumers can rely on SDK-style transitive references
- which Unity/Tuanjie packages must remain explicit because NuGetForUnity does not consume `Shared.csproj` transitively
- serializer-specific runtime/analyzer package metadata such as `PrivateAssets` and `IncludeAssets`

Templates should only render the plan.

## Testing Strategy

Add focused unit tests for the planner before further template expansion:

- `Shared + memorypack` includes `ULinkRPC.Serializer.MemoryPack`, `MemoryPack`, and `MemoryPack.Generator`.
- `Shared + json` does not include `ULinkRPC.Serializer.Json`.
- `Server + memorypack` does not include `ULinkRPC.Serializer.MemoryPack`.
- `Server + json` includes `ULinkRPC.Serializer.Json`.
- `Godot + memorypack` does not include `ULinkRPC.Serializer.MemoryPack`, `MemoryPack`, or `MemoryPack.Core`.
- `Godot + json` includes `ULinkRPC.Serializer.Json`.
- `Stride3D + memorypack` does not include `ULinkRPC.Serializer.MemoryPack`, `MemoryPack`, or `MemoryPack.Core`.
- `Stride3D + json` includes `ULinkRPC.Serializer.Json`.
- `Unity/Tuanjie + memorypack` still includes explicit serializer and Unity runtime dependencies.
- `Unity/Tuanjie + json` still includes explicit JSON serializer and JSON runtime dependencies.

Rendered template tests should remain, but they should verify integration rather than carry the whole dependency model.

## Acceptance Criteria

- Dependency ownership rules live in one planner instead of scattered template switches.
- Server, Godot, and Stride3D continue to avoid redundant `memorypack` package declarations.
- Unity and Tuanjie continue to receive all packages needed by NuGetForUnity restore.
- Existing starter template tests pass.
- New planner tests describe the expected dependency matrix directly.
