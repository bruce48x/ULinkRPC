# ULinkRPC.Transport.Tcp

TCP transport implementations for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Transport.Tcp
```

## Documentation

API reference: https://bruce48x.github.io/ULinkRPC/reference/api/

Design boundary: https://bruce48x.github.io/ULinkRPC/concepts/design-boundary/

## Includes

- `TcpTransport` (client)
- `TcpServerTransport` (server)
- `TcpConnectionAcceptor` (server)

## Server Usage

```csharp
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new MemoryPackRpcSerializer())
    .UseAcceptor(new TcpConnectionAcceptor(20000));

await builder.RunAsync();
```
