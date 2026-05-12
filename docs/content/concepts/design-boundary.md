+++
title = "设计边界"
+++

ULinkRPC 的职责边界是通信框架，而不是完整的应用服务器框架。

框架负责：

- transport 集成和 frame I/O
- frame 编码、压缩、加密和限制
- session 生命周期
- request / response / push 分发
- keepalive 和连接关闭语义
- serializer 边界

应用层负责：

- 用户登录、账号系统和 token 生命周期
- 请求级授权和业务权限
- 业务错误码和可恢复失败
- 自动重连、状态恢复和请求重放
- DTO 版本策略和灰度发布
- Unity、团结或 Godot 主线程派发

## 认证和授权

`TransportSecurityConfig` 可以提供 frame 级压缩和对称加密，但它不验证远端身份，也不内建用户、角色、租户、资源归属或策略系统。

认证可以在应用接入层完成，例如登录 RPC、外部 token 校验或 gateway 注入的身份上下文。授权应留在业务层，因为访问规则依赖具体领域模型。

## 为什么不内建授权策略

请求级授权看起来可以做成 middleware，但实际规则通常依赖业务语义：

- 谁拥有这个资源
- 当前角色能否执行这个动作
- 房间、战斗、队伍或租户状态是否允许
- 操作是否幂等、是否可重试
- 客户端版本是否仍然允许调用旧方法

这些信息不属于底层 RPC runtime。ULinkRPC 应该把调用正确、安全地送到服务实现，而不是把业务策略固化进通信层。

## 和生产指南的关系

上线前应把下面几类策略放在应用层或上层框架中：

- [错误处理](/ULinkRPC/guides/error-handling/)
- [安全模型](/ULinkRPC/guides/security-model/)
- [DTO 版本演进](/ULinkRPC/guides/dto-versioning/)
- [连接生命周期](/ULinkRPC/guides/connection-lifecycle/)
- [线程模型](/ULinkRPC/guides/threading-model/)
