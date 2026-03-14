# RpcCall.MemoryPack

MemoryPack-based RPC sample over TCP.

## Structure

- `RpcCall.MemoryPack.Server`: .NET 10 TCP server
- `RpcCall.MemoryPack.Unity`: Unity 2022 LTS client

## Quick Start

Build or regenerate the sample from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.MemoryPack
```

Run the server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.MemoryPack -Run
```

Open `samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity`, load `Assets/Scenes/TcpConnectionTest.unity`, and press Play.

The Unity client opens multiple TCP connections. Each connection uses three independent services:

- `IPlayerService`
- `IInventoryService`
- `IQuestService`

Each service has its own callback contract. After the player login succeeds, the client keeps calling:

- `IPlayerService.IncrStep()`
- `IInventoryService.IncrRevision()`
- `IQuestService.IncrProgress()`

The server pushes updates back through:

- `IPlayerCallback.OnPlayerNotify(...)`
- `IInventoryCallback.OnInventoryNotify(...)`
- `IQuestCallback.OnQuestNotify(...)`

The Unity client entry now uses `RpcClientOptions` plus the generated `RpcClient.Api` facade:

```csharp
var options = new RpcClientOptions(
    new TcpTransport(_endpoint.Host, _endpoint.Port),
    new MemoryPackRpcSerializer());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();

var player = client.Api.Game.Player;
```
