+++
title = "Generated RpcClient"
+++

`ULinkRPC.CodeGen` 在 client mode 下会生成一个项目专属的 `RpcClient` facade。它不是固定 NuGet 包里的类型，而是写入当前项目的 generated 目录，例如 Unity 的 `Assets/Scripts/Rpc/Generated/RpcApi.cs` 或 Godot 的 `Scripts/Rpc/Generated/RpcApi.cs`。

## 构造

生成类型位于配置的 generated namespace，默认通常是 `Rpc.Generated`。

```csharp
using Rpc.Generated;

var options = new RpcClientOptions(transport, serializer);
await using var client = new RpcClient(options);
```

如果契约包含 callback contract，生成器还会生成 `RpcClient.RpcCallbackBindings`：

```csharp
var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbacks());

await using var client = new RpcClient(options, callbacks);
```

每个 callback receiver 类型只能注册一次。重复注册同一 callback contract 会抛出 `InvalidOperationException`。

## 生命周期

`RpcClient` 内部持有 `ULinkRPC.Client.RpcClientRuntime`。

- `ConnectAsync(CancellationToken)` 会绑定 callback，然后启动 runtime。
- `DisposeAsync()` 会 dispose runtime、停止后台 loop、让 pending request 失败，并 dispose transport。
- `Disconnected` 事件直接转发 runtime 的断线事件。
- `Options` 返回构造时传入的 `RpcClientOptions`。

同一个 generated client 实例不应作为自动重连对象复用。断线后，应用层应 dispose 当前实例，创建新的 transport、serializer、options 和 generated client。

## `Api` facade

`client.Api` 会懒创建一个 `RpcApi`。`RpcApi` 按服务命名空间第一段生成 group，再按 service interface 生成属性。

例如 `Game.Rpc.Contracts.IPlayerService` 通常会生成类似入口：

```csharp
var player = client.Api.Game.Player;
var reply = await player.LoginAsync(request, ct);
```

具体 group 和属性名称取决于契约命名空间和接口名。生成代码是当前项目的事实源，遇到命名冲突时以生成结果为准。

## Callback base class

有 callback contract 时，生成器会为每个 callback interface 生成一个可继承的 base class。base class 实现 callback interface，并把每个 callback 方法生成为空的 virtual 方法，方便业务代码只 override 需要处理的方法。

Callback handler 的线程模型与 `RpcClientRuntime` 一致：handler 在 runtime 的 push loop 上调用，不会自动切回 Unity、团结或 Godot 主线程。需要更新引擎对象时，请转交给主线程逻辑处理。

## 不要手改 generated 文件

generated client、service proxies、callback binders 和 server binders 都由 `ULinkRPC.CodeGen` 生成。starter 项目会通过 build/editor hook 自动刷新这些文件：

- Server、Godot、Stride3D 在编译前运行 `ULinkRPCGenerateCode`。
- Unity、Unity CN、Tuanjie 通过 Editor asset-change trigger 或 `ULinkRPC/Regenerate RPC Code` 菜单刷新。

不要手动编辑 generated 目录；这些文件会被下一次生成覆盖。`ulinkrpc-starter codegen` 和直接运行 `ULinkRPC.CodeGen` 仍可作为显式修复入口。
