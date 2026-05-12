+++
title = "性能调优"
+++

这页只描述当前仓库能支撑的调优方向。项目目前没有发布正式 benchmark 数字；不要把 serializer 或 transport 的性能结论写成绝对值。上线前应在自己的 payload、设备、网络条件和引擎版本下测量。

## 先选可观察性，再选性能

首次接入建议使用 `websocket + json`。JSON payload 更容易排查 DTO 形态、字段名和服务端行为。链路稳定后，再考虑切换到 `memorypack` 或其他 transport。

追求性能时，优先测这些指标：

- 单次 RPC 往返延迟。
- 每秒请求数。
- payload 序列化后的大小。
- Unity / Godot 客户端 GC 分配。
- 服务端 CPU、内存和并发请求排队。
- 弱网、丢包、断线和重连体验。

## Serializer 取舍

`JsonRpcSerializer` 使用 `System.Text.Json`，并启用 `IncludeFields`。它适合调试和早期联调，但 payload 通常更大，序列化成本也更容易受到字段名和对象结构影响。

`MemoryPackRpcSerializer` 使用 MemoryPack，适合更紧凑的二进制 payload 和性能优化阶段。代价是 DTO 标记和版本演进更严格，排错也不如 JSON 直观。

不要在没有测量的情况下切换 serializer。先记录真实 DTO 和调用频率，再比较。

## Transport 取舍

当前 starter 支持 `tcp`、`websocket`、`kcp`：

- WebSocket：适合需要 HTTP/WebSocket 基础设施、代理或浏览器式网络环境的项目。starter 默认路径是 `/ws`。
- TCP：适合直接长连接场景，部署简单，适合内网或可控网络。
- KCP：适合希望基于 UDP 做低延迟交互的场景，但更依赖网络环境和参数验证。

`LoopbackTransport` 更适合本地测试，不是生产网络 transport。

## Keepalive 成本

keepalive 默认关闭。启用后，空闲连接会按 `Interval` 发送 ping，并在 `Timeout` 内没有收到任何入站 frame 时断开。它会增加少量 frame、timer 和后台任务成本，但能更快发现半开连接。

移动端和弱网场景不要把 interval 设置得过短。过短的心跳会增加电量、流量和服务端压力。

## Payload 尺寸

优先减少高频 RPC 的 payload：

- 避免在高频方法中发送完整大对象。
- 对列表做分页、增量或版本号同步。
- 避免把日志、调试文本或重复字段放进生产 DTO。
- 对可压缩的大 payload，可以评估 `EnableCompression`，但要测 CPU 成本和安全边界。

`TransportSecurityConfig.MaxDecompressedFrameBytes` 用于限制解压后的 frame 大小。生产环境不要无限制接收大 payload。

## 服务端压力限制

`RpcServerLimits` 当前提供：

- `MaxConcurrentRequestsPerSession`，默认 64。
- `MaxQueuedRequestsPerSession`，默认 256。
- `MaxPendingAcceptedConnections`，默认来自连接接入默认值。

队列满时服务端会返回 `RpcStatus.Exception` 和过载消息。调大这些值前先确认服务方法是否会阻塞、是否访问共享锁、是否会制造更多内存压力。

## 当前 benchmark 状态

当前仓库没有公开的跨 transport / serializer benchmark 报告。性能相关文档只能给出测量方向，不能声称某组合在所有场景下更快。
