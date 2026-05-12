+++
title = "Guides"
+++

这里是面向真实项目接入和上线前判断的专题指南。入门教程负责把默认 `Ping` 示例跑通；这些指南负责回答接下来要做的工程决策。

## 上线前必读

- [错误处理](/ULinkRPC/guides/error-handling/)：`RpcStatus`、服务端异常传播、响应错误 payload，以及客户端会看到的失败形态。
- [安全模型](/ULinkRPC/guides/security-model/)：`TransportSecurityConfig` 能保护什么、不能保护什么，以及它和 TLS / WSS 的关系。
- [连接生命周期](/ULinkRPC/guides/connection-lifecycle/)：connect、disconnect、keepalive、shutdown，以及重连责任边界。
- [线程模型](/ULinkRPC/guides/threading-model/)：后台循环、push callback、Unity / Godot 主线程责任，以及用户代码同步要求。

## 契约和性能

- [DTO 版本演进](/ULinkRPC/guides/dto-versioning/)：JSON 与 MemoryPack 的兼容性差异、字段增删建议、灰度约束。
- [性能调优](/ULinkRPC/guides/performance-tuning/)：serializer / transport 选择、keepalive 成本、payload 尺寸，以及当前 benchmark 状态。
- [技术选型指南](/ULinkRPC/guides/selection-guide/)：什么时候选 ULinkRPC，什么时候不选，以及和手写消息分发、schema-first RPC 工具的边界。

## 引擎接入

- [Godot 接入指南](/ULinkRPC/guides/godot-guide/)：使用 starter 生成 Godot 4.x C# 客户端、恢复依赖、运行默认场景和继续开发。
