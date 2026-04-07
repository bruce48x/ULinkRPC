# Changelog

## 0.8.1 / 0.6.3 / 0.6.1

- Fixed RPC keepalive semantics so peer liveness is proven by inbound traffic and ping/pong responses rather than local outbound activity.
- Fixed client shutdown to fail pending RPC calls deterministically instead of leaving them hanging.
- Isolated client push-handler failures so callback exceptions no longer tear down the entire connection.
- Added a decompression size limit to transport security decoding to reduce compressed-frame denial-of-service risk.
- Added regression coverage for keepalive, shutdown, push isolation, and compressed-frame limits.

## 0.13.5

- Fixed generated client overload forwarding so parameterless client calls correctly delegate to the `CancellationToken` overload instead of recursing.
- Tightened contract validation to fail fast when RPC services or callback contracts are declared without valid RPC methods.
- Added behavior-level generator tests that compile and execute generated client code, not just compile it.
