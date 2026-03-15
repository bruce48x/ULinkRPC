# ULinkRPC

ULinkRPC is a strongly-typed bidirectional RPC framework for Unity and .NET.

It is designed for projects that need:

- shared contracts between Unity clients and .NET servers
- typed request/response calls instead of hand-written message ids
- server-to-client push callbacks
- transport switching behind one abstraction
- serializer switching between MemoryPack and JSON

## What It Solves

With ULinkRPC, you define interfaces and DTOs once, generate the glue code, then use typed services on both sides.

Typical stack:

- Unity client
- .NET server
- TCP, WebSocket, or KCP transport
- MemoryPack or JSON serializer

## Packages

Core packages:

- `ULinkRPC.Core`
- `ULinkRPC.Client`
- `ULinkRPC.Server`

Transport packages:

- `ULinkRPC.Transport.Tcp`
- `ULinkRPC.Transport.WebSocket`
- `ULinkRPC.Transport.Kcp`
- `ULinkRPC.Transport.Loopback`

Serializer packages:

- `ULinkRPC.Serializer.MemoryPack`
- `ULinkRPC.Serializer.Json`

Code generation:

- `ULinkRPC.CodeGen`

## Quick Start

1. Define shared contracts with `[RpcService]`, `[RpcMethod]`, and optional callback contracts.
2. Generate client and server glue code with `ULinkRPC.CodeGen`.
3. Configure the same transport and serializer on both sides.
4. Connect the client and call generated typed services.

Example contract:

```csharp
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(1, Callback = typeof(IPlayerCallback))]
    public interface IPlayerService
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask<int> IncrStep();
    }

    [RpcCallback(typeof(IPlayerService))]
    public interface IPlayerCallback
    {
        [RpcPush(1)]
        void OnNotify(string message);
    }
}
```

Example client setup:

```csharp
var options = new RpcClientOptions(
    new WsTransport("ws://127.0.0.1:20000/ws"),
    new JsonRpcSerializer());

var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbackReceiver());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();

var player = client.Api.Game.Player;
var reply = await player.LoginAsync(new LoginRequest
{
    Account = "demo",
    Password = "123456"
});
```

## Samples

- `samples/RpcCall.Json`: minimal WebSocket + JSON sample
- `samples/RpcCall.MemoryPack`: TCP + MemoryPack sample with multiple services
- `samples/RpcCall.Kcp`: KCP transport sample

Build or regenerate a sample from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json
```

Run a sample server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -Run
```

## Documentation

- Getting started tutorial: https://bruce48x.github.io/ULinkRPC/posts/ulinkrpc-getting-started/
- Project docs site: https://bruce48x.github.io/ULinkRPC/

## Repository Layout

- `src/ULinkRPC.*`: runtime, transports, serializers, and code generator
- `samples/`: runnable Unity + .NET samples
- `docs/`: Hugo documentation site for GitHub Pages

## For Contributors

Developer-facing rules, architecture notes, testing constraints, publishing steps, and AI agent instructions live in [CONTRIBUTING.md](/Users/wangql/workspace/ULinkRPC/CONTRIBUTING.md).
