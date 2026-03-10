# ULinkRPC.Client.Unity

Unity-oriented high-level client options for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Client.Unity
```

## Purpose

`ULinkRPC.Client.Unity` gives Unity projects a higher-level entry than `RpcClientBuilder`.

Use `RpcUnityClientOptions` to describe transport + serializer once, then hand that to generated client code:

```csharp
var options = RpcUnityClientOptions.MemoryPackTcp("127.0.0.1", 20000);
await using var connection = await RpcConnection.ConnectAsync(options, playerCallback: this, ct);
```

## Includes

- `RpcUnityClientOptions`
- `RpcUnityTransportKind`
- `RpcUnitySerializerKind`

## Example

```csharp
var options = RpcUnityClientOptions.JsonWebSocket("ws://127.0.0.1:20000/ws");
await using var connection = await RpcConnection.ConnectAsync(options, ct: ct);
```
