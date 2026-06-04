+++
title = "API Stability Roadmap"
date = 2026-05-15T00:00:00+08:00
+++

ULinkRPC is currently ready for a **soft freeze**: the main integration path, wire protocol direction, and package boundaries are mostly stable, but it is not yet ready to declare a full hard freeze or 1.0 API freeze.

This document records the current judgment and future optimization direction. It is not a one-time checklist; it is the basis for evaluating breaking changes before future releases.

## Current Judgment

Areas that can be stabilized first:

- C# contract-first workflow: `[RpcService]`, `[RpcMethod]`, `[RpcCallback]`, `[RpcPush]`
- Basic usage of the generated client facade, server binder, and callback binder
- Main entry points such as `ITransport`, `IRpcSerializer`, `RpcClientOptions`, and `RpcServerHostBuilder`
- Package boundaries for TCP, WebSocket, KCP, and Loopback transports
- Replacement boundary between JSON and MemoryPack serializers
- Basic wire semantics for request / response / push / keepalive

Areas that should not be hard-frozen yet:

- Public commitment boundary for low-level frame/envelope/session types
- RPC status taxonomy and remaining error model details
- Push callback registration and unregistration model
- Restart and reuse semantics for runtime, transport, and session objects
- Naming rules and conflict handling for generated facades
- Server-side advanced APIs such as `RpcSession` constructor overloads, `RpcServiceRegistry`, and low-level handler delegates

## API Layers

Future documentation and release notes should divide public APIs into three layers.

### Stable API

This layer targets regular users. After a hard freeze, it should mostly receive only compatible additions.

- contract attributes
- starter-recommended client/server initialization
- generated `RpcClient` facade lifetime
- `RpcClientOptions`
- `RpcServerHostBuilder`
- transport construction entry points
- serializer construction entry points

### Generated-Support API

This layer primarily supports source generator output. Users can see it and may call it in advanced scenarios, but its compatibility should be tied to matching versions of `ULinkRPC.Analyzers` and runtime packages.

- `IRpcClient`
- `RpcMethod<TArg, TResult>`
- `RpcPushMethod<TArg>`
- `RpcGeneratedServicesBinderAttribute`
- registry and handler entry points used by generated server binders

When this layer has a breaking change, releases must explicitly require users to rebuild so the source generator regenerates glue code, and they should avoid silent failures from combining a new runtime with old generated code.

### Advanced API

This layer is for transports, serializers, test utilities, and custom host integration. It can remain public, but before a hard freeze the project needs to clarify which parts are long-term commitments and which may still change.

- `TransportFrame`
- `RpcEnvelopeCodec`
- envelope/frame DTOs
- `RpcSession`
- `RpcServiceRegistry`
- `IRpcConnectionAcceptor`
- `TransformingTransport`
- `TransportSecurityConfig`

If a type is public only for package-internal cooperation or tests, prefer narrowing its visibility. If it must remain public, add documentation and contract tests.

## Pre-Freeze Improvements

### 1. Clarify Public Commitment Boundaries

The current API Reference lists many low-level types as public APIs. Before freezing, decide:

- which types are stable user entry points
- which types only support generated code
- which types belong to advanced integration
- which types can become internal or hide behind narrower facades

The goal is not to reduce every public type, but to prevent temporary public surface area from being mistaken for a long-term stability promise.

### 2. Strengthen the Error Model

Today `RpcStatus` only has `Ok`, `NotFound`, and `Exception`.

Clients now throw `RpcException` for non-OK remote responses. `RpcException` is the dedicated framework exception for remote RPC failures and exposes:

- `RpcStatus Status`
- `string? ErrorMessage`
- request id
- service id
- method id

Before freezing, evaluate whether framework-level statuses such as overloaded, decode failure, and bad request should be separated. Business errors should still stay in application DTOs or business return models, not be forced into the low-level runtime.

### 3. Revisit the Push Callback API

Callback registration is currently one-shot: `RegisterPushHandler(..., Action<T>)`. Before freezing, decide whether to support:

- handler unregistration
- async handlers
- duplicate registration policy
- handler exception observability
- keeping Unity, Tuanjie, and Godot main-thread dispatch solely as application-layer responsibility

If registration should return `IDisposable`, or if a `Func<T, ValueTask>` shape should be added, do it before the hard freeze.

### 4. Lock Down Lifetime Semantics

The current docs already recommend rebuilding the generated client, runtime, transport, and options after disconnects. Before freezing, clarify further:

- whether `RpcClientRuntime` is single-use
- whether `RpcSession` may restart after stop
- whether `ConnectAsync` on accepted server transports is initialization or an actual connection action
- whether `ITransport.IsConnected` is a diagnostic signal or strongly consistent state
- event and pending-request behavior for dispose, remote close, and keepalive timeout

Once users depend on these semantics, changing them becomes expensive.

### 5. Stabilize Generated Facade Naming Rules

Generated `RpcApi` currently derives group/property names from the contract namespace and service interface, and resolves conflicts with numeric suffixes.

Before freezing, decide whether explicit naming support is needed, such as service/group alias attributes. Otherwise, the generated API naming rules themselves become a long-term compatibility promise.

### 6. Keep Unity Dependency Constraints Accurate

`System.Threading.Channels` is an explicit dependency already used by the current runtime and Unity samples, and should remain on the allowed list.

`System.IO.Pipelines` may enter Unity-side package sets through transport or serializer dependency chains; its presence alone is no longer treated as a violation. Before adding or expanding related usage, validate it against Unity 2022 LTS, iOS, IL2CPP, and HybridCLR.

Unity client code should still avoid:

- `System.Reflection.Emit`
- runtime code generation
- JIT-only APIs

Before adding dependencies, confirm real compatibility with Unity 2022 LTS, iOS, IL2CPP, and HybridCLR.

## Phased Plan

### Soft Freeze Phase

The current phase should prioritize:

- no breaking wire protocol changes without a migration path
- stable starter generation paths
- generated code matching runtime versions
- compatible additions only for main entry APIs
- explicit upgrade instructions for breaking changes in the changelog

### Before Hard Freeze

Before 1.0 or a similar stability commitment, complete:

- API layer documentation
- finalized error model
- finalized callback registration model
- lifetime semantics documentation and contract tests
- finalized generated facade naming rules
- retention, narrowing, or annotation of low-level public types

### After Hard Freeze

After freezing, the project can still improve:

- performance and allocation behavior
- transport robustness
- starter template experience
- documentation and examples
- compatible new APIs

But it should avoid changing existing main entry signatures, generated API shape, and wire protocol. If a breaking change is truly required, use a major version, migration guide, and compatibility window.

## Release Checklist Questions

Before every release that touches runtime, source generator, or transport packages, check at least:

- Did the runtime API called by generated code change?
- Does the change require users to rebuild and refresh source-generated code?
- Did wire frame, service id, method id, request id, or payload semantics change?
- Did disconnect, dispose, pending request, or keepalive timeout behavior change?
- Did generated facade type names, namespaces, groups, or property names change?
- Was a Unity-side dependency added? Was IL2CPP compatibility verified?
- Does the changelog need to document a breaking change or migration step?

This roadmap should evolve with implementation progress. After completing a pre-freeze improvement, move it from a risk item to a stable convention and add the matching reference docs or tests.
