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

- `RpcServiceRegistry`
- `RpcSession`

Pass `ITransport` and `IRpcSerializer` explicitly:

```csharp
var session = new RpcSession(transport, serializer);
```

Optional transport ownership:

```csharp
await using var session = new RpcSession(transport, serializer, ownsTransport: true);
```

When `ownsTransport` is `true`, disposing the session also disposes the transport.
