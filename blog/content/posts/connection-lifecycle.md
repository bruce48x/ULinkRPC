+++
title = "连接生命周期"
date = 2026-05-12T09:10:00+08:00
+++

ULinkRPC 把连接生命周期分成两层：底层 `ITransport` 负责连接和 frame I/O，`RpcClientRuntime` / `RpcSession` 负责 RPC request、response、push、keepalive 和关闭。

## 客户端生命周期

生成的 `RpcClient` facade 内部持有 `RpcClientRuntime`。调用 `ConnectAsync` 时，runtime 会：

1. 调用 transport 的 `ConnectAsync`。
2. 初始化 keepalive 状态。
3. 启动 receive loop。
4. 启动 push loop。
5. 如果启用了 keepalive，启动 keepalive loop。

同一个 runtime 只能 start 一次；重复 start 会抛出 `InvalidOperationException`。断线后不要假设同一个实例可以无损复用。应用层应 dispose 当前客户端，创建新的 transport、serializer 和 generated client，再发起重连。

## 服务端生命周期

`RpcServerHost` 使用 `IRpcConnectionAcceptor` 接收连接。每个 accepted connection 会创建一个 `RpcSession`。session 会：

- 调用 accepted transport 的 `ConnectAsync`。
- 启动 receive loop。
- 按请求创建或复用 session-scoped service。
- 在 session 停止时清理 scoped service。
- 如果 `ownsTransport: true`，dispose session 时也 dispose transport。

`RpcServerHostBuilder.UseKeepAlive(...)` 会把 keepalive 配置传给每个新 session。

## 关闭和断线

客户端 `DisposeAsync` 会取消内部循环、让 pending request 失败、关闭 push queue，并 dispose transport。

服务端 `StopAsync` 会取消 session loop，等待 in-flight request 完成，然后清理 session 状态。host 收到取消后会停止 accept loop，并等待已跟踪的连接任务结束。

底层 receive 返回空 frame、transport 抛出断开相关异常、keepalive 超时，都会让对应 loop 结束。客户端和服务端都提供 `Disconnected` 事件，参数为可用的断开原因；正常本地关闭可能是 `null`。

## Keepalive

keepalive 默认关闭。`RpcKeepAliveOptions` 包含：

- `Enabled`
- `Interval`：多长时间没有收到任何 frame 后发送 ping，默认 15 秒。
- `Timeout`：ping 后多长时间仍没有收到入站 frame 就断开，默认 45 秒。
- `MeasureRtt`：是否记录 ping / pong RTT。

只有入站流量证明对端仍然活着；本端发送 frame 不会抑制探测。客户端 keepalive 超时后，`TimedOutByKeepAlive` 会变为 true。

## 重连归属

当前 ULinkRPC 没有内置自动重连。原因是重连通常牵涉登录态、房间状态、场景状态、重放请求、UI 提示和幂等性，这些都属于应用层。

建议应用层实现：

- 统一的连接状态机：`Idle`、`Connecting`、`Connected`、`Disconnecting`、`Disconnected`。
- 每次重连创建新的 transport 和 generated client。
- 对未完成的用户操作给出明确 UI 状态。
- 登录或鉴权成功后再恢复房间、匹配或战斗状态。

## 当前没有实现的能力

当前仓库没有自动重连、请求重放、session resume、离线队列、心跳自适应调参，或跨连接的服务端会话迁移。
