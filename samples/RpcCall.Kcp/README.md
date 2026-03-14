# RpcCall.Kcp

MemoryPack-based RPC sample over KCP.

## Structure

- `RpcCall.Kcp.Server`: .NET 10 KCP server
- `RpcCall.Kcp.Unity`: Unity 2022 LTS client

## Quick Start

Build or regenerate the sample from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Kcp
```

Run the server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Kcp -Run
```

Open `samples/RpcCall.Kcp/RpcCall.Kcp.Unity`, load `Assets/Scenes/KcpConnectionTest.unity`, and press Play.

The Unity client opens one KCP connection, logs in, then calls `IncrStep()` at a fixed interval. The server creates one `PlayerService` per `RpcSession`, so each client connection keeps its own counter.

The client entry is intentionally minimal:

```csharp
var options = new RpcClientOptions(
    new KcpTransport(_endpoint.Host, _endpoint.Port),
    new MemoryPackRpcSerializer());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();
var player = client.Api.Game.Player;
```
