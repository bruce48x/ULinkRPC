# ULinkRPC.Serializer.Json

`System.Text.Json` based payload serializer for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Serializer.Json
```

## Usage

```csharp
using ULinkRPC.Serializer.Json;

var serializer = new JsonRpcSerializer();
```

Use it with `ULinkRPC.Server` by passing the serializer instance explicitly:

```csharp
var builder = RpcServerHostBuilder.Create()
    .UseSerializer(new JsonRpcSerializer())
    .UseAcceptor(ct => WsConnectionAcceptor.CreateAsync(20000, "/ws", ct));

await builder.RunAsync();
```
