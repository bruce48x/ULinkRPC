# ULinkRPC.Client.Unity

Unity-oriented high-level client options for ULinkRPC.

## Install

```bash
dotnet add package ULinkRPC.Client.Unity
```

## Purpose

`ULinkRPC.Client.Unity` gives Unity projects a higher-level entry than `RpcClientBuilder` without forcing every serializer and transport implementation into one package.

`RpcUnityClientOptions` is intentionally lightweight. Concrete serializer and transport packages contribute extension methods in the same namespace, so a Unity project only references the packages it actually uses.

Use `RpcUnityClientOptions` to describe transport + serializer once, then hand that to generated client code:

```csharp
var options = RpcUnityClientOptions.Create()
    .UseMemoryPack()
    .UseTcp("127.0.0.1", 20000);

await using var connection = await RpcConnection.ConnectAsync(options, playerCallback: this, ct);
```

## Includes

- `RpcUnityClientOptions`

## Example

```csharp
var options = RpcUnityClientOptions.Create()
    .UseJson()
    .UseWebSocket("ws://127.0.0.1:20000/ws");

await using var connection = await RpcConnection.ConnectAsync(options, ct: ct);
```
