# RpcCall.Json (Minimal Tutorial)

最简 RPC 示例，基于 TCP 传输 + JSON 序列化

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

2. 打开 Unity 项目

打开 `samples/RpcCall.Json/RpcCall.Json.Unity`，进入场景 `Assets/Scenes/TcpConnectionTest.unity`，点击 Play。

默认会自动连接 `127.0.0.1:20000` 并执行 `Login` + `Ping`，同时接收服务端通过 `IPlayerCallback.OnNotify` 推送的回调消息。
