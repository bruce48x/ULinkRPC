# ULinkRPC

[English](./README.md)

ULinkRPC 是一个面向 Unity 和 .NET 的强类型双向 RPC 框架。

它特别适合这类项目：

- Unity 客户端和 .NET 服务端需要共享同一套契约
- 希望直接写类型安全的请求/响应调用，而不是手搓消息 ID
- 需要服务端主动向客户端推送回调
- 想在统一抽象下灵活切换传输层
- 想在 MemoryPack 和 JSON 序列化之间自由切换

## 它解决了什么问题

用 ULinkRPC，你只需要定义一次接口和 DTO，生成胶水代码之后，客户端和服务端两边都可以直接使用强类型服务。

一个常见的组合是：

- Unity 客户端
- .NET 服务端
- TCP、WebSocket 或 KCP 传输层
- MemoryPack 或 JSON 序列化

## 设计边界

ULinkRPC 目前刻意把职责边界收在“通信框架”这一层：

- 传输层、安全帧处理、会话管理、请求分发、keepalive，属于框架职责。
- 身份认证可以在应用接入层完成，但具体身份模型不由框架内建。
- 请求级别授权不内建在 ULinkRPC 中，这是刻意的设计选择，而不是缺失实现。

原因很简单：授权规则天然依赖业务语义，例如用户、角色、租户、资源归属、策略组合等。这些规则更适合放在上层业务框架或应用层统一处理，而不是固化进底层 RPC 通信层。

后续如果需要，ULinkRPC 可以增加认证 / 授权扩展点，但核心运行时不会直接演变成内建的权限策略引擎。

## 快速开始

1. 用 `[RpcService]`、`[RpcMethod]`，以及可选的回调契约定义共享接口。
2. 使用 `ULinkRPC.CodeGen` 生成客户端和服务端需要的胶水代码。
3. 在两端配置一致的传输层和序列化器。
4. 连接客户端，然后直接调用生成好的强类型服务。

契约示例：

```csharp
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Game.Rpc.Contracts
{
    [RpcService(1, Callback = typeof(IPlayerCallback))]
    public interface IPlayerService
    {
        [RpcMethod(1)]
        ValueTask<LoginReply> LoginAsync(LoginRequest req);

        [RpcMethod(2)]
        ValueTask<int> IncrStep();
    }

    [RpcCallback(typeof(IPlayerService))]
    public interface IPlayerCallback
    {
        [RpcPush(1)]
        void OnNotify(string message);
    }
}
```

客户端初始化示例：

```csharp
var options = new RpcClientOptions(
    new WsTransport("ws://127.0.0.1:20000/ws"),
    new JsonRpcSerializer());

var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbackReceiver());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();

var player = client.Api.Game.Player;
var reply = await player.LoginAsync(new LoginRequest
{
    Account = "demo",
    Password = "123456"
});
```

## Starter 教程

如果你是从零开始新项目，建议先看这篇：

- [基于 `ULinkRPC.Starter` 的入门教程](https://bruce48x.github.io/ULinkRPC/posts/ulinkrpc-getting-started/)

`ULinkRPC.Starter` 作为全局工具安装时，最低要求是 .NET SDK 10.0。

这篇会覆盖：

- 如何生成一份可运行的 Unity + .NET 项目
- 如何启动默认服务端和 Unity 客户端
- 什么情况下需要重新运行 `ULinkRPC.CodeGen`
- `Shared`、`Server`、`Client` 三层分别负责什么

## 示例

- `samples/RpcCall.Json`：WebSocket + JSON 示例
- `samples/RpcCall.MemoryPack`：TCP + MemoryPack 示例，包含多个服务
- `samples/RpcCall.Kcp`：最精简的示例，基于 KCP + MemoryPack

在仓库根目录构建或重新生成示例：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json
```

```bash
./scripts/sample.sh --sample RpcCall.Json
```

运行示例服务端：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -Run
```

```bash
./scripts/sample.sh --sample RpcCall.Json --run
```

## 包组成

核心包：

- `ULinkRPC.Core`
- `ULinkRPC.Client`
- `ULinkRPC.Server`

传输层包：

- `ULinkRPC.Transport.Tcp`
- `ULinkRPC.Transport.WebSocket`
- `ULinkRPC.Transport.Kcp`
- `ULinkRPC.Transport.Loopback`

序列化包：

- `ULinkRPC.Serializer.MemoryPack`
- `ULinkRPC.Serializer.Json`

代码生成：

- `ULinkRPC.CodeGen`

## 文档

- 入门教程：https://bruce48x.github.io/ULinkRPC/posts/ulinkrpc-getting-started/
- 架构深入解析：https://bruce48x.github.io/ULinkRPC/posts/ulinkrpc-design-and-implementation/
- 项目文档站点：https://bruce48x.github.io/ULinkRPC/

## 仓库结构

- `src/ULinkRPC.*`：运行时、传输层、序列化器和代码生成器
- `samples/`：可直接运行的 Unity + .NET 示例
- `docs/`：用于 GitHub Pages 的 Hugo 文档站点

## 给贡献者

面向开发者的规则、架构说明、测试约束、发布步骤，以及 AI Agent 相关说明，都放在 [CONTRIBUTING.md](./CONTRIBUTING.md) 中。
