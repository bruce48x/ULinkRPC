# ULinkRPC.Core

Shared abstractions and wire-level contracts for ULinkRPC.

`ULinkRPC.Core` does not depend on concrete serializer or transport implementations.
Use it together with `ULinkRPC.Client` / `ULinkRPC.Server` and optional serializer/transport packages.
The NuGet package also carries ULinkRPC contract analyzers so invalid or duplicate RPC ids surface during normal C# editing/builds.

## Install

```bash
dotnet add package ULinkRPC.Core
```

## Documentation

API reference: https://bruce48x.github.io/ULinkRPC/reference/api/

Design boundary: https://bruce48x.github.io/ULinkRPC/concepts/design-boundary/

## Includes

- RPC attributes: `RpcServiceAttribute`, `RpcMethodAttribute`
- Contract analyzers for non-positive ids and duplicate service/method/push ids
- Transport and serializer abstractions: `ITransport`, `IRpcSerializer`, `IRpcClient`
- Envelopes, status, and exceptions: `RpcRequestEnvelope`, `RpcResponseEnvelope`, `RpcStatus`, `RpcException`, `RpcVoid`
- Envelope codec: `RpcEnvelopeCodec`
- Shared framing/security helpers: `LengthPrefix`, `TransportFrameCodec`, `TransformingTransport`, `TransportSecurityConfig`
