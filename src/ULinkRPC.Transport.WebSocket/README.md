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
    .UseSerializer(new JsonRpcSerializer())
    .UseAcceptor(ct => WsConnectionAcceptor.CreateAsync(20000, "/ws", ct));

await builder.RunAsync();
```
