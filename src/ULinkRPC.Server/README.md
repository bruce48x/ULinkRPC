# ULinkRPC.Server

Server runtime implementation for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Server
```

## Dependencies

- `ULinkRPC.Core`

`ULinkRPC.Server` has no hard dependency on concrete serializer or transport implementations.

## Includes

- `RpcServer`

Pass `ITransport` and `IRpcSerializer` explicitly:

```csharp
var server = new RpcServer(transport, serializer);
```

Optional transport ownership:

```csharp
await using var server = new RpcServer(transport, serializer, ownsTransport: true);
```

When `ownsTransport` is `true`, disposing the server also disposes the transport.
