+++
title = "DTO 版本演进"
date = 2026-05-12T09:30:00+08:00
+++

ULinkRPC 的契约源头是共享 C# 接口和 DTO。codegen 根据 `[RpcService]`、`[RpcMethod]` 以及 DTO 类型生成两端胶水代码。版本演进的核心原则是：先保持 wire shape 兼容，再滚动部署，再删除旧字段或旧方法。

## 稳定 ID 不要复用

`[RpcService(id)]` 和 `[RpcMethod(id)]` 是协议路由的一部分。不要复用已经发布过的 service id 或 method id。删除方法后，也应保留该 id 的历史记录，避免旧客户端把请求路由到新的含义上。

CodeGen 会拒绝非正数 id、重复 service id、同一 service 内重复 method id，以及 callback interface 内重复 push id。`ULinkRPC.Core` 包也会携带 analyzer，让这些错误在普通 C# 编辑和构建阶段提前暴露。

方法签名变化会影响生成代码和 payload 类型。对已发布方法，优先新增方法 id，例如 `GetInventoryV2Async`，等旧客户端下线后再清理旧方法。

## JSON DTO

`JsonRpcSerializer` 基于 `System.Text.Json`，并强制 `IncludeFields = true`。这意味着属性和字段都可能进入 JSON payload。

相对安全的变更：

- 新增可选字段或属性，并提供默认值。
- 让服务端容忍客户端没有发送新字段。
- 保持现有字段名、类型和含义不变。

高风险变更：

- 重命名字段或属性。
- 改变字段类型，例如 `int` 改成 `string`。
- 把可缺省字段改成业务必填。
- 删除旧客户端仍会发送或读取的字段。

JSON 更适合早期联调和排错，因为 payload 更容易观察；但兼容性仍然取决于 DTO 设计和两端 serializer options。

## MemoryPack DTO

starter 在 MemoryPack 模式下会给默认 DTO 添加 `[MemoryPackable]` 和 `[MemoryPackOrder(n)]`。MemoryPack 的二进制格式更依赖成员顺序和标记，演进时要更保守。

建议：

- 给可持久演进的成员保留稳定 `MemoryPackOrder`。
- 新字段追加到末尾，使用新的 order。
- 不要重排已有 order。
- 不要把已有 order 复用于另一个含义。
- 对需要灰度的破坏性变更，新增 DTO 或新增 RPC 方法。

当前仓库没有为 DTO 演进提供额外兼容层；MemoryPack 的具体兼容规则以 MemoryPack 本身为准。发布前应写跨版本序列化测试，用旧 DTO payload 喂给新 DTO，反向也要覆盖。

## 灰度发布顺序

推荐发布顺序：

1. 服务端先兼容旧 DTO 和新 DTO，或者新增 V2 方法但保留旧方法。
2. 客户端逐步升级到新字段或新方法。
3. 观察线上旧版本占比和服务端日志。
4. 确认旧客户端不再请求后，才删除旧字段、旧方法或旧 handler。

如果客户端是游戏包，旧版本存活时间通常比服务端长。不要假设所有客户端会同步升级。

## 当前没有实现的能力

当前仓库没有 schema registry、自动 DTO diff 检查、跨版本兼容测试生成器、服务端按客户端版本自动路由，或协议级 deprecation 标记。这些规则需要团队在契约评审和 CI 中执行。
