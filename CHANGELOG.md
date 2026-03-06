# Changelog

## ULinkRPC.CodeGen 0.2.2 - 2026-02-26
- Add callback-aware server binder overload generation for `IRpcService<TSelf, TCallback>`: `Bind(RpcServer, Func<TCallback, TSelf>)`.
- Update samples and docs to demonstrate end-to-end `TCallback` push wiring.

## ULinkRPC.CodeGen 0.1.7 - 2026-02-20
- Update `ULinkRPC.CodeGen` to generate client overloads that accept `CancellationToken` and forward it to `IRpcClient`.

## Runtime Packages 0.1.1 - 2026-02-20
- Harden runtime transport/client/server behavior:
  - thread-safe client request id generation
  - start lifecycle guard for client/server
  - KCP socket ownership control
  - WebSocket receive buffer cap
  - stricter request envelope and frame length validation

## ULinkRPC.CodeGen 0.1.1 - 2026-02-18
- Update `ULinkRPC.CodeGen` generated no-argument call payload from `RpcVoid.Instance` to `default` to keep stubs serializer-agnostic.

## 0.2.0 - 2026-02-18
- Split `ULinkRPC.Runtime` into `ULinkRPC.Core`, `ULinkRPC.Client`, and `ULinkRPC.Server`.
- Move client runtime implementation into `src/ULinkRPC.Client` and remove duplicated sample-side runtime client sources.
- Update code generation defaults and generated code usings to `ULinkRPC.Core` / `ULinkRPC.Server`.
- Update samples, contracts, and server references to the new package and namespace layout.
- Split serializers into dedicated packages: `ULinkRPC.Serializer.MemoryPack` and `ULinkRPC.Serializer.Json`.
- Split transport implementations into dedicated packages: `ULinkRPC.Transport.Tcp`, `ULinkRPC.Transport.Kcp`, `ULinkRPC.Transport.WebSocket`, and `ULinkRPC.Transport.Loopback`.
- Make `ULinkRPC.Client` and `ULinkRPC.Server` depend only on abstractions (`ITransport` + `IRpcSerializer`) without concrete transport/serializer package dependencies.

## 0.1.5 - 2026-02-18
- Publish `ULinkRPC.Runtime` and `ULinkRPC.CodeGen` version `0.1.5`.
- Add `IRpcClient` abstraction in runtime so generated clients depend on shared runtime contracts instead of sample-specific client namespaces.
- Improve `ULinkRPC.CodeGen` defaults and auto-detection for `RpcCall.Json` / `RpcCall.MemoryPack` layouts.
- Enhance binder generation with delegate-based `Bind(...)` overloads to reduce handwritten test/service adapter code.

## 0.1.4 - 2026-02-14
- Internal release preparation and packaging updates.

## 0.1.2 - 2026-02-02
- Rename namespaces under `src/ULinkRPC.Runtime` to use the `ULinkRPC` prefix.
- Convert file-scoped namespaces to block namespaces for C# 9.0 compatibility.

## 0.1.3 - 2026-02-02
- Add MIT license file to the NuGet package.
