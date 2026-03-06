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

- `auto` (default): detect project type and generate outputs accordingly.
- `unity`: generate Unity client + binder code.
- `server`: generate server binders + `AllServicesBinder`.

Generated client stubs now depend on `ULinkRPC.Core.IRpcClient`.
Generated client calls use typed descriptors (`RpcMethod<TArg, TResult>` / `RpcPushMethod<TArg>`) instead of passing raw service/method ids.
Generated Unity client output also includes `IRpcClient` extension factories (for example `client.CreatePlayerService()`) so business code does not instantiate generated client types directly.
Generated Unity client output includes a mandatory grouped facade `RpcApi`; business code should enter via `client.CreateRpcApi()` and access services through named groups (for example `rpcApi.Game.Player`).
Generated binders reference `ULinkRPC.Core` + `ULinkRPC.Server` and include both `Bind(RpcServer, IYourService)` and delegate-based `Bind(...)` overloads. For `IRpcService<TSelf, TCallback>` services, server binder also emits `Bind(RpcServer, Func<TCallback, TSelf>)` to wire callback proxy and implementation together.

### Options

- `--contracts <path>` Path to contract sources.
- `--output <path>` Output directory for generated clients (Unity).
- `--binder-output <path>` Output directory for generated binders (Unity).
- `--server-output <path>` Output directory for server binders.
- `--server-namespace <ns>` Namespace for server binders.
- `--mode <auto|unity|server>` Force output mode.

## Default Behavior

- Unity project: by default generates to `samples/RpcCall.Json/RpcCall.Json.Unity/Assets/Scripts/Rpc/RpcGenerated` (or matching `RpcCall.MemoryPack` path when detected).
- Server project: by default generates to `samples/RpcCall.Json/RpcCall.Json.Server/RpcCall.Json.Server/Generated` (or matching `RpcCall.MemoryPack` path when detected).

Paths can be overridden via options.
