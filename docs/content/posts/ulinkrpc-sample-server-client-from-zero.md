---
title: 用示例项目从 0 搭一个可运行的 ULinkRPC 联机原型
date: 2026-03-18T10:30:00+08:00
tags:
  - ulinkrpc
  - unity
  - dotnet
  - rpc
  - tcp
  - memorypack
categories:
  - Tutorial
---

上一篇 [《用 ULinkRPC 从零搭一个 Unity 和 .NET 双向通信项目》](/ULinkRPC/posts/ulinkrpc-getting-started/) 更像是“你先知道 ULinkRPC 能做什么”。如果你现在真正准备开一个项目，往往还会继续问下面这些问题：

- 共享契约到底放哪？
- 服务端项目怎么建？
- Unity 工程怎么引用共享代码？
- 生成代码要在哪边跑？
- 客户端拿到回调以后，应该怎么把网络消息接到游戏逻辑里？

这篇就不再停留在概念层，而是直接拿两个真实仓库来拆：

- 服务端：<https://github.com/bruce48x/ULinkRPC-Sample-Server>
- 客户端：<https://github.com/bruce48x/ULinkRPC-Sample-Client>

目标也很明确：**带你照着这两个仓库，从 0 搭出一个“Unity 客户端 + .NET 服务端 + 共享契约 + 自动生成胶水代码”的最小可用项目。**

> 先说明一下，这一套示例用的不是上一篇里的 `WebSocket + JSON`，而是：
>
> - 传输层：`TCP`
> - 序列化：`MemoryPack`
> - 服务端：`.NET`
> - 客户端：`Unity`
>
> 这样更贴近“游戏原型 / 实时同步 demo”的真实起步方式。

## 先看最终结构：你真正要搭的是三层

无论 sample 看起来有多少目录，核心其实就三层：

### 1. Shared：共享契约层

这里放：

- RPC 接口
- Callback 接口
- DTO / 同步数据结构

在这组示例里，对应的是服务端仓库里的 `Shared/`，而且 Unity 通过本地包直接引用它。也就是说，**契约不是复制两份，而是共享一份。**

### 2. Server：服务端运行层

这里放：

- ULinkRPC Server runtime
- 传输层配置
- 序列化配置
- 服务实现类
- 服务端生成代码

### 3. Client：Unity 表现层

这里放：

- Unity 场景和游戏逻辑
- ULinkRPC Client runtime
- 客户端生成代码
- 接收服务端回调后的本地更新逻辑

你可以把这套目录关系记成一句话：

**Shared 定义协议，Server 负责处理协议，Client 负责消费协议。**

---

## 第一步：先建服务端仓库，并把 Shared 单独抽出来

示例服务端仓库的顶层结构非常干净：

```text
ULinkRPC-Sample-Server/
├── Shared/
└── ULinkRPC-Sample-Server/
```

为什么要这么拆？因为真实项目里，**共享契约最好从第一天就独立出来**。原因很现实：

- 服务端要引用它
- Unity 客户端要引用它
- 代码生成要扫描它
- 后续如果你拆网关、AI 服务、工具程序，也还是会继续引用它

所以不要把接口直接塞进服务端项目里，后面再搬家会很痛。

### Shared 里到底放什么

这个 sample 的契约集中在 `Shared/Interfaces/IPlayerService.cs` 里。你能看到一套很完整的双向通信定义：

```csharp
[RpcService(1, Callback = typeof(IPlayerCallback))]
public interface IPlayerService
{
    [RpcMethod(1)]
    ValueTask<LoginReply> LoginAsync(LoginRequest req);

    [RpcMethod(2)]
    ValueTask Move(MoveRequest req);
}

[RpcCallback(typeof(IPlayerService))]
public interface IPlayerCallback
{
    [RpcPush(1)]
    void OnMove(List<PlayerPosition> playerPositions);
}
```

这段契约说明了四件非常关键的事：

1. `IPlayerService` 是客户端调用的服务。
2. `LoginAsync` 是登录入口。
3. `Move` 是玩家移动请求。
4. `IPlayerCallback.OnMove` 是服务端主动把最新位置快照推回客户端。

这就是 ULinkRPC 最值得建立的习惯：

**把“请求”和“推送”都写进契约，而不是到了后面再额外补一套事件协议。**

### DTO 也放在 Shared

这个示例里，`LoginRequest`、`LoginReply`、`MoveRequest`、`PlayerPosition` 都和接口放在一起，并且使用 `MemoryPackable` 标记。原因也很直接：

- 这次示例选的是 `MemoryPack` 序列化
- 所以传输对象本身就应该按 `MemoryPack` 的方式声明
- Unity 和 .NET 服务端都吃同一份类型定义

真实项目里你也可以把 DTO 按模块拆文件，但原则不变：

**只要会跨进程传输，就应该属于 Shared。**

---

## 第二步：服务端项目只做两件事：配置 runtime，写实现类

很多人第一次接触 RPC 框架时，会以为服务端入口一定很长。其实这个示例的 `Program.cs` 非常短：

```csharp
await RpcServerHostBuilder.Create()
 .UseCommandLine(args)
 .UseMemoryPack()
 .UseTcp(defaultPort: 20000)
 .RunAsync();
```

这几行其实就是在做一套“运行时拼装”：

- `UseMemoryPack()`：告诉服务端序列化格式是 `MemoryPack`
- `UseTcp(defaultPort: 20000)`：告诉服务端走 TCP，并监听 20000 端口
- `UseCommandLine(args)`：允许你通过命令行覆盖配置

也就是说，**服务端入口本身并不关心业务，只关心 runtime 组合。**

### 真正的业务逻辑写在服务实现里

这个 sample 的亮点不只是“能登录”，而是做了一个很真实的最小玩法闭环：

- 玩家登录后进入一个小地图
- 服务端维护玩家和 AI 的位置状态
- 客户端发送移动请求
- 服务端广播最新位置快照
- 客户端拿快照刷新画面

这部分都在 `Services/PlayerService.cs` 里。

如果你看实现思路，它其实很像真实业务服务：

- `LoginAsync` 里做参数校验，初始化游戏状态，返回 token
- `Move` 里接收输入并更新玩家位置
- 维护一份内存态 `GameState`
- 后台循环驱动 AI 移动
- 每次状态变化后调用 callback，把最新 `PlayerPosition` 列表推回客户端

你会发现一个很重要的设计点：

**客户端并不是请求一次就拿一次“局部结果”，而是服务端统一推“当前世界快照”。**

这套模式很适合：

- 房间内小游戏
- 小规模同步玩法
- 原型验证
- 先跑通联机框架，再逐步细化同步策略

如果你正在从 0 做项目，我建议第一版就先学这个 sample：

- 不要一开始就做复杂状态同步
- 先把“客户端发输入、服务端产快照、客户端应用快照”跑通
- 跑通以后再考虑插值、预测、压缩、分区同步

---

## 第三步：Unity 客户端不要复制 Shared，而是直接引用服务端仓库里的 Shared

这是这两个示例仓库最值得照搬的地方。

客户端仓库里有一个 `.gitmodules`：

```text
[submodule "ulinkrpc-sample-server"]
 path = ulinkrpc-sample-server
 url = https://github.com/bruce48x/ULinkRPC-Sample-Server
```

意思是：**客户端仓库把服务端仓库作为 submodule 拉进来了。**

接着，Unity 的 `Packages/manifest.json` 里又直接这样引用共享包：

```json
"com.ulinkrpc-sample-server.shared": "file:../ulinkrpc-sample-server/Shared/"
```

这一步非常关键，因为它解决了很多项目后期才爆炸的问题：

- 不用手工复制接口文件
- 不会出现客户端契约和服务端契约不一致
- Shared 修改后，Unity 立即能看到
- 代码生成时扫到的就是同一份契约

### 真实项目推荐的做法

如果你也想从 0 开项目，我建议直接按下面这个思路组织：

#### 方案 A：像 sample 一样，用 Git submodule + 本地包引用

适合：

- 小团队
- 原型阶段
- 服务端和客户端同时开发
- 想先把结构跑顺

优点：

- 简单直接
- 契约永远只有一份
- Unity 包管理体验也比较自然

#### 方案 B：把 Shared 单独拆成独立仓库 / 私有包

适合：

- 多个客户端共同使用
- 多个服务共同使用
- 有 CI/CD 和版本发布流程

优点：

- 版本管理更清晰
- 发布流程更稳定
- 多项目复用更舒服

如果你现在只是刚起步，先用 sample 这套 `submodule + file:` 已经完全够了。

---

## 第四步：跑代码生成，不要手写 RPC 胶水代码

很多人做 RPC 项目，最容易浪费时间的地方就是手写：

- service id
- method id
- 回调分发
- binder
- client proxy
- API 入口

ULinkRPC 的正确姿势不是自己维护这些，而是：

**你维护契约，生成器帮你产出客户端和服务端的胶水代码。**

从这两个 sample 的目录就能看出来：

### 服务端生成结果

放在：

```text
ULinkRPC-Sample-Server/ULinkRPC-Sample-Server/Generated/
```

你会看到：

- `AllServicesBinder.cs`
- `PlayerServiceBinder.cs`
- `PlayerCallbackProxy.cs`

服务端的重点产物是：

- 把请求路由到你的 `PlayerService`
- 帮你把 callback 代理出来
- 把服务注册成统一入口

### 客户端生成结果

放在：

```text
Assets/Scripts/Rpc/RpcGenerated/
```

你会看到：

- `PlayerServiceClient.cs`
- `PlayerCallbackBinder.cs`
- `RpcApi.cs`

客户端的重点产物是：

- 生成 `IPlayerService` 对应的调用代理
- 生成 callback 绑定器
- 提供统一 API 入口

所以在运行时你才能直接写出这种代码：

```csharp
_player = _connection.Api.Shared.Player;
var reply = await _player.LoginAsync(...);
```

这正是代码生成带来的价值：

**你用的是接口语义，不是自己拼协议包。**

### 从 0 开项目时，生成目录建议一开始就约定清楚

建议直接固定好：

- 服务端：`ServerProject/Generated`
- Unity：`Assets/Scripts/Rpc/RpcGenerated`

然后把这两个目录都视为“生成产物目录”，遵守两个原则：

1. 可以提交到仓库，方便别人拉下来直接运行。
2. 但不要手改，真正源头永远是 Shared 契约。

---

## 第五步：在 Unity 里建立一层“RPC 接入脚本”，不要让业务逻辑直接散落到处连网

这组 sample 在客户端给了你两个非常实用的参考层次。

### 层次 1：`RpcConnectionTester`

`Assets/Scripts/Rpc/RpcConnectionTester.cs` 更像一个最小接线样板，做的事情包括：

- 创建 `RpcClient`
- 配置 `TcpTransport`
- 配置 `MemoryPackRpcSerializer`
- 绑定 callback
- `ConnectAsync`
- 调 `LoginAsync`
- 周期性发送 `Move`

这类脚本很适合你在项目初期做两件事：

- 验证联通性
- 隔离“网络接线”和“具体玩法”

也就是说，**先证明链路通，再接玩法。**

### 层次 2：`DotArenaGame`

`Assets/Scripts/Gameplay/DotArenaGame.cs` 则演示了 RPC 如何接入真实游戏逻辑。

它的核心思路非常值得复用：

1. 组件启动时建立连接并登录。
2. 收到 `OnMove(List<PlayerPosition>)` 后，不立即在回调里直接乱改场景对象。
3. 而是先缓存一份快照。
4. 再在 Unity 主线程节奏里应用快照，刷新画面。

这一步为什么重要？因为真实项目里，网络层和表现层最好不要直接硬耦合。更稳妥的结构通常是：

- RPC 回调层：负责接消息
- 状态层：负责缓存最新状态
- 表现层：负责按 Unity 生命周期刷新场景

这样后面你要做：

- 插值
- 延迟补偿
- 状态回放
- UI 状态展示
- 多模块拆分

都会容易很多。

---

## 第六步：把“从 0 搭建”的实际顺序固定下来

如果你今天准备照着 sample 自己开一个新项目，我建议按下面顺序，不要跳步。

### 1. 先建服务端解决方案

先建立：

- 一个服务端启动项目
- 一个 `Shared` 项目

不要先开 Unity，再去想服务端怎么组织。因为 RPC 契约通常应该从服务端主导开始长出来。

### 2. 先写共享契约，再写服务实现

先写：

- `IXXXService`
- `IXXXCallback`
- DTO

再写：

- `XXXService` 实现类

不要反过来先把业务逻辑写满，再临时抽接口。那样最后很容易把内部模型和传输模型缠在一起。

### 3. 先把服务端单独跑起来

至少做到：

- 服务端能启动
- 监听端口正常
- 依赖包正确
- 生成代码无报错

这时候哪怕 Unity 还没接，服务端工程也应该已经是稳定可启动的。

### 4. 再建 Unity 工程，并接入 Shared

这一步做的不是玩法，而是基础设施：

- 引入 Shared
- 引入 ULinkRPC Client 所需包
- 跑一次代码生成
- 在 Unity 里确认生成代码可编译

### 5. 先写最小连接测试，再写玩法

也就是先有一个 `RpcConnectionTester` 级别的入口，确认：

- 能连上
- 能登录
- 能收到 callback

然后再去写真正的 `DotArenaGame` 或你的业务组件。

### 6. 最后再考虑“更像正式项目”的抽象

比如：

- 连接管理器
- 重连策略
- 心跳
- 房间管理
- 多服务模块拆分
- 配置化 endpoint
- 生产环境日志

这些都很重要，但**都应该建立在最小链路已经稳定跑通之后。**

---

## 第七步：照着 sample，你可以直接复用的项目模板

如果把这篇教程浓缩成一份可复制的骨架，大概就是这样：

```text
MyGame.Server/
├── Shared/
│   ├── Interfaces/
│   │   └── IGameService.cs
│   ├── Shared.csproj
│   └── package.json
└── MyGame.Server/
    ├── Generated/
    ├── Services/
    │   └── GameService.cs
    ├── Program.cs
    └── MyGame.Server.csproj

MyGame.Client/
├── Packages/
│   └── manifest.json
├── Assets/
│   ├── Packages/
│   └── Scripts/
│       ├── Rpc/
│       │   ├── RpcGenerated/
│       │   └── RpcConnectionTester.cs
│       └── Gameplay/
│           └── GameEntry.cs
└── .gitmodules
```

### 这个骨架里每个目录的职责

#### `Shared/Interfaces`
只放契约和 DTO。

#### `Server/Services`
只放接口实现，不要把契约再定义一遍。

#### `Server/Generated`
只放服务端生成代码。

#### `Client/Assets/Scripts/Rpc`
只放连接、回调绑定、生成代码。

#### `Client/Assets/Scripts/Gameplay`
只放游戏表现和玩法。

一旦你从第一天就这样拆，后面你想：

- 新增 `IInventoryService`
- 新增 `IMatchService`
- 新增 `IChatService`

都只是按模块继续扩展，不会把工程结构搞乱。

---

## 第八步：真实项目里最容易踩的坑

最后再补几条非常现实的经验，这些基本都是“从 sample 变项目”时最容易出的问题。

### 1. Shared 不要复制粘贴

最危险的做法就是：

- 服务端一份接口
- Unity 再复制一份接口

一开始看起来快，后面一定出同步问题。要么像 sample 一样直接本地引用，要么做独立共享包。

### 2. 生成代码不要手改

你真正维护的是契约，不是生成结果。手改生成文件，下一次重生成功能就没了。

### 3. 业务层不要直接到处 new `RpcClient`

最好集中在一层连接入口里管理。sample 里已经给了不错的信号：先有独立的 `RpcConnectionTester`，再让 `DotArenaGame` 消费连接后的能力。

### 4. 回调里不要直接做太重的 Unity 场景操作

更稳妥的做法是：

- 回调里收消息
- 缓存状态
- 在 `Update` 或你自己的主线程调度点应用

sample 的 `DotArenaGame` 已经是这个思路。

### 5. 第一版同步不要过度设计

这个 sample 用“服务端维护状态 + 回调广播快照”就已经足够让一个联机原型跑起来。很多项目最大的问题不是同步策略不够高级，而是第一版根本没先跑通。

---

## 最后总结：这两个 sample 最值得你抄的，不是代码细节，而是项目组织方式

如果只让我总结一条，这篇教程最想传达的是：

**ULinkRPC 真正省事的地方，不只是“RPC 调用很方便”，而是它让你可以围绕共享契约来组织整个项目。**

结合这两个 sample，你可以直接把启动顺序记成下面 8 步：

1. 建服务端仓库。
2. 把 Shared 独立出来。
3. 在 Shared 里定义服务接口、回调接口和 DTO。
4. 跑代码生成。
5. 在服务端实现业务类并启动 runtime。
6. 在 Unity 里通过本地包引用 Shared。
7. 在客户端接入 `RpcClient`、生成代码和 callback。
8. 把 callback 接到真实游戏逻辑。

这样做的结果是：

- 契约清晰
- 前后端一致
- 双向通信天然成立
- 连接层和玩法层职责分明
- 项目后面扩模块也不容易乱

如果你刚好正准备做一个小型联机原型，我建议你直接把这两个仓库拉下来，按这篇文章的顺序自己重新搭一遍。**不要只看代码，要按“为什么这么分层、为什么这样组织目录、为什么共享一份契约”去理解它。**

当你真的亲手搭完一次之后，ULinkRPC 的工作流就会非常顺。
