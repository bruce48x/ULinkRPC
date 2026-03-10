# ULinkRPC.Client

Client runtime implementation for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Client
```

## Dependencies

- `ULinkRPC.Core`

`ULinkRPC.Client` has no hard dependency on concrete serializer or transport implementations.

## Includes

- `RpcClient`
- `RpcClientBuilder`
- `RpcClientConnection<TApi>`

## Recommended Entry

Use `RpcClientBuilder` as the default client entry:

```csharp
var builder = RpcClientBuilder.Create()
    .UseSerializer(serializer)
    .UseTransport(transport);

await using var client = await builder.ConnectAsync(ct);
```

If you already have a typed facade factory, `ConnectApiAsync(...)` returns a disposable pair of `RpcClient` + typed API:

```csharp
await using var connection = await builder.ConnectApiAsync(
    client => client.CreateRpcApi(),
    ct);
```

Concrete serializer and transport packages also add builder extensions into the `ULinkRPC.Client` namespace, so application code can stay fluent:

```csharp
var builder = RpcClientBuilder.Create()
    .UseMemoryPack()
    .UseTcp("127.0.0.1", 20000);
```
