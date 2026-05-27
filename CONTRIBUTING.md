# Contributing Guide

This document is for contributors, maintainers, and AI agents working on the repository itself.

User-facing introduction, samples, and tutorial links live in [README.md](./README.md). Package-specific user docs live in each package `README.md`, such as [src/ULinkRPC.Starter/README.md](./src/ULinkRPC.Starter/README.md).

ULinkRPC targets **Unity 2022 LTS** with **iOS / IL2CPP / HybridCLR** compatibility. Stability and platform compatibility take priority over convenience.

---

## 1. Quick Workflow

Use the narrowest verification that covers your change, then run broader tests when touching shared runtime behavior.

Common commands:

```powershell
dotnet test tests\Tests.slnx
```

Focused test projects:

- Analyzer/source generation: `dotnet test tests/ULinkRPC.Analyzers.Tests/ULinkRPC.Analyzers.Tests.csproj`
- Core client/server runtime: `dotnet test tests/ULinkRPC.Tests/ULinkRPC.Tests.csproj`
- Transport implementations: `dotnet test tests/ULinkRPC.Transport.Tests/ULinkRPC.Transport.Tests.csproj`
- Serializers: `dotnet test tests/ULinkRPC.Serializer.Tests/ULinkRPC.Serializer.Tests.csproj`
- Starter templates and CLI: `dotnet test tests/ULinkRPC.Starter.Tests/ULinkRPC.Starter.Tests.csproj`

Before committing:

- Keep changes scoped to the package, sample, or test area implied by the task.
- Do not bump package versions unless the work is intended for a package release.
- Update tests when moving source files that source-scan tests read directly.
- Avoid committing generated RPC glue, build output, editor caches, or local tool artifacts.

---

## 2. Repository Layout

### Runtime and tooling packages

- `src/ULinkRPC.Core`: transport abstraction, framing/security helpers, envelopes, serializer abstractions.
- `src/ULinkRPC.Client`: RPC client runtime and generated-client support types.
- `src/ULinkRPC.Server`: RPC server runtime, host builder, dispatching, and sessions.
- `src/ULinkRPC.Transport.Tcp` / `src/ULinkRPC.Transport.WebSocket` / `src/ULinkRPC.Transport.Kcp` / `src/ULinkRPC.Transport.Loopback`: transport implementations.
- `src/ULinkRPC.Serializer.MemoryPack` / `src/ULinkRPC.Serializer.Json`: payload serializer implementations.
- `src/ULinkRPC.Analyzers`: analyzer and source generator package.
- `src/ULinkRPC.Starter`: CLI tool that scaffolds runnable shared/server/client workspaces.

### Tests, samples, and docs

- Core .NET test projects live under `tests/ULinkRPC.*.Tests/**`.
- Unity test assemblies use `*.Tests.Editor` / `*.Tests.PlayMode` with NUnit + Unity Test Framework.
- Server-side .NET sample projects live under `samples/**`.
- Design notes and maintainer-facing decisions live under `design/**`; do not overload package README files with internal design history.

No circular dependencies between assemblies.

---

## 3. Architecture Rules

### Assembly boundaries

- `ULinkRPC.Core` defines shared abstractions and protocol primitives. It must not depend on concrete transports, serializers, client runtime, server runtime, Unity, or Godot.
- `ULinkRPC.Client` and `ULinkRPC.Server` depend on `ULinkRPC.Core`, not on concrete transport or serializer packages.
- Transport packages implement `ITransport` and connection acceptors without leaking transport-specific assumptions into core RPC code.
- Serializer packages implement `IRpcSerializer` without owning transport or session behavior.
- Starter code may reference package names and versions for generation, but generated projects must preserve normal runtime package boundaries.

### Contracts are the single source of truth

- Do not duplicate sample contracts into server-local copies.
- Shared contracts should live in the shared/client contract package or project used by both server and client.
- Sample server projects must reference or link the shared contract sources from the matching sample shared/client package.
- If a server change requires a contract update, edit the shared contract source, not a server-local copy.

For Unity samples that keep contracts in a local UPM package, server projects should link those sources, for example:

```xml
<Compile Include="..\..\RpcCall.Json.Unity\Packages\com.samples.contracts\**\*.cs" />
```

---

## 4. Unity / IL2CPP Compatibility

Allowed Unity-compatible runtime dependencies:

- `System.Threading.Channels` is intentionally used by ULinkRPC runtime packages and Unity samples. Keep it as an explicit package dependency when runtime code needs async producer/consumer queues.
- `System.IO.Pipelines` may appear through transport or serializer package dependency chains. Do not treat its presence alone as a Unity / IL2CPP violation; validate the affected package path on Unity 2022 LTS, iOS, IL2CPP, and HybridCLR before expanding support claims.

Forbidden in Unity client code, including Unity tests:

- `System.Reflection.Emit`
- Runtime code generation
- APIs relying on JIT-only behavior

If an API is common on server-side .NET but not explicitly supported by Unity IL2CPP, treat it as **unsafe**.

Unity client code and shared contracts must compile with **C# 9.0**. Do not use C# 10+ syntax in Unity-facing code.

---

## 5. Runtime Safety

### Async and lifetime rules

- Prefer explicit lifetimes: `DisposeAsync`, `StopAsync`, and clear ownership of transports/sessions.
- Clean up background loops in tests.
- Avoid implicit global state.
- Favor clarity over micro-optimizations in shared infrastructure.

Allowed `ValueTask` patterns only:

- `return default;` for `ValueTask`
- `return new ValueTask<T>(value);`
- `async` methods returning `ValueTask<T>` with `return value;`

Forbidden:

- `ValueTask.CompletedTask`
- `ValueTask.FromResult(...)`

### Transport and networking rules

- All transports must implement `ITransport`.
- RPC code must not depend on a specific transport.
- Transport implementations must be cancellation-safe.
- Transport implementations must avoid background thread leaks.
- Disconnect behavior must be explicit and testable.
- Prefer `LoopbackTransport` for local RPC tests that do not need real sockets.

---

## 6. Source Generation

RPC client facades, service clients, callback binders, server binders, callback proxies, and binder discovery attributes are generated by `ULinkRPC.Analyzers` during compilation.

- New starter projects must use `ULinkRPC.Analyzers` source generation as the normal RPC glue path.
- Do **not** reintroduce starter-scaffolded `Generated/` source folders, Unity Editor codegen postprocessors, MSBuild codegen targets, `codegen.ps1` / `codegen.sh`, CLI tool manifests, or committed generated RPC glue for new starter projects.
- Unity, Unity CN, and Tuanjie starter fixes must make the analyzer/source-generator package work in the Unity compiler pipeline. Do **not** solve Unity compile failures by falling back to checked-in generated client source.
- Generated code must be deterministic, IL2CPP-friendly, and avoid heavy reflection.
- If Unity source generation fails, the fix must target analyzer compatibility, packaging, import metadata, or Unity compiler integration. Treat any fallback to generated source as a product regression.

---

## 7. Starter Rules

`ULinkRPC.Starter` is a user-facing CLI package. Its README should explain how to install, generate, and run starter projects. Internal design tradeoffs belong under `design/starter/**`.

Starter templates must:

- Generate runnable projects for the selected client, transport, and serializer.
- Use analyzer source generation for RPC glue.
- Keep generated namespaces stable and independent of the user-provided project name unless intentionally changed.
- Avoid checked-in generated RPC source.
- Keep Unity-compatible templates aligned with Unity / IL2CPP rules in this document.
- Add focused tests when adding or changing client templates, dependency plans, CLI parsing, or generated file layout.

---

## 8. Testing Rules

### .NET tests

- `tests/ULinkRPC.*.Tests/**` uses xUnit and standard .NET test conventions.
- Prefer focused tests for transport cancellation, session cleanup, source generation output, serializer roundtrips, and starter generated-file contents.
- Source-scan tests that read files from `src/**` must be updated when source files move.

### Unity tests

- NUnit + Unity Test Framework only.
- Live under `samples/**/Assets/Tests/**`.
- EditMode tests default for RPC/runtime logic.
- PlayMode tests only for MonoBehaviour, scene, or platform-specific behavior.
- EditMode tests must live under `Assets/Tests/Editor/**`.
- PlayMode tests must live under `Assets/Tests/PlayMode/**` or an asmdef not restricted to Editor.
- EditMode test asmdefs should reference only the assemblies needed by the test, such as shared contracts, `ULinkRPC.Core`, `ULinkRPC.Client`, and the serializer under test.

EditMode asmdefs must include:

```json
"optionalUnityReferences": ["TestAssemblies"],
"includePlatforms": ["Editor"]
```

### Async Unity tests

Do **not** use `async Task` with `[Test]`. Use `[UnityTest]` + `IEnumerator`:

```csharp
[UnityTest]
public IEnumerator Example_Async_Test()
{
    var task = RunAsync();
    yield return new WaitUntil(() => task.IsCompleted);
    if (task.IsFaulted)
        throw task.Exception!;
}

private async Task RunAsync()
{
    // async test logic
}
```

### Unity assertions

Each Unity test file must include:

```csharp
using NUnitAssert = NUnit.Framework.Assert;
```

Use `NUnitAssert.*` only. Do not use unqualified `Assert.*` or `UnityEngine.Assertions.Assert`.

---

## 9. NuGet Publishing

NuGet publishing is handled by GitHub Actions, not by a local manual push.

Pushing to `main` triggers `.github/workflows/publish-nuget.yml` when any of these paths change:

- `.github/workflows/publish-nuget.yml`
- `Directory.Build.props`
- `src/**`

The workflow restores test and package projects, runs the core/analyzer/serializer/transport test suites, packs every `src/*/*.csproj` project into `artifacts/nuget`, and pushes the packages to nuget.org with `--skip-duplicate`.

The workflow uses the `release` GitHub environment and `NuGet/login@v1` with `secrets.NUGET_USER`. Do not rely on a local `NUGET_API_KEY` for the normal release path.

### Version bumping

Each package version is defined in its `.csproj` via the `<Version>` property.

- Bump versions before pushing to `main` when publishing a new release.
- Do not bump versions for pure refactors, test-only changes, or docs-only changes unless a release specifically requires it.

For local verification only, pack all package projects without publishing:

```powershell
mkdir artifacts/nuget
Get-ChildItem src/*/*.csproj | ForEach-Object {
  dotnet pack $_.FullName --no-restore -c Release -o artifacts/nuget
}
```

---

## 10. Assistant / Maintainer Guardrails

- Follow all rules above.
- Preserve assembly boundaries and package ownership.
- Fix Unity / IL2CPP violations before committing.
- Do not solve source generation failures by committing generated RPC glue.
- Prefer changes that preserve existing tests and add focused coverage for new behavior.
- Keep package README files user-facing; put maintainer rationale in `design/**`.
- Avoid unrelated refactors unless they are explicitly requested or necessary to complete the task safely.
