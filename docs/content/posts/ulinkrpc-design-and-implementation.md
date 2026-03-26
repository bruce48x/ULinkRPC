---
title: ULinkRPC 设计思路与底层实现拆解：为什么它能把 Unity 和 .NET 双向 RPC 做得既强类型又不笨重
date: 2026-03-18T10:45:00+08:00
tags:
  - ulinkrpc
  - unity
  - dotnet
  - rpc
  - architecture
  - codegen
categories:
  - Architecture
---

前两篇文章更偏“怎么用”：

- [《用 ULinkRPC 从零搭一个 Unity 和 .NET 双向通信项目》](/ULinkRPC/posts/ulinkrpc-getting-started/)

但如果你准备把 ULinkRPC 真正放进项目里，通常会继续问这几个问题：

- 它为什么要强依赖“共享契约 + 代码生成”？
- 它和“手写消息号 + switch 分发”相比，本质差别是什么？
- 双向通信到底是怎么落到一条连接上的？
- Transport、Serializer、Runtime、CodeGen 之间是怎么解耦的？
- 服务端 callback、客户端 push、请求响应、保活、压缩、加密，这些东西分别在哪一层？

这篇文章不再讲环境搭建，只讲设计本身：它为什么这样分层，为什么一定要 codegen，双向通信到底是怎么落到一条连接上的。

## 先给一句总纲：ULinkRPC 其实是“契约驱动 + 代码生成 + 帧级运行时”的组合

先记住一句话就够了：**你写的是契约，生成器把契约翻译成胶水代码，运行时再把胶水代码变成真实网络收发。**

如果拆开看，它大致是三层：

### 1. 契约层：只描述“谁可以调用谁”

这一层只关心共享接口和 DTO：

- `[RpcService]` 标记服务接口
- `[RpcMethod]` 标记客户端可调用的方法
- `[RpcCallback]` 标记服务对应的回调接口
- `[RpcPush]` 标记服务端主动推送的方法

这一层只定义“能调什么、能推什么”，不处理网络细节。

### 2. 生成层：把契约翻译成“可运行的胶水代码”

这一层由 `ULinkRPC.CodeGen` 负责。它会读取 contracts 源码，然后分别生成：

- 客户端 service proxy
- 客户端 callback binder
- 客户端统一入口 `RpcApi`
- 服务端 binder
- 服务端 callback proxy
- 服务端 `AllServicesBinder`

生成层的职责，就是把“接口定义”翻译成“可发包、可收包、可分发”的代码。

### 3. 运行时层：真正把一帧帧数据在连接上送出去

运行时主要由几部分组成：

- `ITransport`：负责“收发完整帧”
- `IRpcSerializer`：负责“对象 <-> payload”
- `RpcEnvelopeCodec`：负责“RPC 包头 <-> 二进制帧”
- `RpcClientRuntime`：负责客户端请求、等待响应、接收 push
- `RpcSession`：负责服务端收包、分发、回包、推送
- `TransportFrameCodec` / `TransformingTransport`：负责压缩和加密

可以把它记成一句话：

> **Contract 定义语义，CodeGen 生成胶水，Runtime 负责收发。**

---

## 为什么 ULinkRPC 不鼓励你直接手写网络消息

很多 Unity + 服务端项目一开始都会走一条更熟悉的路：

1. 自己定义一个消息号枚举
2. 手写请求结构
3. 客户端发一个 `messageId`
4. 服务端用 `switch` 或字典分发
5. 回包时再手写一层 response 结构
6. 服务端主动推送时再补一套 push 协议

这条路不是不能走，但问题通常会在项目变复杂以后一起爆出来：

- 消息号和业务方法名是两套概念
- 客户端和服务端容易各自维护一份协议
- 回调推送常常和请求响应不是一套模型
- 新人接手时，看不出“这个消息到底对应哪段业务接口”
- refactor 时，重命名接口/参数不会自动同步到底层分发代码

ULinkRPC 的选择是反过来：

**不把 message id 作为第一抽象，而把“接口方法”作为第一抽象。**

然后再把 `serviceId` / `methodId` 退回到底层实现细节里。

所以你表面上写的是：

```csharp
[RpcService(1, Callback = typeof(IPlayerCallback))]
public interface IPlayerService
{
    [RpcMethod(1)]
    ValueTask<LoginReply> LoginAsync(LoginRequest req);
}
```

但运行时真正依赖的仍然是稳定 id：

- `serviceId = 1`
- `methodId = 1`

这相当于同时保住了两件事：

- **业务层可读性**：你面对的是接口和方法
- **协议层稳定性**：网络上传的还是固定数字 id

这就是它比“全手写协议”更值得长期维护的地方。

---

## 为什么强制“一个方法只收一个 DTO”

这是 ULinkRPC 最近一个比较明确的设计收敛：**不再鼓励把方法参数列表直接映射到网络 payload，而是要求 `RpcMethod` 和 `RpcPush` 都只带一个 DTO。**

也就是说，只能这样写：

```csharp
[RpcMethod(1)]
ValueTask<LoginReply> LoginAsync(LoginRequest req);

[RpcPush(1)]
void OnNotify(PlayerNotify notify);
```

而不是这样：

```csharp
[RpcMethod(1)]
ValueTask<LoginReply> LoginAsync(string account, string password);

[RpcPush(1)]
void OnNotify(string message);
```

### 这次收敛的起因是什么

最早如果允许“零参数 / 单参数直传 / 多参数元组”三种模式同时存在，表面上看很灵活，但长期会暴露几个问题：

- 方法签名一改，wire payload 形状也跟着改
- 多参数方法天然依赖参数顺序
- callback 如果也支持裸参数，协议风格会越来越散
- codegen 里要维护 `RpcVoid / 单参数 / ValueTuple` 三套分支
- 文档和 sample 很难形成统一规范

这对短期 demo 没什么，但对多人协作、长期迭代、前后端版本错位这些真实场景并不友好。

### 为什么不直接上 protobuf 那套字段号模型

原因也很现实：ULinkRPC 目前同时支持 JSON 和 MemoryPack，而这两个 serializer 都不是天然围绕“显式字段号协议”设计的。  
如果硬把整个 contract 体系往 protobuf 风格改，会让使用方式变重，反而损失 ULinkRPC 现在“看起来像普通 C# 接口”的自然感。

所以现在选的是一个更务实的中间方案：

- 不强行引入字段号体系
- 不要求用户写一套额外的 schema 语言
- 先把最容易出问题的“方法参数列表直接上网”收掉
- 统一成“一个请求 DTO / 一个响应 DTO / 一个推送 DTO”

### 这个取舍到底换来了什么

好处：

- RPC 方法的 wire shape 更稳定
- codegen 明显简单很多
- callback、request、response 的模型更统一
- sample、文档、团队约定都更容易收敛
- 后面要做版本演进时，主要是在 DTO 层思考，而不是在参数列表层思考

代价：

- 要多写一些 DTO
- 小接口看起来会比“裸参数方法”啰嗦一点
- 它不是 protobuf v3 那种“字段随便增减都天然兼容”的完整方案

也就是说，这个设计不是“兼容性最强”的方案，而是“自然度、实现复杂度、长期可维护性”之间比较平衡的一种方案。

对 ULinkRPC 这个定位来说，我认为这个取舍是合适的：  
**先让 contract 风格稳定，再在 DTO 层逐步约束演进规则，而不是一开始就把整个框架做成重协议系统。**

---

## 整体分层怎么理解：上层按语义组织，下层按帧组织

ULinkRPC 的分层非常清晰，而且分层边界是刻意设计过的。

## 第一层：Contract 层只管语义

在 `ULinkRPC.Core` 里，契约本身其实非常薄。核心就是一组 Attribute 和方法描述类型：

- `RpcServiceAttribute`
- `RpcMethodAttribute`
- `RpcCallbackAttribute`
- `RpcPushAttribute`
- `RpcMethod<TArg, TResult>`
- `RpcPushMethod<TArg>`

这一层不关心：

- 你用的是 TCP 还是 WebSocket
- 你用的是 JSON 还是 MemoryPack
- 你怎么发包
- 你怎么收包

它只关心一件事：**“这个接口方法，在 RPC 语义上意味着什么”。**

这能带来一个很重要的结果：

**框架的顶层心智模型不是“包”，而是“服务接口”。**

---

## 第二层：Envelope 层把语义压成稳定的二进制头

真正上网跑的时候，最后一定还是字节流。所以在 Contract 和 Transport 之间，还需要一层稳定的中间格式：**Envelope**。

在 `RpcEnvelopes` / `RpcEnvelopeCodec` 里，它把 RPC 数据拆成了 5 种帧：

- `Request`
- `Response`
- `Push`
- `KeepAlivePing`
- `KeepAlivePong`

可以把协议头粗略理解成这样。这里的整数都是大端序：

```text
Request
+-----------+-----------------+-----------------+-----------------+------------------+
| Type(1B)  | RequestId(4B)   | ServiceId(4B)   | MethodId(4B)    | PayloadLen(4B)   |
+-----------+-----------------+-----------------+-----------------+------------------+
|                                  Payload bytes...                                 |
+------------------------------------------------------------------------------------+
Fixed header = 17 bytes

Response
+-----------+-----------------+------------+------------------+--------------+
| Type(1B)  | RequestId(4B)   | Status(1B) | PayloadLen(4B)   | HasError(1B) |
+-----------+-----------------+------------+------------------+--------------+
| Payload bytes... | ErrorLen(4B, optional) | Error bytes...(optional, UTF-8) |
+---------------------------------------------------------------------------+
Fixed header = 11 bytes
If HasError = 1, append ErrorLen(4B) + Error bytes

Push
+-----------+-----------------+-----------------+------------------+
| Type(1B)  | ServiceId(4B)   | MethodId(4B)    | PayloadLen(4B)   |
+-----------+-----------------+-----------------+------------------+
|                           Payload bytes...                           |
+----------------------------------------------------------------------+
Fixed header = 13 bytes

KeepAlivePing / KeepAlivePong
+-----------+----------------------+
| Type(1B)  | TimestampTicks(8B)   |
+-----------+----------------------+
Fixed header = 9 bytes
```

这里最关键的区别只有一个：

- `Request / Response` 需要 `RequestId`，因为它们是一问一答
- `Push` 不需要 `RequestId`，因为它是单向通知

所以 ULinkRPC 没有搞一个“万能消息结构”把所有情况硬塞进去，而是直接在协议层把语义拆开。这样运行时判断更简单，生成器生成出来的代码也更干净。

### 为什么 Envelope 和 Serializer 是分开的

`RpcEnvelopeCodec` 只负责编码头部和 payload 边界，不负责 payload 里面对象怎么序列化。

换句话说：

- Envelope 负责“这是不是请求、它属于哪个 service、哪个 method、payload 有多长”
- Serializer 负责“payload 里的对象字段到底怎么编码”

这就是它能自由切换 JSON / MemoryPack 的根本原因：**协议头固定，payload 编码可替换。**

---

## 第三层：Transport 层不关心 RPC，只关心“完整帧”

`ITransport` 的设计很值得注意。它没有暴露什么“发字节片段”“按流读取”的 API，而是直接定义成：

- `SendFrameAsync(ReadOnlyMemory<byte> frame)`
- `ReceiveFrameAsync()`

也就是说，**Transport 的边界不是 stream，而是 frame。**

这是个很关键的取舍。

### 这样设计的好处是什么

它把复杂度压到了 transport 实现里，而不是把复杂度泄漏给 RPC runtime。

对 runtime 来说，它永远面对的是：

- “拿到一个完整的请求帧”
- “送出一个完整的响应帧”

于是：

- TCP 需要自己解决拆包/粘包
- WebSocket 天生更接近消息帧
- KCP 也有自己的消息边界

但这些差异都被 transport 层吃掉了。上层 runtime 不需要知道底下到底是不是基于流。

### 这就是 ULinkRPC 能换传输层的真正原因

很多框架说“支持多传输”，其实只是把 socket 客户端包了几层；但 ULinkRPC 这里是更彻底的抽象：

**只要你实现的是“完整帧收发”，上层 RPC 就完全不用改。**

所以 TCP / WebSocket / KCP 能挂在同一套 runtime 上，不是因为它们相似，而是因为接口边界定义得够准。

---

## 第四层：Runtime 层负责“把帧变成调用”

真正让 RPC 成立的，不是 Attribute，也不是 Transport，而是 Runtime。

客户端的核心是 `RpcClientRuntime`，服务端的核心是 `RpcSession`。你可以把它俩看成一对镜像组件。

### 客户端 runtime 主要做什么

`RpcClientRuntime` 核心在做 4 件事：

1. 给每个请求分配递增的 `requestId`
2. 把请求对象序列化后封成 `Request` 帧发出去
3. 用 `_pending` 字典按 `requestId` 等待响应
4. 后台接收循环里处理 `Response` / `Push` / `KeepAlivePong`

这意味着客户端本质上是一个 **“多路复用请求管理器”**：

- 一个连接上可以同时飞多个请求
- 每个请求靠 `requestId` 找回自己的 `TaskCompletionSource`
- 收到 response 后，再按 `requestId` 唤醒对应等待方

所以它虽然对外暴露的是强类型 `CallAsync<TArg, TResult>`，但底层其实仍然是典型的异步复用模型。

### 服务端 session 主要做什么

`RpcSession` 对应的是服务端单连接会话。它主要做 5 件事：

1. 启动 transport
2. 循环收取完整帧
3. 遇到 `Request` 就解出 `serviceId/methodId`
4. 从 handler registry 找到对应处理器
5. 执行业务逻辑，序列化结果，再回 `Response`

如果服务端要主动给客户端发消息，就通过：

- `RpcSession.PushAsync(serviceId, methodId, arg)`

也就是说，**服务端 push 和服务端 response 共享同一条连接、同一套 framing，只是 frame type 不同。**

这正是“双向通信”能成立的关键：

- 客户端并不只是“请求方”
- 服务端也不只是“被动应答方”
- 这是一条双向可写的全双工会话

---

## 请求响应链路到底怎么走：从接口调用到收到返回值

如果你在 Unity 里写：

```csharp
var reply = await client.Api.Game.Player.LoginAsync(req);
```

底层大致会经过下面这条链路。

```text
Unity caller
    |
    v
client.Api.Game.Player.LoginAsync(req)
    |
    v
PlayerServiceClient (generated proxy)
    |
    v
RpcClientRuntime.CallAsync(...)
    |
    +--> Serializer.Encode(req)
    |
    +--> RpcEnvelopeCodec.EncodeRequest
    |        Request = [Type|RequestId|ServiceId|MethodId|PayloadLen|Payload]
    |
    v
Transport.SendFrameAsync(frame)
    |
    v
========================= network =========================
    |
    v
RpcSession.ReceiveLoopAsync
    |
    +--> RpcEnvelopeCodec.DecodeRequest
    |
    +--> RpcServiceRegistry lookup(serviceId, methodId)
    |
    +--> PlayerServiceBinder.InvokeAsync(...)
    |        |
    |        +--> Serializer.Decode(req payload)
    |        +--> PlayerService.LoginAsync(req)
    |        +--> Serializer.Encode(reply)
    |        +--> RpcEnvelopeCodec.EncodeResponse
    |
    v
Transport.SendFrameAsync(response frame)
    |
    v
========================= network =========================
    |
    v
RpcClientRuntime.ReceiveLoopAsync
    |
    +--> RpcEnvelopeCodec.DecodeResponse
    +--> match pending RequestId
    +--> complete Task<LoginReply>
    |
    v
await LoginAsync(...) returns
```

### 第 1 步：调用的是生成出来的 client proxy

你以为自己在调用接口，其实调用的是 CodeGen 生成的 `PlayerServiceClient`。

这个 proxy 里会预先定义：

```csharp
private static readonly RpcMethod<LoginRequest, LoginReply> loginAsyncRpcMethod = new(ServiceId, 1);
```

然后真正转发到：

```csharp
_client.CallAsync(loginAsyncRpcMethod, req, ct)
```

也就是说，**生成代码把“接口方法调用”翻译成了“带 serviceId/methodId 的 runtime 调用”。**

### 第 2 步：runtime 生成 requestId，并把参数序列化

`RpcClientRuntime.CallAsync` 会：

- 生成一个新的 `requestId`
- 用 serializer 把 `req` 编成 payload
- 组装成 `RpcRequestEnvelope`
- 交给 `RpcEnvelopeCodec.EncodeRequest`

这样就从“一个 C# 对象调用”变成了“一段可发出的二进制帧”。

### 第 3 步：transport 把 frame 发出去

runtime 不关心底下是 TCP / WS / KCP，只会调用：

```csharp
_transport.SendFrameAsync(frame)
```

如果启用了压缩或加密，实际可能还会先经过 `TransformingTransport`，也就是：

- 先做压缩
- 再做加密与鉴别
- 最后再交给底层 transport

### 第 4 步：服务端 session 收到帧并识别为 Request

`RpcSession` 的循环读取到一个 frame 后，会先 `PeekFrameType`。

- 如果是 `KeepAlivePing`，先回 pong
- 如果不是 `Request`，直接忽略
- 如果是 `Request`，解出请求 envelope

然后拿着 `(serviceId, methodId)` 去找 handler。

### 第 5 步：handler 来自生成出来的 binder

这里是很多人第一次看会恍然大悟的地方。

服务端不是自己手写一张巨大的分发表，而是 CodeGen 为每个服务生成 binder。比如 `PlayerServiceBinder` 会把：

- `(1, 1)` 绑定到 `LoginAsync`
- `(1, 2)` 绑定到 `IncrStep`

binder 做的事情包括：

- 从 `req.Payload` 反序列化出参数
- 拿到 service 实现对象
- 调用真实业务方法
- 把结果序列化成 response payload
- 拼成 `RpcResponseEnvelope`

所以 binder 的本质作用是：

**把“协议分发”自动编译成一组静态类型安全的注册代码。**

### 第 6 步：服务实现被真正执行

到这一步，才轮到你写的业务类，比如 `PlayerService`。

业务类本身完全不需要知道：

- 请求是从哪条网络连接来的
- 包头长什么样
- response 要怎么编码

它只要实现普通接口方法即可。

这就是 ULinkRPC 很重要的一点：

**把业务代码从网络样板代码里解放出来。**

### 第 7 步：响应回到客户端，并按 requestId 完成等待

客户端收到 `Response` 后，`RpcClientRuntime.ReceiveLoopAsync` 会：

- 解码 response
- 用 `response.RequestId` 去 `_pending` 字典找对应的 `TaskCompletionSource`
- 设置结果

于是最外层 `await client.Api.Game.Player.LoginAsync(req)` 就恢复执行。

### 第 8 步：如果失败，错误也沿着协议返回

如果服务端 handler 找不到、业务抛异常、返回异常状态，response 里会带：

- `Status`
- `ErrorMessage`

客户端 runtime 检查到 `Status != Ok` 时会抛异常。

所以调用端的编程体验依然是“await 一个方法，失败就抛异常”，但底层已经经过了完整的一次网络往返。

---

## 服务端推客户端是怎么成立的：callback 不是外挂，而是契约的一部分

ULinkRPC 最实用的地方之一，就是它没有把“服务端主动推送”设计成一套完全不同的系统。

它的思路非常统一：

- 客户端 -> 服务端：`[RpcService] + [RpcMethod]`
- 服务端 -> 客户端：`[RpcCallback] + [RpcPush]`

对应的数据流大致是这样：

```text
Server business code
    |
    v
_callback.OnNotify(new PlayerNotify { Message = "hello" })
    |
    v
PlayerCallbackProxy (generated)
    |
    +--> Serializer.Encode("hello")
    +--> RpcEnvelopeCodec.EncodePush
    |        Push = [Type|ServiceId|MethodId|PayloadLen|Payload]
    |
    v
RpcSession.PushAsync(...)
    |
    v
Transport.SendFrameAsync(push frame)
    |
    v
========================= network =========================
    |
    v
RpcClientRuntime.ReceiveLoopAsync
    |
    +--> RpcEnvelopeCodec.DecodePush
    +--> find callback handler by (serviceId, methodId)
    +--> PlayerCallbackBinder.Invoke(...)
    |        |
    |        +--> Serializer.Decode(push payload)
    |        +--> PlayerCallbackReceiver.OnNotify(notify)
    |
    v
Unity callback code runs
```

### 为什么这点很重要

很多项目的问题不在“请求”这部分，而在“推送”这部分：

- 请求走 RPC
- 推送走另一套消息系统
- 结果同一个业务模块要维护两套协议定义

ULinkRPC 的做法是：**把 callback 也放进同一份契约里。**

例如：

```csharp
[RpcService(1, Callback = typeof(IPlayerCallback))]
public interface IPlayerService
{
    [RpcMethod(1)]
    ValueTask<LoginReply> LoginAsync(LoginRequest req);
}

[RpcCallback(typeof(IPlayerService))]
public interface IPlayerCallback
{
    [RpcPush(1)]
    void OnNotify(PlayerNotify notify);
}
```

这段定义的意思不是“顺便附送一个推送接口”，而是：

**`IPlayerService` 这项服务天然就拥有一个反向 callback 通道。**

### 服务端 callback proxy 是怎么工作的

服务端生成代码会产出一个 `PlayerCallbackProxy`。它实现 `IPlayerCallback`，但方法体内部做的不是本地逻辑，而是：

- 把参数序列化
- 调用 `RpcSession.PushAsync`
- 发出一个 `Push` 帧

所以当你的服务实现里写：

```csharp
_callback.OnNotify(new PlayerNotify { Message = "hello" })
```

你看起来像在调用本地对象，实际上是在：

- 使用 callback proxy
- 发出一个 server-to-client push frame

这就是 ULinkRPC 很舒服的地方：

**推送在业务代码里表现成“调接口”，而不是“手搓推送包”。**

### 客户端 callback binder 又做了什么

客户端这边生成的 `PlayerCallbackBinder` 会把 `(serviceId, methodId)` 绑定到你注册的 callback receiver。

也就是说，当客户端 runtime 收到一个 `Push` 帧时：

1. 解出 `serviceId` / `methodId`
2. 找到对应 push handler
3. 反序列化 payload
4. 回调到你自己的 receiver

这套链路说明：

- 服务端 callback proxy 负责“把接口调用变成 Push 帧”
- 客户端 callback binder 负责“把 Push 帧变回接口调用”

前后两边正好闭环。

---

## 为什么要做代码生成：因为“反射式 RPC”在 Unity 场景里并不划算

很多人看到这里会问：为什么不直接运行时反射？

理论上当然可以：

- 启动时扫程序集
- 找 `[RpcService]`
- 找 `[RpcMethod]`
- 动态建分发表
- 收到请求后再反射调用

但 ULinkRPC 还是选择了 CodeGen，主要是因为这几件事：

```text
Without CodeGen

contracts
   |
   +--> hand-written client proxy
   +--> hand-written request packing
   +--> hand-written response unpacking
   +--> hand-written callback dispatch
   +--> hand-written server routing table
   +--> hand-written binder / push proxy


With CodeGen

contracts
   |
   v
ULinkRPC.CodeGen
   |
   +--> generated client proxy
   +--> generated RpcApi / RpcClient facade
   +--> generated callback binder
   +--> generated server binder
   +--> generated callback proxy
   +--> generated all-services registration

business code
   |
   +--> implement service interface
   +--> implement callback receiver
```

差别不在于少写几行，而在于谁来维护那一大坨重复、脆弱、又很容易和契约漂移的胶水代码。

### 1. Unity / AOT 场景更适合静态生成

Unity、IL2CPP、AOT 环境对“运行时反射 + 动态生成”并不友好。提前生成静态代码，会更稳。

### 2. 生成后更容易看懂和排错

出了问题，你可以直接打开生成文件看到：

- 某个方法对应哪个 `serviceId/methodId`
- 请求参数怎么打包
- 服务端怎么反序列化
- callback 怎么绑定

这比把逻辑藏在黑盒反射里更容易调试。

### 3. 运行时成本更低

把分发逻辑提前编译成静态代码，能减少：

- 运行时反射扫描
- 动态绑定开销
- 通用对象装箱/拆箱路径

### 4. 约束更清晰

CodeGen 在扫描契约时会直接校验：

- `ServiceId` 是否重复
- `MethodId` 是否重复
- callback 是否和 service 对得上
- RPC 方法返回值是否是 `ValueTask` / `ValueTask<T>`

这意味着很多协议错误会在生成阶段就被挡下来，而不是拖到运行时才炸。

所以 ULinkRPC 的代码生成不是“为了炫技”，而是很务实地在换取三样东西：

- Unity 兼容性
- 静态可读性
- 更早暴露错误

---

## CodeGen 到底做了哪些脏活

如果把生成器的职责讲得再具体一点，它其实在帮你做 6 类事情。

## 1. 解析 contracts 源码

`ContractParser` 不是在运行时扫程序集，而是直接分析 contracts 目录下的 C# 源文件。

这一步会抽取出：

- service 接口名
- service id
- method 列表
- 参数类型与顺序
- 返回值类型
- callback 接口及其 push 方法
- 所需 using

这意味着生成器的输入不是 DLL，而是源码契约。好处是：

- 生成阶段拿到的信息更完整
- 还没正式编译进业务项目前就能做校验
- 更适合 Unity / shared contracts 的工作流

## 2. 生成客户端 proxy

每个 service 都会生成一个 `XxxServiceClient`。

它的职责很简单：

- 预先把方法 id 固化成 `RpcMethod<TArg, TResult>` 字段
- 把接口调用转发到 `_client.CallAsync(...)`

这让最终使用者可以写出和本地接口近似的调用方式。

## 3. 生成客户端 callback binder

如果 service 声明了 callback，生成器会产出 `XxxCallbackBinder`。

它的职责是：

- 预定义 `RpcPushMethod<TArg>`
- 给 runtime 注册 push handler
- 收到 push 后把参数解出来
- 调用你提供的 callback receiver

## 4. 生成客户端统一门面 `RpcApi`

如果项目里有多个 service，最终并不是让你手动 new 一堆 client proxy，而是生成：

- `RpcApi`
- 分组后的 `xxxRpcGroup`
- 扩展后的 `RpcClient`

所以你最终拿到的是这种体验：

```csharp
client.Api.Game.Player.LoginAsync(...)
client.Api.Game.Inventory.GetRevisionAsync(...)
```

它本质上是在把“多个独立 proxy”组织成一个更适合业务代码消费的门面。

## 5. 生成服务端 binder

服务端 binder 是整个 CodeGen 最关键的产物之一。

它把每个 `(serviceId, methodId)` 都注册到 `RpcServiceRegistry`，并在 handler 里完成：

- 参数解码
- service 实现获取
- 业务方法调用
- 返回值编码
- response 组装

如果说客户端 proxy 负责“把本地调用翻译成网络请求”，那服务端 binder 负责的就是“把网络请求翻译回本地调用”。

## 6. 生成服务端 callback proxy 和总绑定器

如果服务有 callback，服务端还会生成 callback proxy。再加上 `AllServicesBinder`，就能做到：

- 自动发现服务实现
- 自动按规则绑定
- 或者让你显式传入 service 实例 / factory

这让小项目可以开箱即用，大项目也能保留显式控制权。

---

## 服务端为什么有 registry + session scoped service 这套结构

很多人第一次看服务端代码时，会注意到两个点：

- 有一个全局的 `RpcServiceRegistry`
- `RpcSession` 里又有 `_scopedServices`

这不是重复设计，而是两个不同层级。

### `RpcServiceRegistry` 解决的是“怎么找到处理器”

registry 的 key 是：

- `serviceId`
- `methodId`

它存的是 `RpcSessionHandler`，也就是：

- 某个会话收到某个 RPC 方法时，应该执行什么逻辑

所以 registry 负责的是 **协议级分发**。

### `_scopedServices` 解决的是“同一连接复用哪个服务实例”

当 binder 执行业务方法前，会通过：

```csharp
server.GetOrAddScopedService(ServiceId, implFactory)
```

来拿服务实例。

这意味着默认语义更接近：

- **每个 session / 每个 serviceId 对应一个 scoped service 实例**

这样做有什么好处？

- 一个连接上的状态可以自然挂在 service 实例里
- callback proxy 可以和当前 session 绑死
- 不需要你自己维护“连接 -> 服务对象”的映射

这对有连接态的业务很自然，比如：

- 登录状态
- 当前玩家上下文
- 房间会话状态
- 某连接的回调通道

所以这层设计不是多余，而是在帮你把“连接作用域”沉到框架里。

---

## 保活为什么放在 runtime，而不是 transport

ULinkRPC 里 keepalive 不是某个 transport 自己各玩各的，而是由 RPC runtime 统一处理：

- 客户端有 `KeepAlivePing` / `KeepAlivePong`
- 服务端 session 在收到 ping 时回 pong
- 客户端可测 RTT
- 服务端也能基于超时判定连接失效

### 这么做的好处是什么

如果把保活放在 transport 层，每个 transport 都要各自实现一套：

- TCP 一套
- WebSocket 一套
- KCP 一套

而且语义未必统一。

现在放在 RPC frame 层之后，它就变成了跨 transport 的统一行为：

- 任何 transport 只要能发 frame
- keepalive 就都能工作

也就是说，**keepalive 是 RPC 会话语义的一部分，不是某个具体 socket 的特性。**

这和 ULinkRPC 的整体设计是一致的：

- transport 提供“送帧能力”
- runtime 提供“连接会话语义”

---

## 压缩和加密为什么又单独抽成 `TransformingTransport`

除了 RPC runtime 和底层 transport 之外，ULinkRPC 还插了一层 `TransformingTransport`。

它的想法很工程化：

- transport 负责连通性
- runtime 负责 RPC 语义
- 安全与压缩属于“frame 变换”

所以：

- 压缩阈值控制在 `TransportSecurityConfig`
- 编码/解码逻辑在 `TransportFrameCodec`
- 组合方式通过 `TransformingTransport` 包住真实 transport

### 这种设计的好处

它避免了两种常见混乱：

#### 混乱 1：把压缩写进 serializer

这样会让“对象编码”和“网络优化”耦合在一起，不同 serializer 的行为也会变复杂。

#### 混乱 2：把加密写进每个 transport

这样 TCP、WS、KCP 都得各自维护一套压缩/加密实现，重复度高。

现在 ULinkRPC 的做法是：

- 先得到标准 frame
- 如果有需要，再做 frame 级压缩/加密
- 收到后先解密/解压，再还原成标准 frame

这说明框架把“安全”和“协议”分得很清楚。

不过也要注意：这里的加密是框架层的对称加密方案，它解决的是“帧内容保护”，不是完整的 TLS 替代品。实际项目里如果 transport 自己已经跑在 TLS/WSS 上，要不要再叠一层，需要结合你的部署环境来判断。

---

## ULinkRPC 的双向能力，本质上不是“两套系统”，而是“一条全双工会话上的两类 frame”

这是我觉得最值得建立的正确心智模型。

很多人一说双向 RPC，就会想成：

- 一套 client -> server 系统
- 再额外做一套 server -> client 推送系统

但 ULinkRPC 不是这么拆的。

它更接近：

- 底层只有一条全双工会话
- 会话上跑几种不同 frame type
- 请求响应是一类 frame 组合
- 服务器推送是另一类 frame 组合

也就是说：

- **双向能力来自连接模型本身**
- **不是来自另起炉灶的第二套协议栈**

这会带来两个很现实的好处：

### 1. 心智统一

不管是请求、响应、推送、保活，都是围绕：

- frame
- service id
- method id
- payload

来运转的。

### 2. 维护成本低

你不需要为“推送”再维护另一套 serializer、另一套消息总线、另一套 handler 体系。

这对 Unity 项目尤其重要，因为客户端本来就不适合承受太多重复基础设施。

---

## 这套设计最适合什么项目，不适合什么项目

讲完设计，再说适用边界会更客观。

## 比较适合的场景

### 1. Unity + .NET 的共享契约项目

这是 ULinkRPC 最天然的主场。你本来就有：

- Unity 客户端
- C# 服务端
- 共享 DTO / 接口

那它的契约驱动和代码生成就会非常顺手。

### 2. 需要明确服务边界的业务

比如：

- 登录
- 背包
- 任务
- 房间
- 战斗控制指令
- 小规模状态同步

这些模块很适合天然地拆成 service。

### 3. 想要“强类型 + 双向回调”，但不想手搓协议的人

如果你已经厌倦：

- 消息号管理
- 手写编解码映射
- push 协议和 request 协议分裂

ULinkRPC 就很对症。

## 可能不那么适合的场景

### 1. 你需要极端自由的二进制协议布局

如果你要手动抠到 bit-level、字段压缩、极限包体设计，那 ULinkRPC 这种通用 RPC 抽象就不会是最贴身的方案。

### 2. 你传的不是“服务调用”，而是高频流式数据

例如非常高频、超细粒度的同步流，如果每条都建模成 RPC 方法，未必划算。更适合把 ULinkRPC 用在：

- 控制面
- 业务调用面
- 中低频状态分发

而把极高频数据通道做成更专用的协议。

### 3. 你根本不打算共享 C# 契约

ULinkRPC 的优势很大程度来自共享接口和生成代码。如果前后端语言不统一、也不想共享契约，那它的收益会下降很多。

---

## 如果你准备在真实项目里用它，建议记住这 7 条原则

### 1. 把 contracts 当成第一公民

真正长期稳定的不是 generated 文件，而是契约本身。

### 2. `serviceId` / `methodId` 一旦上线就尽量别乱改

因为它们是协议稳定标识，不只是代码里的装饰数字。

### 3. 把 callback 当成服务设计的一部分

如果某服务天然要从服务端反推客户端，就一开始写进契约，不要后面再补一套旁路协议。

### 4. 把 ULinkRPC 优先用在“控制面”和“清晰语义调用”上

像登录、匹配、背包、任务、房间管理、玩法指令，都很合适。

### 5. 让 transport 和 serializer 的选择服务于场景

- 想更直观排错：先 JSON
- 想更高效：再 MemoryPack
- Web 兼容优先：WebSocket
- 更偏实时联机原型：TCP / KCP

### 6. 生成代码不要手改

它是契约的投影，不是手工维护层。改行为应该回到 contracts 或 runtime。

### 7. 业务代码尽量别感知网络细节

如果你的业务实现到处在关心包头、消息号、序列化细节，说明抽象已经被穿透了。

---

## 最后收个尾：ULinkRPC 真正解决的问题，不是“怎么发包”，而是“怎么长期维护一套双向通信边界”

很多框架都能把数据从 A 发到 B，真正难的是下面这些事能不能一起成立：

- 契约能不能共享
- 接口能不能强类型
- 客户端调用和服务端推送能不能统一建模
- 传输层能不能替换
- 序列化能不能替换
- Unity / .NET 工作流能不能顺
- 新人接手时能不能快速看懂

ULinkRPC 给出的答案其实很朴素：

1. **用接口定义通信边界**
2. **用代码生成填平样板代码**
3. **用轻量运行时处理请求、响应、推送和保活**
4. **用 transport / serializer 抽象保持底层可替换**

它不是那种大而全的网络框架，更像一套边界很清楚的组合：

- 上层是强类型契约
- 中层是自动生成的胶水代码
- 下层是统一帧协议和可替换基础设施

如果你正好在做 Unity + .NET 项目，这套思路会比较顺：写契约、跑生成、接 runtime、业务层只管实现接口和处理 callback。

一句话收尾：

> **ULinkRPC 不是在帮你“省几行发包代码”，而是在帮你把双向 RPC 的边界长期维护好。**
