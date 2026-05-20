+++
title = "线程模型"
date = 2026-05-12T09:15:00+08:00
+++

ULinkRPC 运行时使用后台 `Task` 处理 receive、push、request dispatch 和 keepalive。它不会自动切回 Unity、团结或 Godot 的主线程。

## 客户端线程模型

`RpcClientRuntime.StartAsync` 会启动：

- receive loop：读取 transport frame，处理 response、push、keepalive ping / pong。
- push loop：从内部 queue 读取 push，并调用已注册的 push handler。
- keepalive loop：启用 keepalive 时发送 ping 并检测 timeout。

push handler 运行在 runtime 的 push loop 上。代码注释已经明确：push handlers 不会 marshal 到 Unity 主线程。

普通 RPC 调用的 `await` continuation 由调用方上下文决定；但 runtime 内部大量使用 `ConfigureAwait(false)`，不能依赖它帮你回到引擎主线程。

## 服务端线程模型

每个 `RpcSession` 有 receive loop。收到 request 后，session 会把请求处理加入 tracked task，并使用两个限制控制压力：

- `MaxConcurrentRequestsPerSession`，默认 64。
- `MaxQueuedRequestsPerSession`，默认 256。

同一个 session 的多个请求可能并发执行。服务实现如果访问共享状态，需要自己加锁、使用 actor / queue，或把状态隔离到 session scope。

生成的 server binder 通常通过 `GetOrAddScopedService` 创建 session-scoped service。同一个 service 实例可能被该 session 的并发请求同时使用，因此它也需要线程安全。

## Unity / 团结主线程责任

不要在 push handler、后台 RPC continuation 或服务回调中直接访问 UnityEngine 对象，除非你已经确认当前在主线程。

推荐做法：

- 在 MonoBehaviour 中持有连接状态机。
- 网络 callback 只写入线程安全队列或记录轻量状态。
- 在 `Update()` 中 drain 队列并更新 UI、GameObject、Scene。
- 对按钮点击、场景退出和对象销毁使用 `CancellationTokenSource` 控制 pending RPC。

## Godot 主线程责任

Godot C# 节点生命周期方法，例如 `_Ready`、`_Process`、`_ExitTree`，在 Godot 主线程上运行。ULinkRPC callback 不保证在这个线程上。

需要更新 Node、SceneTree 或 UI 时，应把网络结果转交给主线程逻辑处理。starter 默认 Godot 测试脚本只做简单连接和打印；真实项目应补连接状态机和主线程派发。

## 当前没有实现的能力

当前仓库没有 Unity main-thread dispatcher、Godot main-thread dispatcher、`SynchronizationContext` 捕获策略、单线程 service actor 模型，或按方法声明串行执行的机制。
