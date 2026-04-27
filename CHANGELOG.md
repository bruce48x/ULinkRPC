# Changelog

## 0.2.44

- Release packages:
	- `ULinkRPC.Starter` `0.2.44`
- Starter-generated projects now include root `codegen.ps1` and `codegen.sh` helpers so DTO or service-contract changes under `Shared/` can regenerate both server and client code in one command.
- The generated regeneration scripts choose the correct client codegen target automatically for Unity / Tuanjie versus Godot.

## 0.2.43

- Release packages:
	- `ULinkRPC.Starter` `0.2.43`
- Updated the starter's bundled `ULinkRPC.Transport.Kcp` dependency to `0.11.8`.
- Refreshed the starter's pinned Unity/NuGet dependency baseline to the latest stable package versions used by the generated `packages.config` files, including current `System.Text.Json`, `System.IO.Pipelines`, Roslyn, and BCL support packages.

## 0.11.8

- Release packages:
	- `ULinkRPC.Transport.Kcp` `0.11.8`
- Added an explicit `KcpTransport(string host, int port, uint conversationId)` overload so clients can connect with a server-assigned `conv` instead of always generating one locally.
- Added optional KCP handshake admission hooks via `KcpHandshakeAdmission` and new `KcpConnectionAcceptor` / `KcpListener` overloads so servers can validate or reserve incoming `conv` values before establishing a session.

## 0.2.41

- Release packages:
	- `ULinkRPC.Starter` `0.2.41`
- Added Tuanjie client support to `ULinkRPC.Starter`.
- `ULinkRPC.Starter` now supports `--client-engine tuanjie` plus `unity-china` / `unitycn` aliases, reusing the existing Unity-compatible client template and codegen flow.
- Updated starter help and readme text so Unity-compatible client scaffolding explicitly covers Tuanjie in addition to Unity.

- Fixed Godot starter generation so scaffolded projects now include a root `.gitattributes` file with LF normalization for source assets and LFS/binary rules for common Godot project binaries.

## 0.16.3 / 0.2.39

- Release packages:
	- `ULinkRPC.CodeGen` `0.16.3`
	- `ULinkRPC.Starter` `0.2.39`
- Added `--version` to `ulinkrpc-codegen` so the tool can print its package version and exit without requiring any other arguments.
- Added `--version` to `ulinkrpc-starter` so the tool can print its package version and exit before any interactive prompts or template generation.

## 0.2.38

- Release packages:
	- `ULinkRPC.Starter` `0.2.38`
- Updated the generated Godot client baseline from Godot `4.4` to Godot `4.6`.
- The generated `project.godot` now writes `config/features=PackedStringArray("4.6", "C#")`.
- The generated `Client.csproj` now falls back to `Godot.NET.Sdk/4.6.1` when no local SDK package source is detected.
- Updated starter help/readme text so Godot scaffolding consistently refers to Godot `4.6`.

## 0.2.37

- Reverted the unreleased source-side Unity `memorypack` formatter-registration experiment that had been added after `0.2.36`.
- The explicit `SharedMemoryPackRegistration` approach did not reliably fix fresh Unity starter projects in real user validation, so it is not being kept in source as the current direction.
- The Unity fresh-start `memorypack` issue is now documented as known-but-deferred and should be revisited later with a different approach.

## 0.2.36

- Release packages:
	- `ULinkRPC.Starter` `0.2.36`
- Fixed Unity starter `memorypack` runtime startup so newly generated clients explicitly register generated `MemoryPack` formatters before constructing the RPC client.
- The generated shared project now emits a `SharedMemoryPackRegistration` helper, and the generated Unity tester calls it before opening the default connection.
- This removes the first-run Unity failure where starter-generated `PingRequest` / `PingReply` types could still throw `MemoryPackSerializationException: ... is not registered in this provider` even though package restore and source generation had succeeded.

## 0.2.35

- Release packages:
	- `ULinkRPC.Starter` `0.2.35`
- Fixed Unity starter `memorypack` package restore again so generated `Assets/packages.config` now references the correct Roslyn package set for `MemoryPack.Generator`.
- The generated Unity package list now uses `Microsoft.CodeAnalysis.Common` instead of the wrong umbrella `Microsoft.CodeAnalysis` package, and also includes the missing `System.Reflection.Metadata` plus related runtime dependencies required for Unity's first import.
- This removes the first-import Unity errors where `MemoryPack.Generator.dll` and `Microsoft.CodeAnalysis.CSharp.dll` still failed to load even after the previous `0.2.34` fix.

## 0.2.34

- Release packages:
	- `ULinkRPC.Starter` `0.2.34`
- Fixed Unity starter `memorypack` package restore so generated `Assets/packages.config` now includes the Roslyn dependencies required by `MemoryPack.Generator`.
- This removes the first-import Unity error where `MemoryPack.Generator.dll` could not resolve `Microsoft.CodeAnalysis` / `Microsoft.CodeAnalysis.CSharp` and the project entered Safe Mode on initial open.

## 0.11.2 / 0.2.33

- Release packages:
	- `ULinkRPC.Transport.Tcp` `0.11.2`
	- `ULinkRPC.Starter` `0.2.33`
- Fixed TCP server-side accepted-connection handling so freshly accepted `TcpServerTransport` instances report a connected state before `RpcSession.StartAsync()` calls `ConnectAsync()`.
- This fixes a runtime bug where `BoundedConnectionAcceptor` could treat a newly accepted TCP connection as stale and dispose it before the server session started.
- In practice, this restores starter-generated `tcp + memorypack` and `tcp + json` flows where the client could connect successfully but then hang on the first RPC call.
- Updated `ULinkRPC.Starter`'s bundled release manifest so newly scaffolded TCP projects reference `ULinkRPC.Transport.Tcp` `0.11.2`.

## 0.2.32

- Release packages:
	- `ULinkRPC.Starter` `0.2.32`
- Fixed Godot starter `memorypack` projects so the generated `Shared.csproj` now targets `net8.0;net10.0` instead of `netstandard2.1;net10.0`.
- Fixed MemoryPack starter contracts so the generated shared project explicitly references `MemoryPack` and `MemoryPack.Generator`.
- This removes the Godot runtime failure where generated `MemoryPack` DTOs could throw `System.TypeLoadException: Virtual static method 'Serialize' is not implemented` while loading `Shared.Interfaces.PingReply`.

## 0.2.31

- Release packages:
	- `ULinkRPC.Starter` `0.2.31`
- Fixed Godot starter C# project binding so newly generated `project.godot` files now set `[dotnet] project/assembly_name="Client"` to match the generated `Client.csproj` / `Client.dll`.
- Fixed Godot starter client output so runtime package dependencies are copied into Godot's load directory and local restores are not blocked by NuGet audit checks in restricted environments.
- This removes the runtime error where Godot reported that the associated C# class could not be found for `res://Scripts/Rpc/Testing/RpcConnectionTester.cs` even though the script file and class existed.

## 0.2.30

- Release packages:
	- `ULinkRPC.Starter` `0.2.30`
- Fixed Godot starter script binding so the generated `RpcConnectionTester.cs` now declares a top-level `RpcConnectionTester` class that Godot can instantiate from `Main.tscn`.
- This removes the runtime error where Godot reported that the associated C# class could not be found for `res://Scripts/Rpc/Testing/RpcConnectionTester.cs`.

## 0.2.29

- Release packages:
	- `ULinkRPC.Starter` `0.2.29`
- Fixed Godot starter runtime behavior so the default connection example now defers auto-connect until the scene is ready, matching the expected Unity starter flow more closely.
- This restores the generated Godot project's default behavior of creating a client connection and issuing the starter `Ping` request automatically on Play.

## 0.2.28

- Release packages:
	- `ULinkRPC.Starter` `0.2.28`
- Fixed Godot starter generation so `project.godot` no longer writes the selected transport and serializer into `config/features`.
- This removes false unsupported-feature warnings when opening newly scaffolded Godot projects with combinations like `websocket + memorypack` or `kcp + json`.

## 0.16.2 / 0.2.27

- Release packages:
	- `ULinkRPC.CodeGen` `0.16.2`
	- `ULinkRPC.Starter` `0.2.27`
- Added Godot 4.x client support to `ULinkRPC.CodeGen`.
- `ULinkRPC.CodeGen` now supports `--mode godot`, detects Godot projects via `project.godot`, defaults generated output to `Scripts/Rpc/Generated`, and keeps Unity-only `.asmdef` emission scoped to Unity projects.
- Added Godot 4.x client scaffolding to `ULinkRPC.Starter`.
- `ULinkRPC.Starter` now supports `--client-engine unity|godot`, prompts for the client engine interactively when omitted, and generates either the existing Unity skeleton or a Godot 4.x C# client skeleton.
- Updated `ULinkRPC.Starter`'s bundled `ULinkRPC.CodeGen` version so new starter projects install the Godot-capable generator by default.

## 0.2.26

- Release packages:
	- `ULinkRPC.Starter` `0.2.26`
- Changed the Unity generated client namespace used by `ULinkRPC.Starter` from `Client.Generated` to `Rpc.Generated`.
- This keeps the scaffolded tester script, the explicit `--namespace` passed to `ULinkRPC.CodeGen`, and the generated output path `Assets/Scripts/Rpc/Generated` on the same naming convention.

## 0.2.25

- Release packages:
	- `ULinkRPC.Starter` `0.2.25`
- Changed the Unity client codegen output path used by `ULinkRPC.Starter` from `Assets/Scripts/Rpc/RpcGenerated` to `Assets/Scripts/Rpc/Generated`.
- This aligns the scaffolded folder layout with `ULinkRPC.CodeGen`'s own default Unity output path and avoids carrying two naming conventions for the same generated client code.

## 0.2.24

- Release packages:
	- `ULinkRPC.Starter` `0.2.24`
- Fixed Unity starter generation so it no longer writes a duplicate `ULinkRPC.Generated.asmdef` alongside the asmdef already emitted by `ULinkRPC.CodeGen`.
- Before this fix, newly scaffolded Unity projects could fail on first open with `Assembly with name 'ULinkRPC.Generated' already exists` because both `Assets/Scripts/Rpc/Generated` and `Assets/Scripts/Rpc/RpcGenerated` contained the same assembly name.

## 0.11.7 / 0.11.3 / 0.11.2 / 0.11.1 / 0.16.1 / 0.2.23

- Release packages:
	- `ULinkRPC.Server` `0.11.7`
	- `ULinkRPC.Transport.Kcp` `0.11.7`
	- `ULinkRPC.Transport.WebSocket` `0.11.3`
	- `ULinkRPC.Transport.Tcp` `0.11.1`
	- `ULinkRPC.Core` `0.11.2`
	- `ULinkRPC.CodeGen` `0.16.1`
	- `ULinkRPC.Starter` `0.2.23`
- Updated `ULinkRPC.Starter`'s bundled release manifest so newly scaffolded projects pin the current in-repo package versions instead of older package revisions.

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
