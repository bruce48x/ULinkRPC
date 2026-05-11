---
title: 核心运行时 API 概览
date: 2026-05-11T00:00:00+08:00
---

这一页先覆盖 ULinkRPC 用户最常接触的运行时类型。后续会继续拆成逐类 API reference，并补齐构造函数、异常、线程安全和示例。

逐成员 API 文档从代码 XML 注释生成，见 [API Reference](/ULinkRPC/reference/api/)。

## 客户端类型

| 类型 | 包 | 主要职责 |
| --- | --- | --- |
| `RpcClientOptions` | `ULinkRPC.Client` | 组合客户端 transport、serializer、keepalive 和可选 frame security。 |
| `RpcClientRuntime` | `ULinkRPC.Client` | 启动连接、发送请求、等待响应、分发服务端 push、维护 keepalive 状态。 |
| 生成的 `RpcClient` | generated client code | 包装 `RpcClientRuntime`，绑定 callback receiver，并暴露 `client.Api` 强类型门面。 |

### `RpcClientOptions`

`RpcClientOptions` 构造时必须传入一个 `ITransport` 和一个 `IRpcSerializer`。`KeepAlive` 默认关闭。调用 `UseSecurity(...)` 后，`CreateConfiguredTransport()` 会在原 transport 外包一层 `TransformingTransport`。

典型用法：

```csharp
var options = new RpcClientOptions(
    new WsTransport("ws://127.0.0.1:20000/ws"),
    new JsonRpcSerializer())
{
    KeepAlive = new RpcKeepAliveOptions
    {
        Enabled = true,
        Interval = TimeSpan.FromSeconds(15),
        Timeout = TimeSpan.FromSeconds(45)
    }
};
```

### `RpcClientRuntime`

`RpcClientRuntime.StartAsync(...)` 会连接 transport，并启动后台接收循环、push 分发循环，以及可选 keepalive 循环。一个 runtime 实例只能启动一次；重复调用 `StartAsync` 会抛出 `InvalidOperationException`。

`CallAsync<TArg, TResult>(...)` 会：

1. 分配 request id。
2. 序列化请求 DTO。
3. 编码 `Request` envelope。
4. 发送 frame。
5. 等待匹配 request id 的 `Response`。
6. 如果响应状态不是 `RpcStatus.Ok`，抛出 `InvalidOperationException`。

`RegisterPushHandler(...)` 注册的处理器由客户端内部 push loop 调用，不保证在 Unity 主线程执行。Unity 客户端如果要改场景、GameObject 或 UI，应把回调内容切回主线程。

`DisposeAsync()` 会取消后台循环、失败所有 pending request，并释放 transport。

## 服务端类型

| 类型 | 包 | 主要职责 |
| --- | --- | --- |
| `RpcServerHostBuilder` | `ULinkRPC.Server` | 组合 serializer、connection acceptor、generated binders、keepalive、security 和 server limits。 |
| `RpcSession` | `ULinkRPC.Server` | 管理单个客户端连接，接收 request frame，分发到 handler，发送 response / push。 |
| `RpcServiceRegistry` | `ULinkRPC.Server` | 保存 `(serviceId, methodId)` 到 handler 的分发表。 |

### `RpcServerHostBuilder`

推荐服务端入口使用 `RpcServerHostBuilder`。最小配置必须包含 serializer 和 transport acceptor；如果没有显式配置服务绑定，`Build()` 会尝试从 entry assembly 绑定 codegen 生成的 `AllServicesBinder`。

```csharp
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new JsonRpcSerializer())
    .UseKeepAlive(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45))
    .UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
        20000,
        "/ws",
        ct));

await builder.RunAsync();
```

`UseLimits(...)` 可调整每个 session 的并发请求数、队列长度和 pending accepted connection 数。队列满时，session 会返回 overload 响应，而不是无限积压请求。

### `RpcSession`

`RpcSession` 表示一个已建立的客户端连接。它可以手动注册 handler，也可以接收 `RpcServiceRegistry`。`StartAsync(...)` 启动后台接收循环，`RunAsync(...)` 会启动并等待结束，`StopAsync()` 会停止循环并等待 in-flight request 完成。

`PushAsync<TArg>(serviceId, methodId, arg, ct)` 用于服务端主动向客户端发送 callback push。实际业务代码通常不直接调用它，而是使用 codegen 生成的 callback proxy。

服务实例默认是 session scoped：生成 binder 会通过 `GetOrAddScopedService(...)` 为每个连接复用服务实例。这适合保存登录态、玩家上下文或 callback proxy。

## Transport 和 Serializer 扩展点

| 类型 | 包 | 主要职责 |
| --- | --- | --- |
| `ITransport` | `ULinkRPC.Core` | 发送和接收完整 frame，隐藏 TCP / WebSocket / KCP 差异。 |
| `IRpcSerializer` | `ULinkRPC.Core` | 在 DTO 对象和 payload bytes 之间转换。 |

`ITransport` 的边界是完整 frame，而不是 stream 片段：

```csharp
ValueTask ConnectAsync(CancellationToken ct = default);
ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default);
ValueTask<TransportFrame> ReceiveFrameAsync(CancellationToken ct = default);
```

实现 transport 时要保证上层收到的是完整 frame。TCP 实现需要自己处理粘包 / 拆包；WebSocket 和 KCP 也要把各自的消息边界规整成同一套 frame API。

## 当前缺口

这页仍然是概览，不替代完整 API reference。下一步需要补齐：

- 每个 public member 的参数、返回值、异常和生命周期说明。
- `RpcStatus` 与错误传播语义。
- `TransportSecurityConfig` 的安全模型和非目标。
- 连接断开、重连和 Unity 主线程调度指南。
