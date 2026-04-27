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
- `godot`: generate Godot 4.x C# client + binder code.
- `server`: generate server binders + `AllServicesBinder`.

Generated client stubs now depend on `ULinkRPC.Core.IRpcClient`.
Generated client calls use typed descriptors (`RpcMethod<TArg, TResult>` / `RpcPushMethod<TArg>`) instead of passing raw service/method ids.
Generated client output also includes `IRpcClient` extension factories (for example `client.CreatePlayerService()`) so business code does not instantiate generated client types directly.
Generated client output also emits a complete `ULinkRPC.Client.RpcClient` wrapper that owns `RpcClientRuntime`, binds callbacks, and exposes the grouped `Api` facade.
Client generated code namespace is derived from the output directory (for example `Assets/Scripts/Rpc/Generated` or `Scripts/Rpc/Generated` -> `Rpc.Generated`).
Generated files now inherit `using` directives declared by contract sources so referenced types resolve correctly.
Contract parsing is implemented via Roslyn syntax trees for better correctness across C# language forms.
Generated binders reference `ULinkRPC.Core` + `ULinkRPC.Server` and include both `Bind(RpcServiceRegistry, IYourService)` and delegate-based `Bind(...)` overloads. Generated `AllServicesBinder` emits only `BindAll(RpcServiceRegistry registry)`, which reflects over the current assembly to locate concrete service implementations automatically; callback services prefer a single-parameter constructor accepting the callback interface, and fall back to a public parameterless constructor. Per-connection service creation now uses `RpcSession`.

Typical client-side usage now looks like this:

```csharp
var options = new RpcClientOptions(
    new TcpTransport("127.0.0.1", 20000),
    new MemoryPackRpcSerializer())
{
    KeepAlive = new RpcKeepAliveOptions
    {
        Enabled = true,
        Interval = TimeSpan.FromSeconds(15),
        Timeout = TimeSpan.FromSeconds(45)
    }
};

var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbacks());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync(ct);

var player = client.Api.Game.Player;
```

### Options

- `--contracts <path>` Path to contract sources (required).
- `--mode <unity|godot|server>` Generation mode. If omitted, the tool will try to infer it from the current directory.
- `--output <path>` Output directory for generated files.
- `--namespace <ns>` Namespace for generated client code.
- `--server-output <path>` Output directory for server binders.
- `--server-namespace <ns>` Namespace for server binders.

## Default Behavior

- Unity mode defaults output to `Assets/Scripts/Rpc/Generated` under detected Unity project root.
- Godot mode defaults output to `Scripts/Rpc/Generated` under detected Godot project root.
- If Unity project root cannot be detected, pass `--output` explicitly.
- If Godot project root cannot be detected, pass `--output` explicitly.
- Client namespace defaults to value derived from output path unless `--namespace` is provided.
- Server mode defaults output to `Generated` under the detected server project root. If no server project root can be detected, it falls back to `./Generated`.
- Server namespace defaults to `<contracts namespace>.Server.Generated` unless `--server-namespace` is provided.
- If `--mode` is omitted, the tool auto-detects `unity` when the current directory is inside a Unity project, `godot` when the current directory is inside a Godot project, and `server` when the current directory is inside a directory tree that contains a `.csproj` server project.

Paths can be overridden via options.
