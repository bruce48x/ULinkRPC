+++
title = "API 稳定性路线图"
date = 2026-05-15T00:00:00+08:00
+++

ULinkRPC 当前适合进入 **soft freeze**：主流接入路径、wire protocol 方向和包边界已经基本稳定，但还不适合宣布完整的 hard freeze 或 1.0 API 冻结。

这篇文档记录当前判断和后续优化方向。它不是一次性完成清单，而是后续 release 前评估 breaking change 的依据。

## 当前判断

可以优先稳定的部分：

- C# contract-first 工作流：`[RpcService]`、`[RpcMethod]`、`[RpcCallback]`、`[RpcPush]`
- generated client facade、server binder 和 callback binder 的基本使用方式
- `ITransport`、`IRpcSerializer`、`RpcClientOptions`、`RpcServerHostBuilder` 这些主入口
- TCP、WebSocket、KCP、Loopback transport 的包边界
- JSON 和 MemoryPack serializer 的替换边界
- request / response / push / keepalive 的基本 wire 语义

暂不 hard freeze 的部分：

- 低层 frame/envelope/session 类型的 public 承诺边界
- RPC 错误模型和客户端异常类型
- push callback 注册和注销模型
- runtime、transport、session 的重启和复用语义
- generated facade 的命名规则和冲突处理
- server-side advanced API，例如 `RpcSession` 构造重载、`RpcServiceRegistry` 和低层 handler delegate

## API 分层

后续文档和 release note 应把 public API 分成三层。

### Stable API

这层面向普通用户，进入 hard freeze 后应尽量只做兼容新增。

- contract attributes
- starter 推荐的 client/server 初始化方式
- generated `RpcClient` facade 的生命周期
- `RpcClientOptions`
- `RpcServerHostBuilder`
- transport 构造入口
- serializer 构造入口

### Generated-support API

这层主要服务 codegen 输出。用户可以看到，也可能在高级场景下调用，但它的兼容性应跟 `ULinkRPC.CodeGen` 版本绑定。

- `IRpcClient`
- `RpcMethod<TArg, TResult>`
- `RpcPushMethod<TArg>`
- `RpcGeneratedServicesBinderAttribute`
- generated server binder 使用的 registry 和 handler 入口

这层发生 breaking change 时，必须明确要求用户重新运行 codegen，并避免出现新版 runtime 配旧版 generated code 的隐式失败。

### Advanced API

这层适合 transport、serializer、测试工具和自定义 host 集成。它可以保持 public，但 hard freeze 前要明确哪些是长期承诺，哪些仍可能调整。

- `TransportFrame`
- `RpcEnvelopeCodec`
- envelope/frame DTO
- `RpcSession`
- `RpcServiceRegistry`
- `IRpcConnectionAcceptor`
- `TransformingTransport`
- `TransportSecurityConfig`

如果某个类型只是为了包内协作或测试而 public，应优先收窄可见性；如果确实要保留 public，应补齐文档和契约测试。

## 冻结前优化项

### 1. 明确 public 承诺边界

当前 API Reference 会把较多低层类型列为公开 API。冻结前应决定：

- 哪些类型是用户稳定入口
- 哪些类型只是 generated code 支撑面
- 哪些类型属于 advanced integration
- 哪些类型可以改成 internal 或隐藏在更窄的 facade 后面

目标不是减少所有 public 类型，而是避免“临时 public”被用户误认为长期稳定承诺。

### 2. 强化错误模型

当前 `RpcStatus` 只有 `Ok`、`NotFound`、`Exception`，客户端默认把非 OK 响应转成 `InvalidOperationException`。

冻结前建议引入明确的框架异常类型，例如 `RpcException`，至少包含：

- `RpcStatus Status`
- `string? ErrorMessage`
- 可选的 request、service、method 诊断信息

同时评估是否需要拆出框架级状态，例如 overloaded、decode failure、bad request。业务错误仍应留在应用 DTO 或业务返回模型中，不应强行塞进底层 runtime。

### 3. 重新审视 push callback API

当前 callback 注册是一次性 `RegisterPushHandler(..., Action<T>)`。冻结前需要决定是否支持：

- 注销 handler
- async handler
- 重复注册策略
- handler 异常是否可观测
- Unity、团结、Godot 主线程派发是否只保留为应用层责任

如果要把注册返回值改成 `IDisposable`，或新增 `Func<T, ValueTask>` 形态，应在 hard freeze 前完成。

### 4. 固化生命周期语义

当前文档已经建议断线后重建 generated client、runtime、transport 和 options。冻结前应进一步明确：

- `RpcClientRuntime` 是否 single-use
- `RpcSession` 是否允许 stop 后 restart
- accepted server transport 的 `ConnectAsync` 应是初始化还是连接动作
- `ITransport.IsConnected` 是诊断信号还是强一致状态
- dispose、remote close、keepalive timeout 的事件和 pending request 行为

这些语义一旦被用户依赖，后续修改成本很高。

### 5. 稳定 generated facade 命名规则

generated `RpcApi` 当前根据 contract namespace 和 service interface 推导 group/property 名称，并用数字后缀解决冲突。

冻结前应决定是否需要显式命名能力，例如 service/group alias attribute。否则 generated API 的命名规则本身就是长期兼容承诺。

### 6. 保持 Unity 依赖约束准确

`System.Threading.Channels` 是当前 runtime 和 Unity samples 已采用的显式依赖，应保留在允许列表中。

仍应避免 Unity client 依赖以下能力：

- `System.IO.Pipelines`
- `System.Reflection.Emit`
- runtime code generation
- JIT-only API

新增依赖前应确认 Unity 2022 LTS、iOS、IL2CPP、HybridCLR 的实际兼容性。

## 分阶段计划

### Soft freeze 阶段

当前阶段应优先保证：

- wire protocol 不做无迁移路径的破坏性变更
- starter 生成路径稳定
- generated code 与 runtime 版本匹配
- 主入口 API 只做兼容新增
- breaking change 必须在 changelog 里显式写出升级方式

### Hard freeze 前

进入 1.0 或类似稳定承诺前，应完成：

- API 分层文档
- 错误模型定稿
- callback 注册模型定稿
- 生命周期语义文档和契约测试
- generated facade 命名规则定稿
- 低层 public 类型的保留、收窄或标注

### Hard freeze 后

冻结后仍可继续优化：

- 性能和分配优化
- transport 健壮性
- starter 模板体验
- 文档和示例
- 兼容新增 API

但应避免修改既有主入口签名、generated API 形状和 wire protocol。确需 breaking change 时，应走 major version、迁移说明和兼容窗口。

## Release 检查问题

每次准备发布涉及 runtime、codegen 或 transport 的版本前，至少检查：

- 是否改变了 generated code 需要调用的 runtime API？
- 是否要求用户重新运行 codegen？
- 是否改变了 wire frame、service id、method id、request id 或 payload 语义？
- 是否改变了断线、dispose、pending request、keepalive timeout 的行为？
- 是否改变了 generated facade 的类型名、namespace、group 或属性名？
- 是否新增 Unity 侧依赖？是否验证 IL2CPP 兼容？
- 是否需要在 changelog 写明 breaking change 或迁移步骤？

这份路线图应随着实现推进持续更新。完成一项冻结前优化后，应把它从风险项转为已稳定约定，并补充对应 reference 文档或测试。
