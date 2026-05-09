# ULinkRPC.Transport.WebSocket

WebSocket client/server transport implementations for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Transport.WebSocket
```

## Includes

- `WsTransport`
- `WsServerTransport`
- `WsConnectionAcceptor`

## Server Usage

```csharp
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new JsonRpcSerializer());

builder.UseAcceptor(ct => WsConnectionAcceptor.CreateAsync(
    20000,
    "/ws",
    builder.Limits.MaxPendingAcceptedConnections,
    ct));

await builder.RunAsync();
```
