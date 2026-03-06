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

Pass `ITransport` and `IRpcSerializer` explicitly:

```csharp
var client = new RpcClient(transport, serializer);
```
