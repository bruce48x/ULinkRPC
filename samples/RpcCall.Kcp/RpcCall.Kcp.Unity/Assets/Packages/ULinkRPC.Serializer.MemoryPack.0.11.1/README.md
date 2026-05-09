# ULinkRPC.Serializer.MemoryPack

MemoryPack based payload serializer for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Serializer.MemoryPack
```

## Usage

```csharp
using ULinkRPC.Serializer.MemoryPack;

var serializer = new MemoryPackRpcSerializer();
```

Use it with `ULinkRPC.Server` by passing the serializer instance explicitly:

```csharp
var builder = RpcServerHostBuilder.Create()
    .UseSerializer(new MemoryPackRpcSerializer())
    .UseAcceptor(new TcpConnectionAcceptor(20000));

await builder.RunAsync();
```
