# RPC Status Error Model Design

Date: 2026-06-04

## Context

ULinkRPC is still early enough to change protocol and API names without carrying historical compatibility. The goal is to make framework-level RPC failures clear, stable, and useful for long-lived projects.

Today `RpcStatus` has only:

- `Ok`
- `NotFound`
- `Exception`

That is too coarse. It currently groups handler failures, null handler responses, and server overload under `Exception`, which weakens client retry policy, observability, and operator diagnosis.

## Design Principle

`RpcStatus` only represents framework and infrastructure outcomes.

Business failures stay in business DTOs. For example, login failure, insufficient inventory space, room not found, cooldown not ready, or rejected matchmaking should be represented by normal response DTO fields, not by `RpcStatus`.

This keeps the framework transport/error channel separate from application semantics.

## Status Set

Define the stable baseline status set as:

```csharp
public enum RpcStatus : byte
{
    Ok = 0,
    NotFound = 1,
    HandlerError = 2,
    Overloaded = 3,
    BadRequest = 4,
    ProtocolError = 5
}
```

Remove the public `Exception` status name. The name is ambiguous because it sounds like .NET exception transport rather than RPC framework status. `HandlerError` is narrower and communicates the intended meaning.

## Status Semantics

`Ok`
: The service method completed successfully. The payload contains the serialized return DTO. `void` returns use an empty payload.

`NotFound`
: No handler was found for the requested `serviceId:methodId`. Clients should usually not retry. This usually indicates client/server version mismatch, deployment drift, or missing service registration.

`HandlerError`
: The server-side handler failed while executing, or returned an invalid framework response such as null. The server logs the full exception. The client receives a stable sanitized message through `RpcException`.

`Overloaded`
: The server cannot currently accept the request, such as when a session request queue is full. Clients may apply application-owned backoff or retry only when the operation is safe to retry. The framework should not automatically retry RPC calls because it cannot know method idempotency.

`BadRequest`
: The frame reached the RPC request layer, but the request content is invalid for the RPC contract. Examples include malformed request payload, deserialization failure, or request data that cannot be interpreted as the generated DTO shape. Clients should usually not retry unchanged data.

`ProtocolError`
: The peer violated the wire protocol or connection state machine. Examples include unknown frame type, invalid envelope shape, or a frame that is illegal in the current state. This usually should close the connection rather than return a normal response. Use this response status only when there is a clear request id to answer.

## Current Implementation Mapping

Implement these mappings immediately:

- Missing handler returns `NotFound`.
- Handler exception returns `HandlerError`.
- Handler returns null response returns `HandlerError`.
- Session request queue full returns `Overloaded`.
- Non-`Ok` responses cause the client runtime to throw `RpcException`.

Add `BadRequest` and `ProtocolError` to the enum and documentation now, but do not force broad parser or deserializer rewrites in the first implementation unless an existing call site already has a clear mapping. These names reserve the stable protocol surface for later validation improvements.

## Client Failure Taxonomy

Client code should treat these channels separately:

- `RpcException`: remote RPC framework failure, classified by `RpcException.Status`.
- `OperationCanceledException`: local caller cancellation or timeout policy.
- `ObjectDisposedException`, transport exceptions, and `Disconnected`: connection lifecycle failure.
- Business response DTOs: normal expected business failures.

Generated clients should continue returning successful business DTOs and throwing `RpcException` for non-`Ok` framework responses. They should not introduce a framework result union for every RPC call.

## Observability

The server should continue logging handler exceptions with request id, service id, method id, and session context id.

For `Overloaded`, the response status should be machine-readable. The error message may stay short and safe, for example:

```text
RPC server is overloaded; request queue is full.
```

Client observability should rely on `RpcException.Status`, `RequestId`, `ServiceId`, and `MethodId`, not string matching against error messages.

## Non-Goals

This design does not add:

- A shared business error-code protocol.
- Automatic retry policy.
- Server exception type serialization.
- Stack trace transport to clients.
- Per-method timeout configuration.
- Authentication or authorization status codes.

Those can be handled by application DTOs or later framework features if they become unavoidable. They should not be added speculatively to `RpcStatus`.

## Compatibility Position

Because the project is still early, do not keep an `Exception` alias for compatibility. Removing it now avoids long-term ambiguity.

Existing docs, tests, and generated references should be updated to use `HandlerError` and `Overloaded`.

## Acceptance Criteria

- `RpcStatus.Exception` no longer exists.
- Server handler failures use `RpcStatus.HandlerError`.
- Null handler responses use `RpcStatus.HandlerError`.
- Request queue full uses `RpcStatus.Overloaded`.
- Missing handler remains `RpcStatus.NotFound`.
- Generated/client runtime non-`Ok` failures still throw `RpcException`.
- Tests cover the concrete status mappings above.
- Error handling documentation describes `RpcException` rather than `InvalidOperationException` for non-`Ok` responses.
- Changelog records the protocol/API error status change.
