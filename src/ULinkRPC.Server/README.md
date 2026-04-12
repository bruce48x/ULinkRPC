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
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new MemoryPackRpcSerializer())
    .UseKeepAlive(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45))
    .UseAcceptor(new TcpConnectionAcceptor(20000));

await builder.RunAsync();
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

## KeepAlive

`RpcServerHostBuilder.UseKeepAlive(...)` enables connection-level idle timeout handling for accepted sessions.

- The server automatically replies to client keepalive pings with pong.
- When enabled on the host, each `RpcSession` also tracks idle time and disconnects sessions that remain inactive longer than the configured timeout.

## Authentication And Authorization Boundary

`ULinkRPC.Server` is intentionally focused on RPC session management, transport integration, request dispatch, and connection-level concerns such as framing, keepalive, and transport security.

Request-level authorization is not built into the server runtime by design.

- Transport security belongs in the RPC layer because it protects the connection itself.
- Authentication may be integrated at the application boundary, but the concrete identity model is application-specific.
- Authorization is expected to live in an upper application/business layer because access rules depend on domain concepts such as users, roles, tenants, resources, and policies.

This boundary is intentional: the RPC runtime should carry calls correctly and safely, but it should not hard-code business authorization semantics into the communication layer.

Future evolution may add authentication / authorization extension points, but the core runtime is not intended to become a built-in policy engine.
