# RpcCall.MemoryPack (Full Tutorial)

完整 RPC 示例，包含压缩与加密。

## 结构

- `RpcCall.MemoryPack.Server`：.NET 10.0 TCP 服务端
- `RpcCall.MemoryPack.Unity`：Unity 2022 LTS 客户端

## 快速开始

1. 运行服务端

```bash
cd samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Server
dotnet build
dotnet run --project RpcCall.MemoryPack.Server
```

2. 打开 Unity 项目

打开 `samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity`，进入场景 `Assets/Scenes/TcpConnectionTest.unity`，点击 Play。

默认会自动连接 `127.0.0.1:20000` 并执行 `Login` + `Ping`，同时接收服务端通过 `IPlayerCallback.OnNotify` 推送的回调消息。
