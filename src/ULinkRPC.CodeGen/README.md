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
Generated Unity client output also emits a complete `ULinkRPC.Client.RpcClient` wrapper that owns `RpcClientRuntime`, binds callbacks, and exposes the grouped `Api` facade.
Unity generated code namespace is derived from Unity output directory (for example `Assets/Scripts/Rpc/RpcGenerated` -> `Rpc.Generated`).
Generated files now inherit `using` directives declared by contract sources so referenced types resolve correctly.
Contract parsing is implemented via Roslyn syntax trees for better correctness across C# language forms.
Generated binders reference `ULinkRPC.Core` + `ULinkRPC.Server` and include both `Bind(RpcServiceRegistry, IYourService)` and delegate-based `Bind(...)` overloads. Generated `AllServicesBinder` includes a convenience overload `BindAll(RpcServiceRegistry registry)` that reflects over the current assembly to locate concrete service implementations automatically; callback services prefer a single-parameter constructor accepting the callback interface, and fall back to a public parameterless constructor. Per-connection service creation now uses `RpcSession`.

Typical Unity-side usage now looks like this:

```csharp
var options = new RpcClientOptions(
    new TcpTransport("127.0.0.1", 20000),
    new MemoryPackRpcSerializer());

var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbacks());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync(ct);

var player = client.Api.Game.Player;
```

### Options

- `--contracts <path>` Path to contract sources (required).
- `--mode <unity|server>` Generation mode. If omitted, the tool will try to infer it from the current directory.
- `--output <path>` Output directory for generated files.
- `--namespace <ns>` Namespace for generated Unity code.
- `--server-output <path>` Output directory for server binders.
- `--server-namespace <ns>` Namespace for server binders.

## Default Behavior

- Unity mode defaults output to `Assets/Scripts/Rpc/RpcGenerated` under detected Unity project root.
- If Unity project root cannot be detected, pass `--output` explicitly.
- Unity namespace defaults to value derived from output path unless `--namespace` is provided.
- Server mode defaults output to `Generated` under the detected server project root. If no server project root can be detected, it falls back to `./Generated`.
- Server namespace defaults to `<contracts namespace>.Server.Generated` unless `--server-namespace` is provided.
- If `--mode` is omitted, the tool auto-detects `unity` when the current directory is inside a Unity project, and `server` when the current directory is inside a directory tree that contains a `.csproj` server project.

Paths can be overridden via options.
