# RpcCall.Json (Minimal Tutorial)

最简 RPC 示例，基于 WebSocket 传输 + JSON 序列化。
仓库里与 WebSocket 相关的 sample 示例和传输测试现在都集中在这里。

## 结构

- `RpcCall.Server`：.NET 10.0 服务端
- `RpcCall.Unity`：Unity 2022 LTS 客户端

## 快速开始

1. 运行服务端

```bash
cd samples/RpcCall.Json/RpcCall.Json.Server
dotnet build
dotnet run --project RpcCall.Json.Server
```

也可以在仓库根目录直接执行，默认会先生成代码再构建：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json
```

直接启动服务端：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -Run
```

重新生成客户端/服务端代码：

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Json -SkipBuild
```

2. 打开 Unity 项目

打开 `samples/RpcCall.Json/RpcCall.Json.Unity`，进入场景 `Assets/Scenes/TcpConnectionTest.unity`，点击 Play。

默认会自动建立多个 WebSocket 连接到 `ws://127.0.0.1:20000/ws`。每个连接会先执行 `Login`，然后按固定间隔持续调用 `IncrStep()`；服务端会为每个连接分别累加计数，并通过 `IPlayerCallback.OnNotify` 推送当前步数。
