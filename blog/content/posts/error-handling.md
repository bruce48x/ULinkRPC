+++
title = "错误处理"
date = 2026-05-12T09:20:00+08:00
+++

这页描述当前 ULinkRPC 运行时已经实现的错误语义。它不是业务错误码设计指南；业务层仍然应该在自己的 DTO 里表达可预期的失败，例如登录失败、背包空间不足、房间不存在。

## 协议状态

RPC 响应 envelope 使用 `RpcStatus` 表示框架层结果：

- `Ok = 0`：服务方法成功返回，payload 是返回 DTO，`void` 返回值使用空 payload。
- `NotFound = 1`：服务端找不到对应的 `serviceId:methodId` handler。
- `Exception = 2`：服务端 handler 执行失败、handler 返回了 null 响应，或服务端会话请求队列已满。

这些状态只覆盖框架层。不要把可恢复的业务失败映射成服务端异常；更稳定的做法是返回业务 DTO，例如 `LoginReply { Success, ErrorCode, Message }`。

## 服务端异常传播

当前服务端不会把原始异常类型、堆栈、内部消息直接传给客户端。`ServerRequestDispatcher` 会记录服务端日志，然后向客户端返回：

```text
RpcStatus.Exception
ErrorMessage = "RPC handler failed."
```

找不到 handler 时，错误消息会包含缺失的 `serviceId:methodId`。请求队列满时，状态也是 `Exception`，错误消息为服务端过载提示。

这意味着客户端不能依赖服务端异常类型做分支。需要客户端感知的失败必须进入返回 DTO。

## 客户端失败模式

生成的客户端最终调用 `RpcClientRuntime.CallAsync`。当前行为是：

- 收到非 `Ok` 响应时抛出 `InvalidOperationException`，消息包含 `RpcStatus` 和响应 `ErrorMessage`。
- 请求的 `CancellationToken` 被取消时，pending request 会被取消。
- 连接断开、transport 关闭、keepalive 超时等会让 receive loop 结束，并让 pending request 以断开原因失败。
- dispose 客户端时，pending request 会收到 `ObjectDisposedException`。

客户端需要把“RPC 调用失败”和“业务返回失败”分开处理。前者通常意味着连接、协议、服务端 handler 或部署问题；后者是正常游戏流程的一部分。

## 推荐实践

在业务 DTO 中定义可预期错误，不要用异常表示正常分支。

给每个用户触发的 RPC 传入合理的 `CancellationToken`，例如界面关闭、场景切换或对象销毁时取消。

监听 `RpcClientRuntime.Disconnected` 或生成客户端暴露的 dispose / reconnect 流程，断线后由应用层决定是否创建新客户端并重连。

服务端 handler 内记录业务上下文，但不要把敏感信息放进返回 DTO 或抛出的异常消息。

## 当前没有实现的能力

当前仓库没有通用业务错误码协议、自动重试策略、异常类型到客户端类型的映射、分布式 tracing 集成，或按方法配置的超时策略。需要这些能力时，应在应用层封装。
