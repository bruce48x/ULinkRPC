---
title: 用 ULinkRPC 从零搭一个 Unity + .NET 的双向 RPC
date: 2026-03-15 12:30:00
tags:
  - ulinkrpc
  - unity
  - dotnet
  - rpc
  - websocket
categories:
  - Tutorial
---

如果你想在 Unity 客户端和 .NET 服务端之间建立一套类型安全、可生成代码、支持服务端主动推送的通信层，`ULinkRPC` 这套结构就是为这个目标准备的。

这篇文章不讲抽象概念，直接带你走通一个最小可运行版本：

- Unity 作为客户端
- .NET 作为服务端
- 使用 WebSocket 传输
- 使用 JSON 序列化
- 支持客户端调用服务端
- 支持服务端反向推送消息给客户端

文中的代码和目录都对应当前仓库里的 `samples/RpcCall.Json` 示例，你可以一边看一边对照项目。

## 先理解 ULinkRPC 的核心思路

`ULinkRPC` 的关键点只有三个：

1. 先定义共享契约
2. 再用代码生成器产出客户端桩代码和服务端绑定代码
3. 最后把传输层和序列化器接进去

也就是说，你平时主要维护的是接口和 DTO，不是手写一堆字符串协议，也不是自己拼 method id。

在这个仓库里，最重要的事实是：

- 契约是单一事实来源
- Unity 和服务端共用同一份 contracts
- 客户端调用入口和服务端 binder 都是生成的

这和很多“先写 socket 再写协议再手工分发消息”的做法不同，维护成本会低很多。

## 项目结构

这篇入门教程对应的示例在：

```text
samples/RpcCall.Json/
├── RpcCall.Json.Server/
└── RpcCall.Json.Unity/
```

其中：

- `RpcCall.Json.Unity/Packages/com.samples.contracts` 放共享契约
- `RpcCall.Json.Unity/Assets/Scripts/Rpc/RpcGenerated` 放生成出来的 Unity 客户端代码
- `RpcCall.Json.Server/Generated` 放生成出来的服务端 binder 和 callback proxy

也就是说，真正需要你长期维护的业务入口，首先是 contracts。

## 第一步：定义共享契约

先看这个示例里的契约定义：

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

这个定义表达了三层信息：

- `IPlayerService` 是一个 RPC 服务，服务 id 是 `1`
- `LoginAsync` 和 `IncrStep` 是两个远程调用方法
- `IPlayerCallback` 是服务端推送给客户端时使用的回调契约

这套设计最实用的地方在于，双向通信不是“额外机制”，而是契约本身的一部分。

再看 DTO：

```csharp
namespace Game.Rpc.Contracts
{
    public class LoginRequest
    {
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class LoginReply
    {
        public int Code { get; set; }
        public string Token { get; set; } = "";
    }
}
```

这就是最典型的做法：接口负责行为，DTO 负责载荷。

## 第二步：生成客户端和服务端代码

定义完 contracts 之后，不是自己手写网络封装，而是跑代码生成器。

仓库里已经准备了脚本：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json
```

这一步会做几件事：

- 编译 `ULinkRPC.CodeGen`
- 从 `com.samples.contracts` 扫描 `[RpcService]` 和 `[RpcMethod]`
- 生成 Unity 客户端桩代码
- 生成服务端 binder / callback proxy
- 构建当前 sample 的服务端项目

如果你只关心生成器本体，底层调用本质上就是：

```bash
dotnet run --project src/ULinkRPC.CodeGen/ULinkRPC.CodeGen.csproj --
```

生成之后，客户端会拿到一个统一入口 `RpcClient.Api`，服务端会拿到类似 `PlayerServiceBinder` 这样的绑定代码。

这一步非常关键，因为它把“接口定义”和“网络调用细节”彻底隔离开了。

## 第三步：实现服务端

在这个示例里，服务端入口非常短：

```csharp
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.WebSocket;

await RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseJson()
    .UseWebSocket(defaultPort: 20000, path: "/ws")
    .RunAsync();
```

这段代码表达得很直接：

- 用 `ULinkRPC.Server` 启服务
- 序列化选 `JsonRpcSerializer`
- 传输层选 WebSocket
- 默认监听 `20000`
- 路径是 `/ws`

你可以把它理解成三块拼装：

- Server runtime
- Serializer
- Transport

只要客户端和服务端用的是同一套 serializer 和 transport 约定，RPC 层就能工作。

### 服务实现怎么写

具体业务实现也很普通，和写本地接口实现没什么本质区别：

```csharp
using Game.Rpc.Contracts;

namespace RpcCall.Json.Server.Services;

public class PlayerService : IPlayerService
{
    private readonly IPlayerCallback _callback;
    private int _step;

    public PlayerService(IPlayerCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        _callback.OnNotify($"Welcome {req.Account}, login request accepted.");

        return new ValueTask<LoginReply>(new LoginReply
        {
            Code = 0,
            Token = $"token-{req.Account}-{Guid.NewGuid():N}"
        });
    }

    public ValueTask<int> IncrStep()
    {
        _step++;
        _callback.OnNotify($"IncrStep => {_step}");
        return new ValueTask<int>(_step);
    }
}
```

这里有两个重点：

- `LoginAsync` 是标准请求响应式 RPC
- `_callback.OnNotify(...)` 是服务端主动推送给客户端

所以对业务开发者来说，推送不是额外维护一个 socket channel，而是像调用接口一样使用 callback。

### binder 帮你做了什么

生成后的 `PlayerServiceBinder` 会把接口方法注册到 `RpcServiceRegistry`，并负责：

- 把请求 payload 反序列化成 `LoginRequest`
- 调用你的 `IPlayerService`
- 把返回值重新序列化成 `RpcResponseEnvelope`
- 为当前连接创建对应的 callback proxy

这意味着你业务层基本不需要碰：

- service id
- method id
- payload 编解码
- request/response 封包

这些重复劳动都交给生成器和 runtime 处理。

## 第四步：Unity 客户端接入

Unity 侧先创建 `RpcClientOptions`：

```csharp
using ULinkRPC.Client;
using UnityEngine;

namespace Rpc.Testing
{
    public sealed class RpcConnectionTester : RpcConnectionTesterBase
    {
        [SerializeField] private RpcEndpointSettings _endpoint =
            RpcEndpointSettings.CreateWebSocket("127.0.0.1", 20000, "/ws");

        protected override RpcClientOptions CreateClientOptions()
        {
            return new RpcClientOptions(
                new global::ULinkRPC.Transport.WebSocket.WsTransport(_endpoint.GetWebSocketUrl()),
                new global::ULinkRPC.Serializer.Json.JsonRpcSerializer());
        }
    }
}
```

这里客户端和服务端必须保持一致：

- 都是 WebSocket
- 都是 JSON

如果两边 serializer 不一致，或者路径端口不一致，请求就根本通不了。

### 客户端怎么拿到业务服务

生成代码后，客户端不是自己 new `PlayerServiceClient`，而是通过统一入口访问：

```csharp
await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();

var player = client.Api.Game.Player;
```

这里的体验很像调用本地业务 facade：

- `client.Api` 是生成出来的总入口
- `Game` 是按业务分组组织的 service group
- `Player` 是最终的 typed service proxy

然后你就可以像本地接口一样发起调用：

```csharp
var reply = await player.LoginAsync(new LoginRequest
{
    Account = "demo",
    Password = "123456"
});

var step = await player.IncrStep();
```

这正是类型安全 RPC 真正有价值的地方：

- 方法名有编译期约束
- 参数和返回值有明确类型
- 不需要手动处理 opcode
- 不需要拼 JSON 包体

## 第五步：接收服务端推送

既然契约里定义了 `IPlayerCallback`，客户端就要提供对应实现。

生成代码里已经给了一个可继承的基类：

```csharp
public abstract class PlayerCallbackBase : IPlayerCallback
{
    public virtual void OnNotify(string message)
    {
    }
}
```

你只要继承它并覆盖回调即可：

```csharp
public sealed class PlayerCallbackReceiver : RpcClient.PlayerCallbackBase
{
    public override void OnNotify(string message)
    {
        Debug.Log($"[Push] {message}");
    }
}
```

然后在创建客户端时注册进去：

```csharp
var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbackReceiver());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();
```

从这里开始，只要服务端调用 `_callback.OnNotify(...)`，客户端就会收到推送。

这套模型比自己维护“请求通道 + 推送通道 + 消息路由器”更简单，因为整个反向推送已经被折叠成 typed callback contract。

## 第六步：跑起来验证

在仓库根目录执行：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -Run
```

然后：

1. 用 Unity 打开 `samples/RpcCall.Json/RpcCall.Json.Unity`
2. 打开场景 `Assets/Scenes/WsConnectionTest.unity`
3. 点击 Play

这个示例运行后，你会看到一条完整链路：

- Unity 连接到 `ws://127.0.0.1:20000/ws`
- 客户端调用 `LoginAsync`
- 服务端返回 `LoginReply`
- 服务端再通过 `OnNotify` 主动推送消息
- 客户端继续调用 `IncrStep`
- 服务端维护每个连接自己的计数状态

也就是说，它不是一个只能 request/response 的单向 RPC，而是一个天然支持双向事件回流的结构。

## 为什么这套方式适合 Unity

如果你做过 Unity 网络层，很容易踩到几个坑：

- 协议字段手写太多，改一次接口就全链路手改
- WebSocket / TCP 切换时，业务层被迫一起改
- 推送和请求分成两套模型，维护成本翻倍
- IL2CPP 环境下，一些反射或动态生成方案不稳

`ULinkRPC` 这套设计本质上是在解决这些问题：

- 契约先行，接口就是协议
- transport 可切换，业务代码不依赖 TCP / WebSocket / KCP
- serializer 可切换，JSON 和 MemoryPack 都能接
- 生成代码替你屏蔽样板逻辑
- callback contract 让双向通信保持强类型

特别是 Unity + .NET 混合开发时，这种“shared contracts + generated stubs + typed callbacks”的结构，比裸 socket 或自定义消息总线更容易持续演进。

## 如果你要开始自己的项目，建议这样落地

第一版不要一上来就做很复杂的业务，先照这个顺序走：

1. 新建一份 contracts 仓库或目录
2. 定义一个最小服务，例如 `IPlayerService`
3. 定义一个最小 callback，例如 `OnNotify`
4. 跑 codegen，先把客户端和服务端代码生成出来
5. 先用 JSON + WebSocket 跑通
6. 确认流程稳定后，再切到 MemoryPack 或 TCP/KCP

原因很简单：

- JSON 更适合排查问题
- WebSocket 对本地调试更直观
- 先验证契约和调用模型，再优化性能，成本最低

## 下一步可以看什么

如果这篇文章帮你跑通了第一条链路，下一步建议继续看仓库里的两个方向：

- `RpcCall.MemoryPack`：看多服务、多回调、TCP 场景
- `RpcCall.Kcp`：看 KCP 传输版本

等你把最小链路跑通之后，再考虑这些增强项：

- 使用 `MemoryPackRpcSerializer`
- 切换到 `TcpTransport` 或 `Kcp`
- 开启压缩和加密
- 拆分更细的业务 service group

## 总结

你可以把 `ULinkRPC` 理解成一套面向 Unity 和 .NET 的“契约优先双向 RPC”方案。

它最值得用的不是“可以远程调用”，而是它把下面这些东西组合成了一套稳定工作流：

- 共享契约
- 类型安全接口
- 自动生成客户端和服务端胶水代码
- 可切换 transport
- 可切换 serializer
- 服务端主动推送

如果你准备在 Unity 里做一套长期维护的客户端通信层，这种模式会比手写消息协议轻松很多。

下一篇可以继续写 `MemoryPack + TCP` 版本，把性能和多服务组织方式也补上。
