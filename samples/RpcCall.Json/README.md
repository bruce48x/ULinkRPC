# RpcCall.Json

Minimal RPC sample over WebSocket with JSON serialization.

## Structure

- `RpcCall.Json.Server`: .NET 10 WebSocket server
- `RpcCall.Json.Unity`: Unity 2022 LTS client

## Quick Start

Build or regenerate the sample from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json
```

Run the server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -Run
```

Open `samples/RpcCall.Json/RpcCall.Json.Unity`, load `Assets/Scenes/WsConnectionTest.unity`, and press Play.

The Unity client opens multiple WebSocket connections to `ws://127.0.0.1:20000/ws`, logs in, then keeps calling `IncrStep()`. The server maintains one counter per connection and pushes updates through `IPlayerCallback.OnNotify(...)`.

The Unity client entry now uses `RpcClientOptions` plus the generated `RpcClient.Api` facade:

```csharp
var options = new RpcClientOptions(
    new WsTransport(_endpoint.GetWebSocketUrl()),
    new JsonRpcSerializer());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();

var player = client.Api.Game.Player;
```
