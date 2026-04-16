# Changelog

## 0.11.4 / 0.11.2

- Release packages:
	- `ULinkRPC.Server` `0.11.4`
	- `ULinkRPC.Transport.Kcp` `0.11.2`
- This release focuses on removing small but persistent allocations from the real runtime hot paths instead of papering over them with broader caches or compatibility shims.
- Reworked KCP server session lookup so inbound datagrams are keyed by `(IPAddress, Port)` value semantics instead of `IPEndPoint.ToString()`, eliminating per-packet string allocation in the listener loop.
- Reworked KCP server receive waiting so `ReceiveFrameAsync` no longer allocates a linked `CancellationTokenSource` on every empty-queue wait cycle; shutdown still wakes waiters via the existing frame signal.
- Reworked server inflight-request draining so `TrackedTaskCollection.WaitAsync` now waits on a drained signal driven by the final completing task, instead of repeatedly snapshotting the tracked task set with `ToArray()`.
- Reworked the Unity-compatible KCP server send path so the `netstandard2.1` build no longer materializes outbound buffers with `mem.ToArray()`; it now prefers direct array segments and falls back to pooled copies only when required by the underlying memory owner.
- Design note:
	- The guiding rule for these fixes was to remove allocation sources at the point they occur in the transport/session hot path, while keeping the public runtime contract unchanged for Unity 2022 (`netstandard2.1`) and the server runtime (`net10.0`).
	- The KCP fixes deliberately mirror the more efficient client-side patterns that already existed in the repository, so the server and client transports now follow the same zero-extra-allocation strategy where the underlying APIs allow it.
	- The server shutdown fix avoids introducing another concurrent collection or background sweeper; instead it models the actual lifecycle directly: first tracked task creates a pending drain signal, last completing task resolves it.

## 0.11.3 / 0.2.21

- Release packages:
	- `ULinkRPC.Server` `0.11.3`
	- `ULinkRPC.Starter` `0.2.21`
- Fixed `RpcSession.RunAsync` so that cancelling the external `CancellationToken` now terminates active sessions promptly. Previously, `StartAsync` created an internal `CancellationTokenSource` unlinked from the caller's token, causing `RunAsync` to hang indefinitely when the transport had no EOF signal (e.g. KCP/UDP with connected clients).
- Fixed `RpcServerHost.RunAsync` to transfer ownership of the inner acceptor to `BoundedConnectionAcceptor` instead of holding a separate `await using` reference, which caused a double-`Dispose` and `ObjectDisposedException` on shutdown when no clients were connected.

## 0.2.20

- Release packages:
	- `ULinkRPC.Starter` `0.2.20`
- Fixed Unity starter `packages.config` generation to always include `System.Threading.Channels`, matching the direct runtime dependency now required by `ULinkRPC.Client` and WebSocket/KCP transports.
- Fixed the checked-in Unity samples that were still missing `System.Threading.Channels`, so restored sample clients load `ULinkRPC.Client.dll` cleanly under Unity again.

## 0.16.1 / 0.2.19

- Release packages:
	- `ULinkRPC.CodeGen` `0.16.1`
	- `ULinkRPC.Starter` `0.2.19`
- Fixed Unity client generation so `ULinkRPC.CodeGen` now emits a default `ULinkRPC.Generated.asmdef` when the Unity output folder does not already define its own assembly.
- The generated Unity asmdef now infers the nearest contracts assembly reference from `--contracts`, reducing manual Unity assembly wiring for generated client code.
- Fixed `ULinkRPC.Starter` so newly scaffolded Unity clients always include the generated runtime asmdef expected by the sample testing assembly layout.
- Refreshed the checked-in Unity samples so existing sample projects compile again after the recent generated-client API changes.

## 0.15.0 / 0.11.0

- Release packages:
	- `ULinkRPC.CodeGen` `0.15.0`
	- Runtime packages (`ULinkRPC.Core`, `ULinkRPC.Client`, `ULinkRPC.Server`, transports, serializers) `0.11.0`
- This release fixes a published package mismatch where `ULinkRPC.CodeGen 0.14.0` could still emit server binders that referenced the removed `IRpcSerializer.Serialize(...)` API.
- Fixed the published code generator so server binders now emit `SerializeFrame(...)` and `RpcEnvelopeCodec.EncodeResponse(...)` instead of referencing the removed `IRpcSerializer.Serialize(...)` API.
- Removed `IRpcSerializer.Serialize(...)` from the runtime surface and standardized serializers on pooled `TransportFrame` output to eliminate extra response allocations and copies.
- Changed generated server registry handlers to operate on `RpcRequestFrame` and return encoded `TransportFrame` responses directly, matching the new runtime contract.
- Removed the redundant inner send semaphore from TCP framing so each TCP send is serialized exactly once.
- Refreshed the checked-in sample generated code so repository samples compile against the new runtime and code generator.
- Upgrade guidance:
	- Regenerate all generated RPC code after upgrading to these packages.
	- Do not mix `ULinkRPC.CodeGen 0.14.0` with runtime `0.11.0`; use `ULinkRPC.CodeGen 0.15.0` together with runtime `0.11.0`.
	- If you previously consumed `IRpcSerializer.Serialize(...)` directly, migrate to `SerializeFrame(...)`.

## 0.2.14 / 0.13.6 / 0.8.2 / 0.6.4 / 0.6.2

- Refactored runtime internals by extracting shared keepalive state and request/task tracking helpers, reducing complexity in `RpcClientRuntime`, `RpcSession`, and `RpcServerHost`.
- Refactored `ContractParser` so source-loading/callback binding orchestration is separated from contract validation rules.
- Refactored code generation emitters so all-services binder generation, callback proxy generation, and facade callback generation are isolated into focused files.
- Refactored starter generation internals by centralizing Unity client template values and separating server command-line option parsing from the builder itself.
- Kept generated output and runtime behavior stable while improving local reasoning, reuse, and testability.

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
