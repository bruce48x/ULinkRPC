---
title: 用 ULinkRPC 从零搭一个 Unity 和 .NET 双向通信项目
date: 2026-03-15T12:30:00+08:00
tags:
  - ulinkrpc
  - unity
  - dotnet
  - rpc
  - websocket
categories:
  - Tutorial
---

如果你想让 Unity 客户端和 .NET 服务端共享一套契约代码，而且还能双向通信，不想自己手搓协议、拼 JSON、维护一堆消息号，那 `ULinkRPC` 就是干这个的。

这篇文章不讲太虚的概念，直接带你走一遍最常见的一条链路：

- Unity 当客户端
- .NET 当服务端
- WebSocket 传输
- JSON 序列化
- 客户端调服务端
- 服务端再主动推消息给客户端

仓库里最精简的 sample 是 `samples/RpcCall.Kcp`。不过这篇入门教程还是用 `samples/RpcCall.Json` 来讲，因为 WebSocket + JSON 更容易看懂，也更方便排错。

## 先说结论：ULinkRPC 到底在帮你做什么

你可以把 ULinkRPC 理解成三件事：

1. 先写共享契约，也就是 C# 接口和 DTO
2. 再用 CodeGen 生成客户端和服务端的胶水代码
3. 最后选一个传输层和一个序列化器接进去

也就是说，你平时主要维护的是接口和数据结构，而不是一堆手写的网络封包代码。

它最核心的价值就一句话：

**契约是唯一真相来源，前后端都围着同一份契约转。**

## 先看目录结构

这篇教程对应的示例在：

```text
samples/RpcCall.Json/
├── RpcCall.Json.Server/
└── RpcCall.Json.Unity/
```

你只要先记住这几个位置：

- `RpcCall.Json.Unity/Packages/com.samples.contracts`
  放共享契约
- `RpcCall.Json.Unity/Assets/Scripts/Rpc/RpcGenerated`
  放生成出来的 Unity 客户端代码
- `RpcCall.Json.Server/Generated`
  放生成出来的服务端 binder 和 callback proxy

所以真正长期要维护的，首先是 contracts，不是 generated 目录。

## 第一步：先定义共享契约

先看示例里的服务契约：

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

这里面其实只表达了三件事：

- `IPlayerService` 是一个 RPC 服务，id 是 `1`
- `LoginAsync` 和 `IncrStep` 是客户端可以调用的方法
- `IPlayerCallback` 是服务端反过来推给客户端时用的回调接口

这套写法很重要的一点是：  
**双向通信不是额外再补一套机制，而是直接写进契约里。**

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

很普通，没什么花样。接口负责行为，DTO 负责传输数据，就这么简单。

## 第二步：跑代码生成

契约写完之后，不是自己手搓网络调用，而是直接跑生成器。

仓库里已经有脚本了：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json
```

这条命令会做几件事：

- 编译 `ULinkRPC.CodeGen`
- 扫描 contracts
- 生成 Unity 客户端代码
- 生成服务端 binder / callback proxy
- 顺手把这个 sample 的服务端也编一下

如果你只是想单独装工具，也可以：

```sh
dotnet tool install --global ULinkRPC.CodeGen
```

然后在项目目录里自己跑。

生成之后，客户端会拿到一个统一入口 `RpcClient.Api`，服务端会拿到类似 `PlayerServiceBinder` 这样的绑定代码。你基本不用去关心 service id、method id、payload 封包这些底层细节。

这一步的意义就是：

**你维护契约，生成器负责把契约翻译成网络调用代码。**

## 第三步：把服务端跑起来

这个示例的服务端入口其实很短：

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

这段代码翻译成人话就是：

- 用 ULinkRPC 的服务端 runtime
- 序列化用 JSON
- 传输层用 WebSocket
- 默认监听 `20000`
- WebSocket 路径是 `/ws`

所以服务端本质上就是三块拼起来：

- Server runtime
- Serializer
- Transport

客户端和服务端只要这两边对得上，RPC 就能通。

### 服务实现怎么写

服务实现本身没有什么特别玄学的地方，就是普通的接口实现类：

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

这里有两个点值得注意：

- `LoginAsync` 是标准的请求/响应
- `_callback.OnNotify(...)` 是服务端主动推客户端

也就是说，在业务代码里，服务端推送不是“另开一条通道”的概念，而是直接调 callback。

### binder 到底帮你干了什么

生成出来的 `PlayerServiceBinder` 会负责：

- 把请求反序列化成 `LoginRequest`
- 调用你的 `IPlayerService`
- 把返回值再序列化成响应
- 给当前连接创建 callback proxy

所以业务层基本不需要再碰这些东西：

- service id
- method id
- request/response 封包
- payload 编解码

这些脏活都交给 runtime 和 codegen 了。

## 第四步：Unity 客户端接入

Unity 这边先创建 `RpcClientOptions`：

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
                new ULinkRPC.Transport.WebSocket.WsTransport(_endpoint.GetWebSocketUrl()),
                new ULinkRPC.Serializer.Json.JsonRpcSerializer());
        }
    }
}
```

这里最关键的是两边要一致：

- 服务端是 WebSocket，客户端也得是 WebSocket
- 服务端是 JSON，客户端也得是 JSON

如果两边 serializer 或 transport 不一致，那肯定通不了。

### 客户端怎么调服务

生成代码之后，客户端可以这样用：

```csharp
await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();

var player = client.Api.Game.Player;
```

这里的感觉很像在调本地代码：

- `client.Api` 是总入口
- `Game` 是 service group
- `Player` 是具体服务代理

然后你就可以正常发调用：

```csharp
var reply = await player.LoginAsync(new LoginRequest
{
    Account = "demo",
    Password = "123456"
});

var step = await player.IncrStep();
```

这也是 typed RPC 最实用的地方：

- 方法名有编译期检查
- 参数和返回值有明确类型
- 不需要自己维护 opcode
- 不需要手拼 JSON 包

## 第五步：接服务端推送

既然契约里定义了 `IPlayerCallback`，客户端就要给一个实现。

生成代码里已经给了一个可继承的基类：

```csharp
public abstract class PlayerCallbackBase : IPlayerCallback
{
    public virtual void OnNotify(string message)
    {
    }
}
```

你可以直接继承它：

```csharp
public sealed class PlayerCallbackReceiver : RpcClient.PlayerCallbackBase
{
    public override void OnNotify(string message)
    {
        Debug.Log($"[Push] {message}");
    }
}
```

然后注册进去：

```csharp
var callbacks = new RpcClient.RpcCallbackBindings();
callbacks.Add(new PlayerCallbackReceiver());

await using var client = new RpcClient(options, callbacks);
await client.ConnectAsync();
```

从这以后，只要服务端调 `_callback.OnNotify(...)`，客户端就会收到推送。

所以这套模型的好处很直接：  
你不用再自己维护“请求一套，推送一套”的两层逻辑。

## 第六步：把整个示例跑起来

在仓库根目录执行：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -Run
```

然后：

1. 用 Unity 打开 `samples/RpcCall.Json/RpcCall.Json.Unity`
2. 打开场景 `Assets/Scenes/WsConnectionTest.unity`
3. 点击 Play

正常的话，你会看到这样一条完整链路：

- Unity 连上 `ws://127.0.0.1:20000/ws`
- 客户端调用 `LoginAsync`
- 服务端返回 `LoginReply`
- 服务端再通过 `OnNotify` 推一条消息回来
- 客户端继续调用 `IncrStep`
- 服务端按连接维护自己的计数

这也说明它不只是单向的 request/response RPC，而是一套天然支持双向流动的通信结构。

## 这套方式为什么适合 Unity

如果你做过 Unity 网络层，应该多少都踩过这些坑：

- 协议字段手写太多，改一次接口就到处跟着改
- TCP、WebSocket、KCP 一切换，业务代码也被迫一起动
- 请求和推送分成两套模型，维护起来很烦
- IL2CPP 环境下，一些动态方案不太稳

ULinkRPC 的思路，本质上就是在解决这些问题：

- 契约优先，代码就是协议
- transport 可切换，业务代码不直接依赖 TCP / WebSocket / KCP
- serializer 可切换，JSON 和 MemoryPack 都能接
- 样板代码交给生成器
- 双向通信也保持强类型

尤其是 Unity + .NET 这种组合下，`shared contracts + generated stubs + typed callbacks` 这套思路会比裸 socket 或手写消息协议轻松很多。

## 自己开项目的话，建议怎么落地

我建议第一版按这个顺序来：

1. 先建一份 contracts
2. 先定义一个最小 service，比如 `IPlayerService`
3. 先定义一个最小 callback，比如 `OnNotify`
4. 先把 codegen 跑通
5. 先用 JSON + WebSocket 打通第一条链路
6. 稳了以后，再考虑切到 MemoryPack、TCP 或 KCP

原因也很现实：

- JSON 更方便排错
- WebSocket 本地调试更直观
- 先验证契约和调用模型，再做性能优化，成本最低

## 接下来可以看什么

把这篇教程走通之后，下一步建议看这两个 sample：

- `RpcCall.Kcp`
  看最精简版本是什么样
- `RpcCall.MemoryPack`
  看多 service、多 callback、TCP 场景怎么组织

等你第一条链路跑通之后，再考虑这些增强项：

- 换成 `MemoryPackRpcSerializer`
- 切到 `TcpTransport` 或 `Kcp`
- 开启压缩和加密
- 拆出更细的 service group

## 最后总结一下

你可以把 `ULinkRPC` 理解成一套“契约优先”的 Unity / .NET 双向 RPC 方案。

它真正有价值的点，不只是“能远程调用”，而是它把下面这些东西串成了一条比较稳定的开发链路：

- 共享契约
- 强类型接口
- 自动生成客户端和服务端胶水代码
- 可切换 transport
- 可切换 serializer
- 服务端主动推送

如果你要在 Unity 里做一套能长期维护的通信层，这种写法会比手写协议省事很多。

下一篇可以接着看：[从零搭一个最小可跑的 ULinkRPC Server / Client Sample](/ULinkRPC/posts/ulinkrpc-sample-server-client-from-zero/)
