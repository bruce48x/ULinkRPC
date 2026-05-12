+++
title = "安全模型"
+++

ULinkRPC 的安全配置位于 frame 层，主要入口是 `TransportSecurityConfig`。它可以启用压缩和对称加密，但它不是完整的身份认证、授权或 TLS 替代方案。

## `TransportSecurityConfig` 做什么

当客户端 `RpcClientOptions.UseSecurity(...)` 或服务端 `RpcServerHostBuilder.UseSecurity(...)` 启用安全配置后，运行时会用 `TransformingTransport` 包住底层 transport。

当前可配置项包括：

- `EnableCompression`：发送前压缩 frame。
- `CompressionThresholdBytes`：达到该尺寸才尝试压缩，默认 `1024`。
- `MaxDecompressedFrameBytes`：解压后的最大 frame 尺寸，默认来自 `RpcProtocolLimits.DefaultMaxDecompressedFrameBytes`。
- `EnableEncryption`：启用对称加密。
- `EncryptionKey` / `EncryptionKeyBase64`：对称密钥来源。

加密实现当前使用 AES-CBC 加 HMAC-SHA256。密钥通过 HKDF 派生为加密 key 和 MAC key。接收端会校验 HMAC，校验失败会抛出异常并导致连接失败。

## 它不做什么

`TransportSecurityConfig` 不验证远端身份。只要攻击者拿到同一份对称密钥，就可以构造可通过校验的 frame。

它不提供用户登录、权限校验、会话票据、token 续期或防重放协议。业务身份和授权仍然需要你在服务方法里实现。

它不替代 TLS / WSS。TLS / WSS 提供传输层证书校验、链路加密和成熟的部署生态；ULinkRPC frame 加密只发生在应用 frame 上。

它不隐藏连接元数据，例如 IP、端口、连接时间、frame 大小级别的流量特征。

## TLS / WSS 的关系

如果你使用 WebSocket 并需要公网传输，优先用 `wss://` 和标准 TLS 终止。ULinkRPC 的 `WsTransport` 当前接收 URI；是否使用 `ws://` 或 `wss://` 取决于你的 endpoint 和宿主配置。

`TransportSecurityConfig` 可以作为额外的 frame 级保护层，但客户端和服务端配置必须完全对称。压缩、加密和密钥不一致会导致解码失败。

## 密钥和配置建议

不要把生产密钥写死在 Unity、Godot 或团结客户端里。客户端包内的静态密钥可以被提取；如果必须启用 frame 加密，应结合登录、版本、环境和密钥轮换策略评估风险。

不要在加密前压缩包含攻击者可控内容和秘密内容的 payload，除非你明确接受压缩侧信道风险。当前配置支持压缩后加密；这对带宽有帮助，但不是所有威胁模型下都适合。

服务端和客户端同时启用 `MaxDecompressedFrameBytes` 相关限制，防止异常压缩 payload 造成过大内存消耗。

## 当前没有实现的能力

当前仓库没有证书管理、mTLS、JWT 验证、服务端鉴权 middleware、自动密钥交换、密钥轮换协议、nonce 持久化防重放，或内置账号系统。
