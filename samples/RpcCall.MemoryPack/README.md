# RpcCall.MemoryPack (Full Tutorial)

完整 RPC 示例，包含压缩与加密。

## 结构

- `RpcCall.MemoryPack.Server`：.NET 10 TCP 服务端
- `RpcCall.MemoryPack.Unity`：Unity 2022 LTS 客户端

## 快速开始

1. 运行服务端

```bash
cd samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Server
dotnet build
dotnet run --project RpcCall.MemoryPack.Server
```

也可以在仓库根目录直接执行，默认会先生成代码再构建：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.MemoryPack
```

直接启动服务端：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.MemoryPack -Run
```

重新生成客户端/服务端代码：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.MemoryPack -SkipBuild
```

2. 打开 Unity 项目

打开 `samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity`，进入场景 `Assets/Scenes/TcpConnectionTest.unity`，点击 Play。

默认会自动连接 `127.0.0.1:20000`，完成 `Login` 后持续调用 `IncrStep()`，同时接收服务端通过 `IPlayerCallback.OnNotify` 推送的回调消息。

## 多连接示例

`RpcConnectionTester` 现在会在一个场景里管理多个独立连接，并让每个连接按固定间隔持续调用 `IPlayerService.IncrStep()`。

- `Connection Count`：同时建立的连接数
- `Request Interval Seconds`：每个连接调用 `IncrStep()` 的间隔

服务端会为每个连接创建各自的 `PlayerService` 实例，因此 `IncrStep()` 的返回值会按连接分别递增，而不会彼此共享计数。
