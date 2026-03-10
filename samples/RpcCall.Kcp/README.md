# RpcCall.Kcp

MemoryPack-based RPC sample over KCP.

## Structure

- `RpcCall.Kcp.Server`: .NET 10 KCP server
- `RpcCall.Kcp.Unity`: Unity 2022 LTS client

## Quick Start

Build or regenerate the sample from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Kcp
```

Run the server:

```powershell
pwsh -NoProfile -File .\scripts\sample.ps1 -Sample RpcCall.Kcp -Run
```

Open `samples/RpcCall.Kcp/RpcCall.Kcp.Unity`, load `Assets/Scenes/KcpConnectionTest.unity`, and press Play.

The Unity client opens multiple KCP sessions, logs in, and calls `IncrStep()` on each connection at a fixed interval. The server creates one `PlayerService` per `RpcSession`, so every connection maintains its own counter.
