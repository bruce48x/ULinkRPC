# ULinkRPC.Transport.Kcp

KCP transport primitives for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Transport.Kcp
```

## Includes

- `KcpTransport`
- `KcpListener`
- `KcpAcceptResult`
- `KcpServerTransport`
- `KcpConnectionAcceptor`

## Server Usage

```csharp
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new MemoryPackRpcSerializer())
    .UseAcceptor(new KcpConnectionAcceptor(20000));

await builder.RunAsync();
```
