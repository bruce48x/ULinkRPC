# Contributing Guide

This document is for contributors, maintainers, and AI agents working on the repository itself.

User-facing introduction, samples, and tutorial links live in [README.md](./README.md).

Mandatory engineering rules for this repo. Target is **Unity 2022 LTS** with **iOS / IL2CPP / HybridCLR**; stability and platform compatibility take priority.

---

## 1. Architecture & Sources

### Assemblies
- `Game.Rpc.Contracts`: RPC interfaces, DTOs, attributes (`RpcService`, `RpcMethod`); **no UnityEngine**.
- `ULinkRPC.Core`: transport abstraction, framing/security helpers, envelopes, serializer abstractions.
- `ULinkRPC.Client`: RPC client core.
- `ULinkRPC.Server`: RPC server core.
- `ULinkRPC.Transport.Tcp` / `ULinkRPC.Transport.WebSocket` / `ULinkRPC.Transport.Kcp` / `ULinkRPC.Transport.Loopback`: transport implementations.
- `ULinkRPC.Serializer.MemoryPack` / `ULinkRPC.Serializer.Json`: payload serializer implementations.
- Unity test assemblies: `*.Tests.Editor` / `*.Tests.PlayMode` (NUnit + Unity Test Framework)
- Server-side .NET sample projects live under `samples/RpcCall.<Variant>/RpcCall.<Variant>.Server/**`.
- Core .NET test projects live under `tests/ULinkRPC.*.Tests/**`.

No circular dependencies between assemblies.

### Contracts are the single source of truth
- Do NOT duplicate sample contracts under `samples/RpcCall.<Variant>/RpcCall.<Variant>.Server/**`.
- Sample contracts live in `samples/RpcCall.<Variant>/RpcCall.<Variant>.Unity/Packages/com.samples.contracts/`.
- Sample server projects must include contracts by linking sources from the matching Unity project:
  ```xml
  <Compile Include="..\..\RpcCall.Json.Unity\Packages\com.samples.contracts\**\*.cs" />
  ```
- If server changes require a contract update, edit the package, not server-local copies.

---

## 2. Unity / IL2CPP Constraints

Allowed Unity-compatible runtime dependencies:
- `System.Threading.Channels` is intentionally used by ULinkRPC runtime packages and Unity samples. Keep it as an explicit package dependency when runtime code needs async producer/consumer queues.
- `System.IO.Pipelines` may appear through transport or serializer package dependency chains. Do not treat its presence alone as a Unity / IL2CPP violation; validate the affected package path on Unity 2022 LTS, iOS, IL2CPP, and HybridCLR before expanding support claims.

Forbidden in Unity client code (including tests):
- `System.Reflection.Emit`
- Runtime code generation
- APIs relying on JIT-only behavior

If an API is common on server-side .NET but not explicitly supported by Unity IL2CPP, treat it as **unsafe**.

### C# version
Unity client code and shared contracts must compile with **C# 9.0** (no C# 10+ syntax).

---

## 3. Async & ValueTask Rules

Allowed `ValueTask` patterns only:
- `return default;` for `ValueTask`
- `return new ValueTask<T>(value);`
- `async` methods returning `ValueTask<T>` with `return value;`

Forbidden:
- `ValueTask.CompletedTask`
- `ValueTask.FromResult(...)`

---

## 4. Transport & Networking

- All transports must implement `ITransport`.
- RPC code must not depend on a specific transport.
- Transport implementations must be cancellation-safe, avoid background thread leaks, and be explicit about disconnect behavior.
- Prefer **LoopbackTransport** for local testing.

---

## 5. Testing

### Unity tests
- NUnit + Unity Test Framework only.
- Live under `samples/RpcCall.<Variant>/RpcCall.<Variant>.Unity/Assets/Tests/**`.
- EditMode tests default for RPC/runtime logic; PlayMode only for MonoBehaviour/scene/platform-specific behavior.
- EditMode asmdefs must include:
  ```json
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": ["Editor"]
  ```
- EditMode tests must live under `samples/RpcCall.<Variant>/RpcCall.<Variant>.Unity/Assets/Tests/Editor/**`.
- PlayMode tests must live under `samples/RpcCall.<Variant>/RpcCall.<Variant>.Unity/Assets/Tests/PlayMode/**` (or an asmdef not restricted to Editor).
- EditMode test asmdefs should reference only the assemblies needed by the test, such as `Game.Rpc.Contracts`, `ULinkRPC.Core`, `ULinkRPC.Client`, and the serializer under test.

### Server tests
- `tests/ULinkRPC.*.Tests/**` uses xUnit and standard .NET test conventions.

### Async Unity tests (critical)
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

### Assertions (Unity tests)
Each Unity test file must include:
```csharp
using NUnitAssert = NUnit.Framework.Assert;
```
And use `NUnitAssert.*` only (no unqualified `Assert.*` or `UnityEngine.Assertions.Assert`).

---

## 6. Code Style & Safety

- Prefer explicit lifetimes (`DisposeAsync`, `StopAsync`).
- Clean up background loops in tests.
- Avoid implicit global state.
- Favor clarity over micro-optimizations in shared infrastructure.

---

## 7. Source Generation

RPC client facades, service clients, callback binders, server binders, callback proxies, and binder discovery attributes are generated by `ULinkRPC.Analyzers` during compilation.

- New starter projects must use `ULinkRPC.Analyzers` source generation as the normal RPC glue path.
- Do **not** reintroduce starter-scaffolded `Generated/` source folders, Unity Editor codegen postprocessors, MSBuild codegen targets, `codegen.ps1` / `codegen.sh`, CLI tool manifests, or committed generated RPC glue for new starter projects.
- Unity, Unity CN, and Tuanjie starter fixes must make the analyzer/source-generator package work in the Unity compiler pipeline. Do **not** solve Unity compile failures by falling back to checked-in generated client source.
- Generated code must be deterministic, IL2CPP-friendly, and avoid heavy reflection.
- If Unity source generation fails, the fix must target analyzer compatibility, packaging, import metadata, or Unity compiler integration. Treat any fallback to generated source as a product regression.

---

## 8. NuGet Publishing

NuGet publishing is handled by GitHub Actions, not by a local manual push.

Pushing to `main` triggers `.github/workflows/publish-nuget.yml` when any of these paths change:

- `.github/workflows/publish-nuget.yml`
- `Directory.Build.props`
- `src/**`

The workflow restores test and package projects, runs the core/analyzer/serializer/transport test suites, packs every `src/*/*.csproj` project into `artifacts/nuget`, and pushes the packages to nuget.org with `--skip-duplicate`.

The workflow uses the `release` GitHub environment and `NuGet/login@v1` with `secrets.NUGET_USER`; do not rely on a local `NUGET_API_KEY` for the normal release path.

### Version Bumping
Each package version is defined in its `.csproj` via the `<Version>` property. Bump versions before pushing to `main` when publishing a new release.

For local verification only, you can pack all package projects without publishing:

```powershell
mkdir artifacts/nuget
Get-ChildItem src/*/*.csproj | ForEach-Object {
  dotnet pack $_.FullName --no-restore -c Release -o artifacts/nuget
}
```

---

## 9. AI / Code Assistant Notes

- Follow all rules above.
- Fix any Unity / IL2CPP violations before committing.
- Prefer changes that preserve assembly boundaries and test coverage.
