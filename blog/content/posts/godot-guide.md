+++
title = "Godot 接入指南"
date = 2026-05-12T09:40:00+08:00
+++

ULinkRPC.Starter 已经支持生成 Godot 4.x C# 客户端。Godot 路径和 Unity 路径共用同一套 `Shared` 契约、服务端项目和 Roslyn Source Generator 流程。

## 创建 Godot 项目

推荐先从 WebSocket + JSON 跑通：

```bash
dotnet tool install -g ULinkRPC.Starter
ulinkrpc-starter new --name MyGame --client-engine godot --transport websocket --serializer json
cd MyGame
dotnet run --project Server/Server/Server.csproj
```

客户端在：

```text
MyGame/Client
```

用 Godot 4.x 打开该目录，等待 Godot 生成并恢复 C# 解决方案，打开 `Main.tscn`，点击 Play。

## 生成的 Godot 结构

starter 会生成：

```text
Client/
  project.godot
  Client.csproj
  Main.tscn
  Scripts/
    Rpc/
      Testing/
        RpcConnectionTester.cs
```

`Client.csproj` 引用 `../Shared/Shared.csproj`，并按所选 transport / serializer 添加 NuGet 包。Godot 客户端 RPC glue 由 `ULinkRPC.Analyzers` 在编译期生成。

## Godot SDK 和 NuGet

starter 会尝试寻找本机 Godot Mono SDK 的 `Godot.NET.Sdk.*.nupkg`。如果找到了，会生成 `NuGet.config` 指向本地 Godot 包源和 nuget.org。

如果恢复失败，需要确认：

- 安装的是 Godot .NET / Mono 版本，而不是纯 GDScript 版本。
- `Client.csproj` 里的 `Godot.NET.Sdk` 版本和本机 Godot 匹配。
- 本地 Godot `GodotSharp/Tools/nupkgs` 路径可作为 NuGet source 使用。

## 日常开发流程

契约仍然只改 `Shared/Interfaces/`。修改 RPC 接口或 DTO 后，正常构建 Godot C# 项目即可触发 source generator：

```bash
dotnet build Client/Client.csproj
```

Godot 侧生成代码是编译器输出，不需要项目内 `Generated/` 源码目录。服务端实现放在 `Server/Server/Services/`，Godot 业务脚本放在 `Client/Scripts/` 下你自己的目录中。

`ulinkrpc-starter codegen` 只作为旧项目迁移和排障入口保留。

## Transport 和 serializer

Godot starter 当前支持 `tcp`、`websocket`、`kcp`，以及 `json`、`memorypack`。如果第一次接入，先用 `websocket + json`。确认连接、codegen 和服务端实现稳定后，再切换到 MemoryPack 或其他 transport。

MemoryPack 模式下，Shared DTO 会包含 MemoryPack 标记，相关依赖由 starter 写入项目。DTO 版本演进需要更谨慎，详见 [DTO 版本演进](/ULinkRPC/posts/dto-versioning/)。

## 线程和生命周期

默认 `RpcConnectionTester` 用 `_Ready()` 发起连接，并在 `_ExitTree()` 中触发 shutdown。真实项目需要补连接状态机，不要让多个按钮重复创建连接。

ULinkRPC 的后台 callback 不保证在 Godot 主线程。需要更新 Node、SceneTree 或 UI 时，把结果转交给主线程逻辑处理。

## 当前限制

Godot guide 当前基于 starter 生成路径和仓库样例。仓库没有提供 Godot 专属主线程 dispatcher、编辑器插件、自动重连框架，或 Godot 导出平台矩阵验证报告。
