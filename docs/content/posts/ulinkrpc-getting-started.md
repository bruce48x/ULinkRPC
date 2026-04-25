---
title: 用 ULinkRPC.Starter 快速创建一个 Unity / 团结 / Godot 和 .NET 双向通信项目
date: 2026-03-15T12:30:00+08:00
tags:
  - ulinkrpc
  - unity
  - tuanjie
  - godot
  - dotnet
  - rpc
  - websocket
  - json
  - memorypack
categories:
  - Tutorial
---

如果你现在想开始一个新的 ULinkRPC 项目，最直接的方式已经不是手工搭目录、手工建 `Shared`、手工跑 codegen，而是直接用 `ULinkRPC.Starter`。

它会一次性帮你生成：

- `Shared` 共享契约项目
- `Server` 服务端项目和解决方案
- `Client` Unity 2022、团结引擎或 Godot 4.x 客户端骨架
- 默认 `Ping` 契约、服务实现，以及客户端测试入口
- `ULinkRPC.CodeGen` 本地工具清单和两侧生成代码

也就是说，你现在的推荐起步方式是：

**先选 transport 和 serializer，然后让 starter 直接产出一份可运行的最小项目。**

## 前提条件

开始之前，请先安装 **.NET 10 SDK**：

- 下载地址：https://dotnet.microsoft.com/en-us/download/dotnet/10.0

后面的 `dotnet tool install`、`dotnet tool restore`、`dotnet run` 等命令都依赖本机已经可用的 .NET SDK。

## Quick Start

如果你只想最快跑起来，直接照下面做：

1. 安装 starter
2. 生成一份 `websocket + json` 项目
3. 启动服务端
4. 打开客户端
5. 根据引擎完成依赖恢复
6. 运行默认连接测试

对应命令如下：

```bash
dotnet tool install -g ULinkRPC.Starter
ulinkrpc-starter --name MyGame --client-engine unity --transport websocket --serializer json

# 或者
ulinkrpc-starter --name MyGame --client-engine tuanjie --transport websocket --serializer json
cd MyGame
dotnet run --project Server/Server/Server.csproj
```

如果你选的是 Unity 或团结引擎：

- 用 Unity 2022 LTS 或团结引擎打开 `MyGame/Client`
- 等待导入完成
- 执行 `NuGet -> Restore Packages`
- 打开 `Assets/Scenes/ConnectionTest.unity`
- 点击 Play

如果你选的是 Godot：

- 用 Godot 4.x 打开 `MyGame/Client`
- 等待 Godot 生成并恢复 C# 解决方案
- 打开 `Main.tscn`
- 点击 Play

如果你是第一次接入，我建议先不要从 `memorypack` 开始，先把 `websocket + json` 跑通，再升级到更高性能的组合。

最短路径可以记成这一句：

**安装 starter -> 生成项目 -> 启动 Server -> 打开 Client -> 恢复依赖 -> 运行默认测试场景。**

## 先理解最终结构

starter 生成出来的项目固定是三层：

```text
MyGame/
  Shared/
  Server/
    Server.slnx
    Server/
      Server.csproj
  Client/
```

各层职责很明确：

- `Shared/`
  放共享 DTO、RPC 接口，以及 Unity 场景下使用的 UPM 包定义
- `Server/Server/`
  放服务端入口、服务实现、服务端生成代码
- `Client/`
  放 Unity 或 Godot 工程、客户端生成代码，以及默认测试入口

这套结构最重要的点不是“看起来整齐”，而是：

- 契约只有一份
- 服务端和客户端同时引用同一份 Shared
- codegen 永远围绕同一份契约运行

## 安装 starter

先安装全局工具：

```bash
dotnet tool install -g ULinkRPC.Starter
```

如果已经安装过，更新到最新版：

```bash
dotnet tool update -g ULinkRPC.Starter
```

## 生成项目

最常用的命令是：

```bash
ulinkrpc-starter --name MyGame --transport websocket --serializer json
```

你也可以省略参数，进入交互模式：

```bash
ulinkrpc-starter --name MyGame
```

目前可选项是：

- `client-engine`
  - `unity`
  - `tuanjie`
  - `godot`

- `transport`
  - `tcp`
  - `websocket`
  - `kcp`
- `serializer`
  - `json`
  - `memorypack`

例如，生成一个 `WebSocket + MemoryPack` 项目：

```bash
ulinkrpc-starter --name MyGame --client-engine godot --transport websocket --serializer memorypack
```

## 生成后 starter 做了什么

starter 不只是“建几个空目录”，而是会直接做完这些事情：

1. 生成 `Shared/Shared.csproj`、`Shared.asmdef`、`package.json`
2. 生成默认 DTO 和 `IPingService`
3. 生成 `Server/Server/Program.cs` 和 `Services/PingService.cs`
4. 生成 `Server/Server.slnx` 并把 `Shared`、`Server` 项目都加进去
5. 生成对应客户端引擎的工程骨架和测试入口
6. Unity 模式下生成 `manifest.json`、`packages.config`、`NuGet.config`
7. Unity 模式下生成 `Assets/Scenes/ConnectionTest.unity` 和 `EditorBuildSettings.asset`
8. Godot 模式下生成 `project.godot`、`Client.csproj`、`Main.tscn`
9. 自动安装本地 `ULinkRPC.CodeGen`
10. 自动跑 server / client 两侧代码生成
11. 自动 `git init`

所以它的目标不是“给你一个空模板”，而是“给你一个可直接启动的起点”。

## CodeGen 怎么用

starter 第一次生成项目时，会自动帮你安装并跑好 `ULinkRPC.CodeGen`。

所以 `CodeGen` 真正重要的地方，不是在“第一次建项目”，而是在你后续做新功能的时候。

最常见的真实开发顺序其实是这样的：

1. 先在 `Shared/Interfaces/` 里定义新的接口和 DTO
2. 重新运行 `CodeGen`
3. 让 server / client 两侧更新胶水代码
4. 再去补服务端实现
5. 最后在客户端里调用新的 generated API

你可以把它记成一句话：

**Shared 契约变了，就先跑 codegen；胶水代码更新完，再继续写业务逻辑。**

### 一个更实际的例子：新增背包查询功能

假设 starter 默认的 `Ping` 已经跑通了，现在你要开始做第一个真实功能：

- 玩家打开背包界面
- 客户端向服务端请求物品列表
- 服务端返回当前背包内容

那你的第一步，不是先去改 `Server/Program.cs`，也不是先去手写 generated 目录。

第一步应该是先改 `Shared/Interfaces/`。

比如你在 `Shared/Interfaces/` 里新增：

```csharp
namespace Shared.Interfaces
{
    public sealed class InventoryItemDto
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class GetInventoryRequest
    {
        public long PlayerId { get; set; }
    }

    public sealed class GetInventoryReply
    {
        public List<InventoryItemDto> Items { get; set; } = new();
    }
}
```

以及新的 RPC 接口：

```csharp
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces
{
    [RpcService(2)]
    public interface IInventoryService
    {
        [RpcMethod(1)]
        ValueTask<GetInventoryReply> GetInventoryAsync(GetInventoryRequest request);
    }
}
```

写到这里，服务端和客户端都还不能直接用这个新接口，因为两边的胶水代码还没生成。

### 这时候该做什么

这时候就应该立刻重新跑 `CodeGen`。

在 starter 生成的项目里，通常直接这样做：

```bash
cd MyGame
dotnet tool restore
cd Server/Server
dotnet tool run ulinkrpc-codegen -- --contracts "../../Shared" --mode server --server-output "Generated" --server-namespace "Server.Generated"
cd ../../Client
dotnet tool run ulinkrpc-codegen -- --contracts "../Shared" --mode unity --output "Assets/Scripts/Rpc/Generated" --namespace "Rpc.Generated"
```

如果客户端是 Godot，最后一条改成：

```bash
dotnet tool run ulinkrpc-codegen -- --contracts "../Shared" --mode godot --output "Scripts/Rpc/Generated" --namespace "Rpc.Generated"
```

如果你用的是团结引擎，它走的就是 Unity 这条命令。

### 跑完之后会发生什么

跑完以后，`CodeGen` 会根据你刚才写的 `IInventoryService` 和 DTO 自动更新两边的胶水代码。

server 侧会更新：

- `Server/Server/Generated/AllServicesBinder.cs`
- `Server/Server/Generated/InventoryServiceBinder.cs`
- 以及相关 callback proxy（如果你定义了 callback）

unity / 团结侧会更新：

- `Client/Assets/Scripts/Rpc/Generated/RpcApi.cs`
- `Client/Assets/Scripts/Rpc/Generated/` 下对应的 client stub / binder

godot 侧会更新：

- `Client/Scripts/Rpc/Generated/RpcApi.cs`
- `Client/Scripts/Rpc/Generated/` 下对应的 client stub / binder

这一步的意义是：

- 服务端现在知道如何把网络请求路由到 `IInventoryService`
- 客户端现在有了类型安全的调用入口
- 你不需要手写这些重复的 binder / stub / facade

### 然后再去补服务端实现

胶水代码生成完成后，再去 `Server/Server/Services/` 里写业务实现就顺了。

例如新增一个：

```csharp
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Server.Services
{
    public sealed class InventoryService : IInventoryService
    {
        public ValueTask<GetInventoryReply> GetInventoryAsync(GetInventoryRequest request)
        {
            var reply = new GetInventoryReply();
            reply.Items.Add(new InventoryItemDto
            {
                ItemId = 1001,
                Name = "Health Potion",
                Count = 5
            });

            return ValueTask.FromResult(reply);
        }
    }
}
```

到这里，服务端业务逻辑才算真正接上。

### 客户端怎么用新的生成代码

接着你就可以在客户端逻辑里，直接通过生成出来的 API 发请求，而不是自己拼协议包。

概念上它会是这种调用方式：

```csharp
var reply = await _client.Api.Shared.Inventory.GetInventoryAsync(
    new GetInventoryRequest
    {
        PlayerId = 10001
    });

foreach (var item in reply.Items)
{
    Debug.Log($"{item.ItemId} {item.Name} x{item.Count}");
}
```

这里最重要的不是具体命名细节，而是这个工作流：

- 你改的是 `Shared`
- `CodeGen` 生成的是“连接 Shared 和运行时”的胶水
- 服务端实现只关心接口
- 客户端调用只关心生成后的强类型 API

### 什么时候一定要重跑 CodeGen

只要你改了这些内容，就应该立刻重跑：

- 新增 / 删除 / 修改 RPC 接口
- 新增 / 删除 / 修改 DTO
- 修改方法签名、参数、返回值
- 新增 callback 接口

反过来说，如果你只是改服务内部实现，例如：

- `InventoryService` 里换了一套数据库查询
- 客户端 UI 从按钮点击改成页面打开自动刷新

这种不涉及 Shared 契约变化的改动，就不需要重新跑 `CodeGen`。

### 一个很实用的判断方法

你可以问自己一个问题：

**这次改动有没有动 `Shared/Interfaces/` 里的契约定义？**

如果答案是“有”，那下一步就不是继续写别的代码，而是先跑 `CodeGen`。

如果答案是“没有”，那通常可以继续改服务实现或客户端逻辑。

### 你真正应该维护的是什么

后续开发里，真正应该被你手工维护的是：

- `Shared/Interfaces/` 里的接口和 DTO
- `Server/Server/Services/` 里的服务实现
- 客户端自己的业务脚本

而不是：

- `Server/Server/Generated/`
- `Client/Assets/Scripts/Rpc/Generated/`
- `Client/Scripts/Rpc/Generated/`

这些 generated 目录应该被看成“编译产物式的源码”，核心原则就是：

**契约改了，就重新生成；不要手改 generated。**

## 服务端怎么启动

生成完成后，进入项目根目录：

```bash
cd MyGame
dotnet run --project Server/Server/Server.csproj
```

默认示例会启动一个最小 `Ping` 服务。

如果你选的是：

- `websocket`
  默认监听 `ws://127.0.0.1:20000/ws`
- `tcp`
  默认监听 `127.0.0.1:20000`
- `kcp`
  默认监听 `127.0.0.1:20000`

## 客户端怎么启动

如果你用的是 Unity 或团结引擎，用对应编辑器打开：

```text
MyGame/Client
```

首次打开后：

1. 等待编辑器导入项目
2. 等待 `NuGetForUnity` 导入完成
3. 在编辑器菜单执行 `NuGet -> Restore Packages`
4. 打开或确认已自动打开 `Assets/Scenes/ConnectionTest.unity`
5. 点击 Play

默认场景里已经挂好了 `RpcConnectionTester`，会自动发起连接并调用一次 `Ping`。

如果你用的是 Godot：

1. 用 Godot 4.x 打开 `MyGame/Client`
2. 等待 Godot 生成和恢复 C# 工程
3. 打开 `Main.tscn`
4. 点击 Play

默认 `Main.tscn` 上也挂好了 `RpcConnectionTester`，会自动发起连接并调用一次 `Ping`。

## 默认代码长什么样

默认共享契约会生成在：

```text
Shared/Interfaces/
```

例如：

```csharp
namespace Shared.Interfaces
{
    public sealed class PingRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingReply
    {
        public string Message { get; set; } = string.Empty;
        public string ServerTimeUtc { get; set; } = string.Empty;
    }
}
```

如果选择 `memorypack`，starter 也会自动给 DTO 加上对应的 `MemoryPackable` 标记，并处理 Unity 侧所需的 `asmdef` 引用和 `unsafe` 配置。

默认服务实现则在：

```text
Server/Server/Services/PingService.cs
```

Unity 默认连接脚本在：

```text
Client/Assets/Scripts/Rpc/Testing/RpcConnectionTester.cs
```

Godot 默认连接脚本在：

```text
Client/Scripts/Rpc/Testing/RpcConnectionTester.cs
```

这几个文件一起构成了最小可运行闭环。

## 什么时候选 JSON，什么时候选 MemoryPack

如果你只是第一次接入 ULinkRPC，我建议先从：

- `websocket + json`

开始。原因很简单：

- 更容易排错
- 更容易观察请求和响应形态
- Unity 初次接入时问题更少

当你确认整条链路已经稳定后，再切到：

- `websocket + memorypack`
- `tcp + memorypack`
- `kcp + memorypack`

`MemoryPack` 更适合后续追求更高性能或更低负载的时候再上。

## 已知现象：Unity 首次导入 MemoryPack.Generator

如果你生成的是 `memorypack` 项目，Unity 第一次打开时，可能会看到类似这样的 analyzer 引用告警：

```text
Assembly '...MemoryPack.Generator.dll' will not be loaded due to errors:
Unable to resolve reference 'Microsoft.CodeAnalysis'
Unable to resolve reference 'Microsoft.CodeAnalysis.CSharp'
```

当前实际验证结果是：

- 这类告警可能出现在首次导入阶段
- 关闭 Unity 再重新打开一次，通常会消失
- 消失后项目可以正常运行

所以这更像 Unity / NuGetForUnity / Roslyn analyzer 的首次导入时序问题，而不是 starter 生成结果不可用。

如果你遇到它，建议按这个顺序处理：

1. 先完成 `NuGet -> Restore Packages`
2. 等待导入结束
3. 关闭 Unity
4. 重新打开项目

## 接下来该怎么扩展

当 starter 生成的默认 `Ping` 示例已经跑通后，后续开发就按前面的 `Inventory` 例子那条线往前走：

1. 先在 `Shared/Interfaces/` 里定义功能契约
2. 立刻重新运行 `CodeGen`
3. 再补服务端实现和客户端业务接入

日常开发里真正的源头始终是：

**Shared 契约，而不是 generated 目录。**

## 最后总结

现在最推荐的 ULinkRPC 入门方式已经很明确：

1. 用 `ULinkRPC.Starter` 生成项目
2. 先跑通默认 `Ping` 示例
3. 再开始替换成你自己的契约和业务逻辑

这样做的好处是：

- 项目结构统一
- Shared 不会从第一天就失控
- 服务端和客户端引用关系清晰
- codegen 流程固定
- transport / serializer 选择可以明确收敛到模板层

如果你是第一次接入，我建议就按 starter 的默认结构继续往前长，不要先手工改目录。先让流程稳定，再谈抽象和个性化结构。
