# Changelog

## 0.11.3

- Release packages:
	- `ULinkRPC.Transport.WebSocket` `0.11.3`
- Fixed WebSocket accept queue hygiene so `AcceptAsync()` no longer returns queued transports that already disconnected before the server drained the queue.
- Hardened WebSocket transport disposal so server-side teardown does not hang forever when the remote peer disappears without completing the close handshake.
- Before this fix, a client could complete the WebSocket upgrade, get queued for acceptance, disconnect, and still be handed to the runtime on the next `AcceptAsync()` call. That surfaced as immediate receive failures against what should have been a fresh accepted connection.
- Before this fix, `WsTransportFraming.DisposeAsync(...)` also used an unbounded `CloseAsync(...)` wait, so disposing a half-dead WebSocket could stall shutdown and prevent the acceptor from draining stale queued connections.
- `WsConnectionAcceptor.AcceptAsync()` now skips stale queued WebSocket transports, disposes them, and keeps reading until it finds a live connection or the caller cancels.
- `WsTransportFraming.DisposeAsync(...)` now bounds the close-handshake wait and aborts the socket if the peer is no longer cooperating.
- Tests:
	- Added a regression test proving a disconnected queued WebSocket transport is skipped instead of being returned from `AcceptAsync()`.
	- Added a regression test proving disposing a server-side WebSocket transport still completes promptly after the remote peer aborts.
- Compatibility:
	- The public API and wire protocol are unchanged.
	- The change only tightens server-side WebSocket acceptance semantics.

## 0.11.7

- Release packages:
	- `ULinkRPC.Server` `0.11.7`
- Fixed bounded accept queue hygiene so the server no longer hands disconnected queued connections to the runtime.
- Before this fix, `BoundedConnectionAcceptor` could return a connection that had already died while waiting in the server's pending-accept queue. That let the host spin up session state for a transport that was already disconnected.
- `BoundedConnectionAcceptor.AcceptAsync()` now skips stale queued connections and disposes them before continuing to the next live connection.
- Tests:
	- Added a regression test proving a disconnected queued connection is skipped instead of being returned from `AcceptAsync()`.
- Compatibility:
	- The public API and wire protocol are unchanged.
	- The change only tightens server-side acceptance semantics.

## 0.11.7

- Release packages:
	- `ULinkRPC.Transport.Kcp` `0.11.7`
- Fixed KCP accept queue hygiene so `AcceptAsync()` no longer returns connections that already died while they were still waiting in the pending-accept queue.
- Before this fix, a connection could finish the KCP handshake, get queued for acceptance, then fail before the server drained the queue. The next `AcceptAsync()` call could receive that already-disposed transport and hand a dead connection to the server runtime.
- `KcpListener.AcceptAsync()` now skips stale queued transports and keeps reading until it finds a live connection or the caller cancels.
- Tests:
	- Added a regression test proving that disposed queued KCP connections are not returned from `AcceptAsync()`.
- Compatibility:
	- The wire protocol and public API are unchanged.
	- The change only tightens runtime correctness for queued KCP accepts.

## 0.11.6

- Release packages:
	- `ULinkRPC.Transport.Kcp` `0.11.6`
- Fixed KCP listener fault isolation so a single broken session no longer takes down the entire listener loop.
- Before this fix, if `KcpServerTransport.ProcessDatagram(...)` failed for one accepted connection, the exception escaped from `KcpListener.ReceiveLoopAsync()`. That stopped all future accepts and even caused `KcpListener.DisposeAsync()` to rethrow the background failure during shutdown.
- `KcpListener` now isolates per-session datagram processing failures, disposes only the offending session, and keeps listening for new connections.
- Hardened `KcpServerTransport.DisposeAsync()` to be idempotent so listener-driven session disposal and upper-layer connection teardown can safely converge on the same transport instance.
- Tests:
	- Added a regression test proving one session-processing failure does not prevent later KCP connections from being accepted.
	- Added regression coverage that `KcpServerTransport.DisposeAsync()` is safe to call multiple times.
- Compatibility:
	- The wire protocol is unchanged.
	- The fix applies to the KCP package runtime without altering the public API surface.

## 0.11.5

- Release packages:
	- `ULinkRPC.Transport.Kcp` `0.11.5`
- Fixed the Unity-compatible `netstandard2.1` KCP client receive path so cancellation now stops blocked reads promptly instead of waiting for another UDP packet to arrive.
- Before this fix, `KcpTransport.ReceiveFrameAsync(...)` used uncancellable `Socket.ReceiveFromAsync(...)` calls outside the `NET8+` branch. In practice that meant Unity 2022 clients could stay hung in KCP receive during shutdown, disconnect, or timeout handling.
- Reworked the non-`NET8+` KCP receive paths to use a cancellation-aware polling receive loop for both the main data path and the handshake acknowledgement wait, so Unity-side teardown no longer depends on disposing the socket from another path just to break a blocked receive.
- Tests:
	- Added a regression test that guards the `netstandard2.1` source path against reintroducing uncancellable `ReceiveFromAsync(...)` calls.
- Compatibility:
	- The wire protocol is unchanged.
	- Server-side `net10.0` behavior is unchanged; the fix is specifically for Unity-compatible `netstandard2.1` KCP runtime behavior.

## 0.11.4 / 0.11.2 / 0.11.1

- Release packages:
	- `ULinkRPC.Transport.Kcp` `0.11.4`
	- `ULinkRPC.Transport.WebSocket` `0.11.2`
	- `ULinkRPC.Core` `0.11.2`
	- `ULinkRPC.Transport.Tcp` `0.11.1`
- Fixed outbound frame-size validation so transports now reject oversized frames before putting them on the wire.
- Before this fix, `LengthPrefix.Pack(...)` and the TCP framing sender accepted payloads larger than the runtime's 64 MB frame limit, which meant the local sender appeared to succeed and the failure only surfaced later on the receiving side.
- That delayed failure path could turn a local API misuse into cross-peer runtime errors, including remote disconnects and avoidable transport churn under KCP, WebSocket, and TCP.
- Added shared frame-length validation in `ULinkRPC.Core.LengthPrefix` and enforced it in `TcpPipeFraming.SendFrameAsync(...)`, so Unity 2022 `netstandard2.1` and server-side `net10.0` builds now fail fast on the sending side with the same limit.
- Tests:
	- Added regression coverage proving `LengthPrefix.Pack(...)` now rejects oversized payloads locally.
	- Added regression coverage proving the TCP sender rejects oversized frames before writing them to the stream.
- Compatibility:
	- The wire format is unchanged.
	- The behavioral change is intentional: oversized frames are now rejected locally instead of being allowed onto the network and failing remotely.

## 0.11.6

- Release packages:
	- `ULinkRPC.Server` `0.11.6`
- Fixed a session-lifecycle bug where `RpcSession` kept waiting for in-flight handlers after the client connection had already closed.
- Before this fix, a client could disconnect in the middle of a slow or blocking RPC and leave that session stuck in shutdown, keeping request-budget slots occupied until the handler completed on its own.
- `RpcSession` now cancels its internal session token as soon as the transport closes or the receive loop faults, so in-flight handlers and keepalive work are asked to stop immediately before session teardown waits for them.
- Tests:
	- Added a regression test proving that a remote disconnect cancels an in-flight request and allows the server session to complete promptly.
- Compatibility:
	- The change is server-only and keeps the Unity 2022 `netstandard2.1` client/runtime surface unchanged while remaining compatible with the server-side `net10.0` target.

## 0.11.5 / 0.11.3 / 0.11.1 / 0.2.22

- Release packages:
	- `ULinkRPC.Server` `0.11.5`
	- `ULinkRPC.Transport.Kcp` `0.11.3`
	- `ULinkRPC.Transport.WebSocket` `0.11.1`
	- `ULinkRPC.Core` `0.11.1`
	- `ULinkRPC.Starter` `0.2.22`
- Security:
	- Fixed a pre-session connection admission bug that let `WebSocket` and `KCP` acceptors buffer unbounded pending connections before `RpcServerHost` could apply `MaxPendingAcceptedConnections`.
	- An attacker could exploit this to exhaust server memory, sockets, and per-connection runtime state with unauthenticated connection floods, especially against `KCP`, where each spoofable handshake could materialize a server transport immediately.
- Runtime changes:
	- Added `RpcConnectionAdmissionDefaults.MaxPendingAcceptedConnections` and moved the default pending-connection budget to a shared runtime constant used by the server and transport acceptors.
	- Hardened `WsConnectionAcceptor` so it rejects overflow before queuing another accepted connection and cleans up queued transports during shutdown.
	- Hardened `KcpListener` / `KcpConnectionAcceptor` so new sessions are only created when a pending-admission slot is available, and all failure paths release that slot correctly.
	- Added explicit `maxPendingAcceptedConnections` overloads to `WsConnectionAcceptor.CreateAsync(...)`, `KcpConnectionAcceptor`, and `KcpListener`, then updated the starter templates and checked-in server samples to wire them to `builder.Limits.MaxPendingAcceptedConnections`.
- Tests:
	- Added regression coverage proving `KcpListener` can no longer exceed the default pending-connection limit under a burst of handshake requests.
	- Added a regression guard preventing `WsConnectionAcceptor` from regressing back to an unbounded pending connection queue implementation.
- Compatibility:
	- The fix is validated against Unity 2022 compatible `netstandard2.1` builds and the server-side `net10.0` test solution.

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
