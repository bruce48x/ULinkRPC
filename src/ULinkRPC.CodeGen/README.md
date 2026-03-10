# ULinkRPC.CodeGen

Command-line code generator for ULinkRPC.

## Install (dotnet tool)

```bash
dotnet tool install -g ULinkRPC.CodeGen
```

## Usage

```bash
ulinkrpc-codegen [options]
```

### Modes

- `unity`: generate Unity client + binder code.
- `server`: generate server binders + `AllServicesBinder`.

Generated client stubs now depend on `ULinkRPC.Core.IRpcClient`.
Generated client calls use typed descriptors (`RpcMethod<TArg, TResult>` / `RpcPushMethod<TArg>`) instead of passing raw service/method ids.
Generated Unity client output also includes `IRpcClient` extension factories (for example `client.CreatePlayerService()`) so business code does not instantiate generated client types directly.
Generated Unity client output includes a mandatory grouped facade `RpcApi`; business code should enter via `client.CreateRpcApi()` and access services through named groups (for example `rpcApi.Game.Player`).
Generated Unity client output now also includes a typed `RpcConnection` host with `ConnectAsync(RpcClientBuilder builder, ...)` and `ConnectAsync(RpcUnityClientOptions options, ...)`, so callers no longer need to manually create `RpcClient`, bind callback receivers, and then call `CreateRpcApi()`.
Unity generated code namespace is derived from Unity output directory (for example `Assets/Scripts/Rpc/RpcGenerated` -> `Rpc.Generated`).
Generated files now inherit `using` directives declared by contract sources so referenced types resolve correctly.
Contract parsing is implemented via Roslyn syntax trees for better correctness across C# language forms.
Generated binders reference `ULinkRPC.Core` + `ULinkRPC.Server` and include both `Bind(RpcServiceRegistry, IYourService)` and delegate-based `Bind(...)` overloads. For `IRpcService<TSelf, TCallback>` services, server binder also emits `Bind(RpcServiceRegistry, Func<TCallback, TSelf>)` to wire callback proxy and implementation together. Generated `AllServicesBinder` includes a convenience overload `BindAll(RpcServiceRegistry registry)` that reflects over the current assembly to locate concrete service implementations automatically; callback services prefer a single-parameter constructor accepting `TCallback`, and fall back to a public parameterless constructor. Per-connection service creation now uses `RpcSession`.

Typical Unity-side usage now looks like this:

```csharp
await using var connection = await RpcConnection.ConnectAsync(
    RpcClientBuilder.Create()
        .UseMemoryPack()
        .UseTcp("127.0.0.1", 20000),
    playerCallback: this,
    ct);

var player = connection.Api.Game.Player;
```

With `ULinkRPC.Client.Unity`, the same entry can be reduced to:

```csharp
await using var connection = await RpcConnection.ConnectAsync(
    RpcUnityClientOptions.MemoryPackTcp("127.0.0.1", 20000),
    playerCallback: this,
    ct);
```

### Options

- `--contracts <path>` Path to contract sources (required).
- `--mode <unity|server>` Generation mode (required).
- `--output <path>` Output directory for generated files.
- `--namespace <ns>` Namespace for generated Unity code.
- `--server-output <path>` Output directory for server binders.
- `--server-namespace <ns>` Namespace for server binders.

## Default Behavior

- Unity mode defaults output to `Assets/Scripts/Rpc/RpcGenerated` under detected Unity project root.
- If Unity project root cannot be detected, pass `--output` explicitly.
- Unity namespace defaults to value derived from output path unless `--namespace` is provided.
- Server mode defaults output to `./Generated`.
- Server namespace defaults to `<contracts namespace>.Server.Generated` unless `--server-namespace` is provided.

Paths can be overridden via options.
