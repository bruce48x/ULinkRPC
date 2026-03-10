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
- `RpcServerHostBuilder`
- `RpcServerHost`
- `RpcGeneratedServiceBinder`

## Recommended Usage

Use `RpcServerHostBuilder` to compose serializer, transport, generated binders, and security in one place:

```csharp
await RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseMemoryPack()
    .UseTcp(defaultPort: 20000)
    .RunAsync();
```

When the entry assembly contains code-generated `AllServicesBinder`, the builder binds it automatically.

## Low-Level Usage

Pass `ITransport` and `IRpcSerializer` explicitly when you need a manually managed per-connection session:

```csharp
var session = new RpcSession(transport, serializer);
```

Optional transport ownership:

```csharp
await using var session = new RpcSession(transport, serializer, ownsTransport: true);
```

When `ownsTransport` is `true`, disposing the session also disposes the transport.
