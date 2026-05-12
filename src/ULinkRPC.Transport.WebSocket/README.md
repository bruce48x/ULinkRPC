# ULinkRPC.Transport.WebSocket

WebSocket client/server transport implementations for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Transport.WebSocket
```

## Documentation

API reference: https://bruce48x.github.io/ULinkRPC/reference/api/

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
