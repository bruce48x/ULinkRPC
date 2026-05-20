+++
title = "技术选型指南"
date = 2026-05-12T09:00:00+08:00
+++

ULinkRPC 适合“C# 服务端 + C# 游戏客户端”共享契约的项目。它的核心价值是把 RPC 接口、DTO、生成代码和 transport 抽象收敛到一个固定工作流。

## 适合选择 ULinkRPC 的情况

你的服务端是 .NET，客户端是 Unity、团结引擎或 Godot C#。

你希望 `Shared` 契约成为唯一源头，服务端和客户端都从同一套 C# DTO / interface 生成胶水代码。

你需要在 JSON 和 MemoryPack 之间选择，并希望 transport 可以在 TCP、WebSocket、KCP 之间切换。

你接受“应用层负责连接状态、重连、鉴权、业务错误码和版本策略”的边界。

你希望默认模板能直接生成 server、client、Shared、codegen 和最小连接测试。

## 不适合选择 ULinkRPC 的情况

你的客户端主要不是 C#，例如 TypeScript、C++、Java、Go 或原生移动端。当前仓库没有多语言 schema-first 代码生成。

你需要成熟的服务治理能力，例如服务发现、负载均衡、streaming、deadline propagation、拦截器生态、统一 tracing 和跨语言工具链。这类需求更接近 gRPC 或内部 RPC 平台。

你需要内置账号系统、鉴权 middleware、session resume、自动重连或请求重放。ULinkRPC 当前把这些留给应用层。

你只需要极少量消息，且协议长期不会增长。手写消息分发可能更直接。

## 和手写消息分发相比

手写消息分发的优点是简单、完全可控、没有 codegen 流程。缺点是接口增长后容易出现路由 id、DTO、序列化和调用侧封装不一致。

ULinkRPC 更适合 RPC 方法数量会增长、服务端和客户端都想保留强类型调用入口的项目。代价是必须遵守 Shared 契约、重新 codegen、不要手改 generated 目录。

## 和 schema-first RPC 工具相比

schema-first 工具通常先写 `.proto`、IDL 或 schema，再生成多语言代码。它们适合跨语言、多团队、平台化 API。

ULinkRPC 当前是 C# contract-first：直接写 C# interface 和 DTO。它更贴近 Unity / Godot C# 项目，但不提供跨语言 schema 生态。

## 推荐组合

第一次接入：

```bash
ulinkrpc-starter new --name MyGame --client-engine unity --transport websocket --serializer json
```

Godot：

```bash
ulinkrpc-starter new --name MyGame --client-engine godot --transport websocket --serializer json
```

性能优化阶段再评估 MemoryPack、TCP 或 KCP。不要在没有 benchmark 和上线约束的情况下过早选择复杂组合。

## 决策检查表

- 团队是否愿意让 `Shared` 成为契约唯一源头？
- 客户端是否主要是 C#？
- 是否能接受应用层自己实现重连和鉴权？
- 是否有跨版本 DTO 测试计划？
- 是否会在真实设备和真实网络下做性能测试？

这些问题如果大多数答案是“是”，ULinkRPC 是合理候选；如果不是，先评估手写协议或 schema-first RPC。
