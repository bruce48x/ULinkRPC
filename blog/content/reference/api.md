---
title: API Reference
date: 2026-05-11T00:00:00+08:00
---

This page is generated from C# XML documentation comments. Update the source comments first, then rerun `scripts/generate-api-reference.ps1`.

## ULinkRPC.Core

### Field `ULinkRPC.Core.RpcEnvelopeCodec.MaxPayloadSize`

Maximum payload length accepted by envelope decoders.

### Field `ULinkRPC.Core.RpcFrameType.KeepAlivePing`

A keepalive ping frame.

### Field `ULinkRPC.Core.RpcFrameType.KeepAlivePong`

A keepalive pong frame.

### Field `ULinkRPC.Core.RpcFrameType.Push`

A server-to-client push notification.

### Field `ULinkRPC.Core.RpcFrameType.Request`

A client-to-server RPC request.

### Field `ULinkRPC.Core.RpcFrameType.Response`

A server-to-client RPC response.

### Field `ULinkRPC.Core.RpcStatus.BadRequest`

The request reached the RPC layer but was invalid for the target RPC contract.

### Field `ULinkRPC.Core.RpcStatus.HandlerError`

The server handler failed or returned an invalid framework response.

### Field `ULinkRPC.Core.RpcStatus.NotFound`

The target service or method was not found.

### Field `ULinkRPC.Core.RpcStatus.Ok`

The request completed successfully and the payload contains the serialized return value.

### Field `ULinkRPC.Core.RpcStatus.Overloaded`

The server could not accept the request because it is overloaded.

### Field `ULinkRPC.Core.RpcStatus.ProtocolError`

The peer violated the RPC wire protocol or connection state machine.

### Field `ULinkRPC.Core.RpcVoid.Instance`

Shared void marker instance.

### Method `ULinkRPC.Core.IRpcClient.CallAsync(ULinkRPC.Core.RpcMethod<T0,T1>,T0,System.Threading.CancellationToken)`

Sends one RPC request and waits for the matching response.

Parameters:
- `method`: Generated method descriptor containing the service id and method id.
- `arg`: Request DTO instance, or `null` for void-style requests.
- `ct`: Cancellation token for the outbound send and response wait.

Type parameters:
- `TArg`: Request DTO type.
- `TResult`: Response DTO type.

Returns: The deserialized response DTO.

Exceptions:
- `ULinkRPC.Core.RpcException`: Thrown by the default runtime when the remote response status is not `ULinkRPC.Core.RpcStatus.Ok`.

### Method `ULinkRPC.Core.IRpcClient.RegisterNotificationHandler(ULinkRPC.Core.RpcNotificationMethod<T0>,System.Func<T0,System.Threading.Tasks.ValueTask>)`

Registers the handler for a server-to-client notification method.

Remarks: The default runtime invokes handlers from its internal notification-processing loop. It does not marshal notifications to the Unity main thread.

Parameters:
- `method`: Generated notification descriptor containing the service id and notification method id.
- `handler`: Handler invoked with the deserialized notification DTO.

Type parameters:
- `TArg`: Notification DTO type.

### Method `ULinkRPC.Core.IRpcConnectionAcceptor.AcceptAsync(System.Threading.CancellationToken)`

Waits for the next accepted connection.

Parameters:
- `ct`: Cancellation token for the accept operation.

Returns: The accepted transport plus display and remote endpoint metadata.

### Method `ULinkRPC.Core.IRpcSerializer.Deserialize(System.ReadOnlyMemory<System.Byte>)`

Deserializes a DTO value from payload bytes.

Parameters:
- `data`: Payload bytes.

Type parameters:
- `T`: DTO type.

Returns: The deserialized DTO value.

### Method `ULinkRPC.Core.IRpcSerializer.Deserialize(System.ReadOnlySpan<System.Byte>)`

Deserializes a DTO value from payload bytes.

Parameters:
- `data`: Payload bytes.

Type parameters:
- `T`: DTO type.

Returns: The deserialized DTO value.

### Method `ULinkRPC.Core.IRpcSerializer.SerializeFrame(T0)`

Serializes a DTO value into an owned transport frame.

Parameters:
- `value`: DTO instance to serialize.

Type parameters:
- `T`: DTO type.

Returns: An owned frame containing the serialized payload. The caller disposes it.

### Method `ULinkRPC.Core.ITransport.ConnectAsync(System.Threading.CancellationToken)`

Prepares this transport for frame I/O.

Remarks: Client transports use this call to actively connect to their remote endpoint. Accepted server transports use it to initialize per-connection state such as streams, schedulers, or framing over an already accepted connection. In-memory or already-open transports may implement it as an idempotent no-op.

### Method `ULinkRPC.Core.ITransport.ReceiveFrameAsync(System.Threading.CancellationToken)`

Receives one complete frame.

Parameters:
- `ct`: Cancellation token for the receive operation.

Returns: An owned frame. The caller disposes it after processing. An empty frame means the remote side closed the connection.

### Method `ULinkRPC.Core.ITransport.SendFrameAsync(System.ReadOnlyMemory<System.Byte>,System.Threading.CancellationToken)`

Sends one complete frame.

Parameters:
- `frame`: Frame bytes to send. The transport must not retain this memory after the call completes.
- `ct`: Cancellation token for the send operation.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.DecodeKeepAlivePing(System.ReadOnlySpan<System.Byte>)`

Decodes a keepalive ping envelope.

Parameters:
- `data`: Encoded keepalive ping bytes.

Returns: The decoded keepalive ping envelope.

Exceptions:
- `System.InvalidOperationException`: Thrown when the frame type or envelope length is invalid.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.DecodeKeepAlivePong(System.ReadOnlySpan<System.Byte>)`

Decodes a keepalive pong envelope.

Parameters:
- `data`: Encoded keepalive pong bytes.

Returns: The decoded keepalive pong envelope.

Exceptions:
- `System.InvalidOperationException`: Thrown when the frame type or envelope length is invalid.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.DecodePush(ULinkRPC.Core.TransportFrame)`

Decodes a server-to-client push envelope from a transport frame.

Parameters:
- `data`: Encoded push frame.

Returns: A decoded push frame whose payload slice references `data`.

Exceptions:
- `System.InvalidOperationException`: Thrown when the frame type or envelope length is invalid.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.DecodeRequest(ULinkRPC.Core.TransportFrame)`

Decodes a request envelope from a transport frame.

Parameters:
- `data`: Encoded request frame.

Returns: A decoded request frame whose payload slice references `data`.

Exceptions:
- `System.InvalidOperationException`: Thrown when the frame type or envelope length is invalid.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.DecodeResponse(ULinkRPC.Core.TransportFrame)`

Decodes a response envelope from a transport frame.

Parameters:
- `data`: Encoded response frame.

Returns: A decoded response frame whose payload slice references `data`.

Exceptions:
- `System.InvalidOperationException`: Thrown when the frame type or envelope length is invalid.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.EncodeKeepAlivePing(ULinkRPC.Core.RpcKeepAlivePingEnvelope)`

Encodes a keepalive ping envelope into a transport frame.

Parameters:
- `ping`: Ping timestamp data.

Returns: An owned transport frame containing the encoded keepalive ping.

Exceptions:
- `System.ArgumentNullException`: Thrown when `ping` is `null`.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.EncodeKeepAlivePong(ULinkRPC.Core.RpcKeepAlivePongEnvelope)`

Encodes a keepalive pong envelope into a transport frame.

Parameters:
- `pong`: Pong timestamp data.

Returns: An owned transport frame containing the encoded keepalive pong.

Exceptions:
- `System.ArgumentNullException`: Thrown when `pong` is `null`.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.EncodePush(ULinkRPC.Core.RpcPushEnvelope)`

Encodes a server-to-client push envelope into a transport frame.

Parameters:
- `push`: Push metadata and serialized method payload.

Returns: An owned transport frame containing the encoded push envelope.

Exceptions:
- `System.ArgumentNullException`: Thrown when `push` is `null`.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.EncodeRequest(ULinkRPC.Core.RpcRequestEnvelope)`

Encodes a request envelope into a transport frame.

Parameters:
- `req`: Request metadata and serialized method payload.

Returns: An owned transport frame containing the encoded request envelope.

Exceptions:
- `System.ArgumentNullException`: Thrown when `req` is `null`.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.EncodeResponse(System.UInt32,ULinkRPC.Core.RpcStatus,System.ReadOnlyMemory<System.Byte>,System.String)`

Encodes response fields into a transport frame.

Parameters:
- `requestId`: Identifier of the request being answered.
- `status`: Response status.
- `payload`: Serialized return payload or empty bytes for non-success responses.
- `errorMessage`: Optional UTF-8 error text included with the response.

Returns: An owned transport frame containing the encoded response envelope.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.EncodeResponse(ULinkRPC.Core.RpcResponseEnvelope)`

Encodes a response envelope into a transport frame.

Parameters:
- `resp`: Response metadata, status, serialized payload, and optional error message.

Returns: An owned transport frame containing the encoded response envelope.

Exceptions:
- `System.ArgumentNullException`: Thrown when `resp` is `null`.

### Method `ULinkRPC.Core.RpcEnvelopeCodec.PeekFrameType(System.ReadOnlySpan<System.Byte>)`

Reads the frame type byte from an encoded RPC envelope without decoding the full frame.

Parameters:
- `data`: Encoded envelope bytes.

Returns: The frame type stored in the first byte.

Exceptions:
- `System.InvalidOperationException`: Thrown when `data` is empty.

### Method `ULinkRPC.Core.RpcMethod.constructor(System.Int32,System.Int32)`

Creates a method descriptor from stable protocol ids.

Parameters:
- `serviceId`: Stable service id declared by `ULinkRPC.Core.RpcServiceAttribute`.
- `methodId`: Stable method id declared by `ULinkRPC.Core.RpcMethodAttribute`.

### Method `ULinkRPC.Core.RpcPushFrame.constructor(System.Int32,System.Int32,ULinkRPC.Core.TransportFrame)`

Initializes a decoded push frame.

Parameters:
- `serviceId`: Generated numeric identifier for the target client service.
- `methodId`: Generated numeric identifier for the target client method.
- `payload`: Serialized push payload.

### Method `ULinkRPC.Core.RpcPushFrame.Dispose`

Releases the underlying payload frame.

### Method `ULinkRPC.Core.RpcNotificationMethod.constructor(System.Int32,System.Int32)`

Creates a notification descriptor from stable protocol ids.

Parameters:
- `serviceId`: Stable service id declared by `ULinkRPC.Core.RpcServiceAttribute`.
- `methodId`: Stable notification method id declared by `ULinkRPC.Core.RpcNotificationAttribute`.

### Method `ULinkRPC.Core.RpcRequestFrame.constructor(System.UInt32,System.Int32,System.Int32,ULinkRPC.Core.TransportFrame)`

Initializes a decoded request frame.

Parameters:
- `requestId`: Client-assigned request identifier.
- `serviceId`: Generated numeric identifier for the target service.
- `methodId`: Generated numeric identifier for the target method.
- `payload`: Serialized method argument payload.

### Method `ULinkRPC.Core.RpcRequestFrame.Dispose`

Releases the underlying payload frame.

### Method `ULinkRPC.Core.RpcResponseFrame.constructor(System.UInt32,ULinkRPC.Core.RpcStatus,ULinkRPC.Core.TransportFrame,System.String)`

Initializes a decoded response frame.

Parameters:
- `requestId`: Identifier of the request being answered.
- `status`: Response status.
- `payload`: Serialized return payload.
- `errorMessage`: Optional server error message.

### Method `ULinkRPC.Core.RpcResponseFrame.Dispose`

Releases the underlying payload frame.

### Method `ULinkRPC.Core.TransportSecurityConfig.ResolveKey`

Resolves the configured encryption key.

Returns: `ULinkRPC.Core.TransportSecurityConfig.EncryptionKey` when present, otherwise decoded `ULinkRPC.Core.TransportSecurityConfig.EncryptionKeyBase64`, otherwise `null`.

Exceptions:
- `System.FormatException`: Thrown when `ULinkRPC.Core.TransportSecurityConfig.EncryptionKeyBase64` is not valid base64.

### Property `ULinkRPC.Core.IRpcConnectionAcceptor.ListenAddress`

Human-readable listen address for logs and diagnostics.

### Property `ULinkRPC.Core.RpcKeepAliveOptions.Disabled`

Shared disabled keepalive configuration.

### Property `ULinkRPC.Core.RpcKeepAliveOptions.Enabled`

Enables keepalive probing.

### Property `ULinkRPC.Core.RpcKeepAliveOptions.Interval`

Maximum time without receiving any frame before a keepalive ping is sent.

### Property `ULinkRPC.Core.RpcKeepAliveOptions.MeasureRtt`

Measures round-trip time from keepalive ping/pong timestamps when enabled.

### Property `ULinkRPC.Core.RpcKeepAliveOptions.Timeout`

Maximum time to wait for an inbound frame after a ping before disconnecting the session.

### Property `ULinkRPC.Core.RpcKeepAlivePingEnvelope.TimestampTicksUtc`

UTC timestamp ticks captured by the sender.

### Property `ULinkRPC.Core.RpcKeepAlivePongEnvelope.TimestampTicksUtc`

UTC timestamp ticks copied from the matching ping.

### Property `ULinkRPC.Core.RpcMethod.MethodId`

Stable method id used on the wire.

### Property `ULinkRPC.Core.RpcMethod.ServiceId`

Stable service id used on the wire.

### Property `ULinkRPC.Core.RpcPushEnvelope.MethodId`

Generated numeric identifier for the target client method.

### Property `ULinkRPC.Core.RpcPushEnvelope.Payload`

Serialized push payload.

### Property `ULinkRPC.Core.RpcPushEnvelope.ServiceId`

Generated numeric identifier for the target client service.

### Property `ULinkRPC.Core.RpcPushFrame.MethodId`

Generated numeric identifier for the target client method.

### Property `ULinkRPC.Core.RpcPushFrame.Payload`

Serialized push payload.

### Property `ULinkRPC.Core.RpcPushFrame.ServiceId`

Generated numeric identifier for the target client service.

### Property `ULinkRPC.Core.RpcNotificationMethod.MethodId`

Stable notification method id used on the wire.

### Property `ULinkRPC.Core.RpcNotificationMethod.ServiceId`

Stable service id used on the wire.

### Property `ULinkRPC.Core.RpcRequestEnvelope.MethodId`

Generated numeric identifier for the target method.

### Property `ULinkRPC.Core.RpcRequestEnvelope.Payload`

Serialized method argument payload.

### Property `ULinkRPC.Core.RpcRequestEnvelope.RequestId`

Client-assigned request identifier used to correlate the response.

### Property `ULinkRPC.Core.RpcRequestEnvelope.ServiceId`

Generated numeric identifier for the target service.

### Property `ULinkRPC.Core.RpcRequestFrame.MethodId`

Generated numeric identifier for the target method.

### Property `ULinkRPC.Core.RpcRequestFrame.Payload`

Serialized method argument payload.

### Property `ULinkRPC.Core.RpcRequestFrame.RequestId`

Client-assigned request identifier used to correlate the response.

### Property `ULinkRPC.Core.RpcRequestFrame.ServiceId`

Generated numeric identifier for the target service.

### Property `ULinkRPC.Core.RpcResponseEnvelope.ErrorMessage`

Optional server error message associated with non-success responses.

### Property `ULinkRPC.Core.RpcResponseEnvelope.Payload`

Serialized return payload.

### Property `ULinkRPC.Core.RpcResponseEnvelope.RequestId`

Identifier of the request being answered.

### Property `ULinkRPC.Core.RpcResponseEnvelope.Status`

Response status.

### Property `ULinkRPC.Core.RpcResponseFrame.ErrorMessage`

Optional server error message associated with non-success responses.

### Property `ULinkRPC.Core.RpcResponseFrame.Payload`

Serialized return payload.

### Property `ULinkRPC.Core.RpcResponseFrame.RequestId`

Identifier of the request being answered.

### Property `ULinkRPC.Core.RpcResponseFrame.Status`

Response status.

### Property `ULinkRPC.Core.TransportSecurityConfig.CompressionThresholdBytes`

Minimum frame size before compression is attempted.

### Property `ULinkRPC.Core.TransportSecurityConfig.EnableCompression`

Compresses frames before encryption and transmission when enabled.

### Property `ULinkRPC.Core.TransportSecurityConfig.EnableEncryption`

Encrypts transformed frames when enabled.

### Property `ULinkRPC.Core.TransportSecurityConfig.EncryptionKey`

Raw symmetric encryption key bytes.

### Property `ULinkRPC.Core.TransportSecurityConfig.EncryptionKeyBase64`

Base64-encoded symmetric encryption key. Used when `ULinkRPC.Core.TransportSecurityConfig.EncryptionKey` is not set.

### Property `ULinkRPC.Core.TransportSecurityConfig.IsEnabled`

Indicates whether any frame transformation is enabled.

### Property `ULinkRPC.Core.TransportSecurityConfig.MaxDecompressedFrameBytes`

Maximum allowed decompressed frame size.

### Type `ULinkRPC.Core.IRemoteEndPointProvider`

Optional interface for transports that can report the remote endpoint of the connected peer. Implement on server-side transports where the remote address is known (TCP, UDP/KCP, WebSocket with HTTP context, etc.).

### Type `ULinkRPC.Core.IRpcClient`

Runtime-facing RPC client abstraction used by generated client proxies.

Remarks: Application code normally uses the generated client facade instead of this interface directly. Implementations are responsible for request correlation, response decoding, and server push dispatch.

### Type `ULinkRPC.Core.IRpcConnectionAcceptor`

Server-side transport acceptor that yields accepted RPC connections.

Remarks: Concrete transports such as TCP, WebSocket, and KCP implement this interface. The server host owns the accept loop and creates one `RpcSession` per accepted connection.

### Type `ULinkRPC.Core.IRpcSerializer`

Serializer for RPC method payloads (arguments and return values). Envelope encoding is handled by `ULinkRPC.Core.RpcEnvelopeCodec`.

### Type `ULinkRPC.Core.ITransport`

Transport boundary for RPC: sends and receives complete frames (one message). TCP/WS/KCP differences are hidden below this interface.

### Property `ULinkRPC.Core.ITransport.IsConnected`

Best-known local connection state for diagnostics. This value is not a synchronization primitive and does not guarantee that the next send or receive will succeed.

### Type `ULinkRPC.Core.LengthPrefix`

Network framing: uint32 length prefix (big-endian) + payload bytes. Matches Unity client's LengthPrefix for wire compatibility.

### Type `ULinkRPC.Core.RpcNotificationContractAttribute`

Marks an interface as the server-to-client notification contract for a specific RPC service.

### Type `ULinkRPC.Core.RpcEnvelopeCodec`

Encodes and decodes ULinkRPC wire envelopes.

Remarks: The codec serializes only the transport envelope fields. RPC method payloads are opaque bytes produced by an `ULinkRPC.Core.IRpcSerializer`.

### Type `ULinkRPC.Core.RpcFrameType`

Identifies the kind of RPC envelope stored in a transport frame.

### Type `ULinkRPC.Core.RpcKeepAliveOptions`

Peer liveness probing for RPC transports. Only inbound traffic proves the remote peer is alive; outbound sends do not suppress probes.

### Type `ULinkRPC.Core.RpcKeepAlivePingEnvelope`

Keepalive ping envelope sent to measure liveness and optional round-trip time.

### Type `ULinkRPC.Core.RpcKeepAlivePongEnvelope`

Keepalive pong envelope sent in response to a keepalive ping.

### Type `ULinkRPC.Core.RpcMethod`

Typed descriptor for a client-to-server RPC method.

Type parameters:
- `TArg`: Request DTO type.
- `TResult`: Response DTO type.

### Type `ULinkRPC.Core.RpcMethodAttribute`

Marks an interface method as an RPC method. MethodId must be stable within a service. ULinkRPC source generation requires exactly one request DTO parameter and generates payload packing/unpacking for it.

### Type `ULinkRPC.Core.RpcProtocolLimits`

Central defaults for RPC protocol payload, transport frame, and security transform limits.

### Type `ULinkRPC.Core.RpcNotificationAttribute`

Marks an interface method as a server-to-client notification. MethodId must be stable within a notification contract.

### Type `ULinkRPC.Core.RpcPushEnvelope`

Mutable push envelope used before encoding a server-to-client notification.

### Type `ULinkRPC.Core.RpcPushFrame`

Decoded push envelope with an owned payload frame slice.

### Type `ULinkRPC.Core.RpcNotificationMethod`

Typed descriptor for a server-to-client notification method.

Type parameters:
- `TArg`: Notification DTO type.

### Type `ULinkRPC.Core.RpcRequestEnvelope`

Mutable request envelope used before encoding a client-to-server RPC request.

### Type `ULinkRPC.Core.RpcRequestFrame`

Decoded request envelope with an owned payload frame slice.

### Type `ULinkRPC.Core.RpcResponseEnvelope`

Mutable response envelope used before encoding a server-to-client RPC response.

### Type `ULinkRPC.Core.RpcResponseFrame`

Decoded response envelope with an owned payload frame slice.

### Type `ULinkRPC.Core.RpcServiceAttribute`

Marks an interface as an RPC service. ServiceId must be stable across versions.

### Property `ULinkRPC.Core.RpcServiceAttribute.ApiGroup`

Optional generated `client.Api.<group>` group name. Use this to keep generated facade names stable across namespace refactors.

### Property `ULinkRPC.Core.RpcServiceAttribute.ApiName`

Optional generated `client.Api.<group>.<service>` property name. Use this to keep generated facade names stable across interface renames.

### Type `ULinkRPC.Core.RpcStatus`

Describes framework-level RPC response outcomes only. It does not represent business failures. Non-`Ok` responses are surfaced by the client runtime as `ULinkRPC.Core.RpcException`; use `RpcException.Status` for machine-readable observability and retry decisions.

### Type `ULinkRPC.Core.RpcVoid`

Singleton marker value used for RPC methods with no return payload.

### Type `ULinkRPC.Core.TransportSecurityConfig`

Frame transformation settings applied above a concrete transport.

Remarks: This configuration controls optional compression and symmetric frame encryption performed by `ULinkRPC.Core.TransformingTransport`. It is not a TLS replacement and does not authenticate the remote peer by itself.

## ULinkRPC.Client

### Event `ULinkRPC.Client.RpcClientRuntime.Disconnected`

Raised when the receive loop ends.

Remarks: The event argument is the disconnect reason when one is available. A null value means a normal or locally requested shutdown.

### Method `ULinkRPC.Client.RpcClientOptions.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer)`

Creates client runtime options.

Parameters:
- `transport`: Concrete transport used by the client.
- `serializer`: Serializer used for RPC method payloads.

Exceptions:
- `System.ArgumentNullException`: Thrown when `transport` or `serializer` is null.

### Method `ULinkRPC.Client.RpcClientOptions.CreateConfiguredTransport`

Returns the transport that should be passed to the runtime.

Returns: The original transport when security is disabled; otherwise a `ULinkRPC.Core.TransformingTransport` wrapping the original transport.

### Method `ULinkRPC.Client.RpcClientOptions.UseSecurity(System.Action<ULinkRPC.Core.TransportSecurityConfig>)`

Configures compression or encryption for the client transport.

Parameters:
- `configure`: Configuration callback.

Returns: This options instance.

Exceptions:
- `System.ArgumentNullException`: Thrown when `configure` is null.

### Method `ULinkRPC.Client.RpcClientRuntime.constructor(ULinkRPC.Client.RpcClientOptions)`

Creates a runtime from client options.

Parameters:
- `options`: Client options containing transport, serializer, keepalive, and security settings.

Exceptions:
- `System.ArgumentNullException`: Thrown when `options` is null.

### Method `ULinkRPC.Client.RpcClientRuntime.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,ULinkRPC.Core.RpcKeepAliveOptions)`

Creates a runtime from explicit transport and serializer instances.

Parameters:
- `transport`: Connected or connectable transport used by the runtime.
- `serializer`: Serializer used for RPC payloads.
- `keepAlive`: Optional keepalive configuration.

Exceptions:
- `System.ArgumentNullException`: Thrown when `transport` or `serializer` is null.

### Method `ULinkRPC.Client.RpcClientRuntime.DisposeAsync`

Stops background loops, fails pending requests, and disposes the transport.

### Method `ULinkRPC.Client.RpcClientRuntime.StartAsync(System.Threading.CancellationToken)`

Connects the transport and starts background runtime loops.

Remarks: A runtime is a single-use connection object. Calling `StartAsync` after it has already started throws `InvalidOperationException`; after disconnect or dispose, create a new runtime instead of restarting the old one.

Parameters:
- `ct`: Cancellation token for the initial transport connection.

Exceptions:
- `System.InvalidOperationException`: Thrown when the runtime has already been started.
- `System.ObjectDisposedException`: Thrown when the runtime has been disposed.

### Property `ULinkRPC.Client.RpcClientOptions.KeepAlive`

Optional keepalive configuration. Disabled by default.

### Property `ULinkRPC.Client.RpcClientOptions.Security`

Mutable frame security configuration.

### Property `ULinkRPC.Client.RpcClientOptions.Serializer`

Serializer used for request, response, and push payloads.

### Property `ULinkRPC.Client.RpcClientOptions.Transport`

Underlying transport before optional security wrapping.

### Property `ULinkRPC.Client.RpcClientRuntime.LastReceiveAt`

Last UTC timestamp at which the runtime received a frame.

### Property `ULinkRPC.Client.RpcClientRuntime.LastRtt`

Last measured keepalive round-trip time, when RTT measurement is enabled.

### Property `ULinkRPC.Client.RpcClientRuntime.LastSendAt`

Last UTC timestamp at which the runtime sent a frame.

### Property `ULinkRPC.Client.RpcClientRuntime.TimedOutByKeepAlive`

Indicates whether the runtime stopped because keepalive timed out.

### Type `ULinkRPC.Client.RpcClientOptions`

Configuration used to create a client runtime or generated client facade.

Remarks: The options object owns the selected transport and serializer references. If security is configured, `ULinkRPC.Client.RpcClientOptions.CreateConfiguredTransport` wraps the transport in a `ULinkRPC.Core.TransformingTransport`.

### Type `ULinkRPC.Client.RpcClientRuntime`

Default client runtime for ULinkRPC request/response calls and server notification dispatch.

Remarks: The runtime owns background receive, notification, and keepalive loops after `ULinkRPC.Client.RpcClientRuntime.StartAsync(System.Threading.CancellationToken)`. Notification handlers run on the runtime notification loop and are not marshalled to the Unity main thread.

### Type `ULinkRPC.Client.RpcNotificationPayloadHandler`

Handles a serialized server-to-client notification payload.

Parameters:
- `payload`: Serialized notification payload.

## ULinkRPC.Server

### Event `ULinkRPC.Server.RpcSession.Disconnected`

Raised when the session receive loop ends.

### Method `ULinkRPC.Server.RpcServerHostBuilder.BindGeneratedServicesFromAssembly(System.Reflection.Assembly)`

Binds generated services from an assembly.

Parameters:
- `assembly`: Assembly containing generated binder metadata.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.BindGeneratedServicesFromAssemblyContaining`

Binds generated services from the assembly containing `T`.

Type parameters:
- `T`: Type whose assembly contains generated service binders.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.BindGeneratedServicesFromEntryAssembly`

Binds generated services from the process entry assembly.

Returns: This builder.

Exceptions:
- `System.InvalidOperationException`: Thrown when the entry assembly cannot be resolved.

### Method `ULinkRPC.Server.RpcServerHostBuilder.Build`

Builds a server host.

Returns: A configured server host.

Exceptions:
- `System.InvalidOperationException`: Thrown when serializer or transport configuration is missing.

### Method `ULinkRPC.Server.RpcServerHostBuilder.ConfigureServices(System.Action<ULinkRPC.Server.RpcServiceRegistry>)`

Manually configures service handlers.

Parameters:
- `configure`: Registry configuration callback.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.Create`

Creates a new server host builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.ResolvePort(System.Int32)`

Resolves the configured port or returns a default.

Parameters:
- `defaultPort`: Default port used when no port was configured.

Returns: The configured port or `defaultPort`.

### Method `ULinkRPC.Server.RpcServerHostBuilder.RunAsync(System.Threading.CancellationToken)`

Builds and runs the server host.

Parameters:
- `ct`: Cancellation token for the host run loop.

Returns: A task that completes when the host stops.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseAcceptor(System.Func<System.Threading.CancellationToken,System.Threading.Tasks.ValueTask<ULinkRPC.Core.IRpcConnectionAcceptor>>)`

Sets a factory for creating the transport acceptor.

Parameters:
- `acceptorFactory`: Factory invoked by the host run loop.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseAcceptor(ULinkRPC.Core.IRpcConnectionAcceptor)`

Sets a pre-created transport acceptor.

Parameters:
- `acceptor`: Connection acceptor.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseCommandLine(System.String[])`

Applies supported command-line options to the builder.

Parameters:
- `args`: Command-line arguments, or null.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseKeepAlive(System.TimeSpan,System.TimeSpan)`

Enables keepalive with an interval and timeout.

Parameters:
- `interval`: Maximum idle receive time before sending a ping.
- `timeout`: Maximum idle receive time after a ping before disconnecting.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseKeepAlive(ULinkRPC.Core.RpcKeepAliveOptions)`

Sets keepalive options for accepted sessions.

Parameters:
- `keepAlive`: Keepalive options.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseLimits(System.Action<ULinkRPC.Server.RpcServerLimits>)`

Mutates server back-pressure limits.

Parameters:
- `configure`: Limit configuration callback.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseLimits(ULinkRPC.Server.RpcServerLimits)`

Copies server back-pressure limits from another instance.

Parameters:
- `limits`: Limits to copy.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseLogger(Microsoft.Extensions.Logging.ILogger)`

Uses an `Microsoft.Extensions.Logging.ILogger` instance.

Parameters:
- `logger`: Logger instance.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseLogger(System.Action<System.String>)`

Uses a delegate-backed logger.

Parameters:
- `logger`: Log sink.

Returns: This builder.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UsePort(System.Int32)`

Sets the server port used by transport configuration helpers.

Parameters:
- `port`: TCP/UDP port between 1 and 65535.

Returns: This builder.

Exceptions:
- `System.ArgumentOutOfRangeException`: Thrown when `port` is outside 1..65535.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseSecurity(System.Action<ULinkRPC.Core.TransportSecurityConfig>)`

Configures frame compression or encryption for accepted sessions.

Parameters:
- `configure`: Configuration callback.

Returns: This builder.

Exceptions:
- `System.ArgumentNullException`: Thrown when `configure` is null.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseSerializer(ULinkRPC.Core.IRpcSerializer)`

Sets the serializer used by all accepted sessions.

Parameters:
- `serializer`: Payload serializer.

Returns: This builder.

Exceptions:
- `System.ArgumentNullException`: Thrown when `serializer` is null.

### Method `ULinkRPC.Server.RpcServerHostBuilder.UseSerializer`

Creates and sets a serializer using its public parameterless constructor.

Type parameters:
- `TSerializer`: Serializer type.

Returns: This builder.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,System.Boolean)`

Creates a session and optionally disposes the transport when the session is disposed.

Parameters:
- `transport`: Transport for this connection.
- `serializer`: Serializer used for RPC payloads.
- `ownsTransport`: Whether disposing the session also disposes the transport.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,System.String,System.Boolean)`

Creates a session with an explicit context id and transport ownership setting.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,System.String)`

Creates a session with an explicit context id.

Parameters:
- `transport`: Transport for this connection.
- `serializer`: Serializer used for RPC payloads.
- `contextId`: Stable session id used in logs and scoped services.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,ULinkRPC.Server.RpcServiceRegistry,System.Boolean)`

Creates a session backed by a service registry and optional transport ownership.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,ULinkRPC.Server.RpcServiceRegistry,System.String,System.Boolean,ULinkRPC.Core.RpcKeepAliveOptions,Microsoft.Extensions.Logging.ILogger,ULinkRPC.Server.RpcServerLimits)`

Creates a fully configured session.

Parameters:
- `transport`: Transport for this connection.
- `serializer`: Serializer used for RPC payloads.
- `registry`: Optional generated service registry.
- `contextId`: Stable session id used in logs and scoped services.
- `ownsTransport`: Whether disposing the session also disposes the transport.
- `keepAlive`: Optional keepalive configuration.
- `logger`: Optional logger.
- `limits`: Optional request concurrency and queue limits.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,ULinkRPC.Server.RpcServiceRegistry,System.String)`

Creates a session backed by a service registry with an explicit context id.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer,ULinkRPC.Server.RpcServiceRegistry)`

Creates a session backed by a service registry.

### Method `ULinkRPC.Server.RpcSession.constructor(ULinkRPC.Core.ITransport,ULinkRPC.Core.IRpcSerializer)`

Creates a session that does not own the transport.

Parameters:
- `transport`: Transport for this connection.
- `serializer`: Serializer used for RPC payloads.

### Method `ULinkRPC.Server.RpcSession.DisposeAsync`

Stops the session and disposes owned resources.

### Method `ULinkRPC.Server.RpcSession.GetOrAddScopedService(System.Int32,System.Func<ULinkRPC.Server.RpcSession,T0>)`

Gets or creates a service instance scoped to this session and service id.

Parameters:
- `serviceId`: Stable service id.
- `factory`: Factory invoked once per session and service id.

Type parameters:
- `TService`: Service implementation type.

Returns: The existing or newly created service instance.

### Method `ULinkRPC.Server.RpcSession.SendNotificationAsync(System.Int32,System.Int32,T0,System.Threading.CancellationToken)`

Sends a server-to-client notification.

Parameters:
- `serviceId`: Stable service id.
- `methodId`: Stable notification method id.
- `arg`: Notification DTO instance.
- `ct`: Cancellation token for the send operation.

Type parameters:
- `TArg`: Notification DTO type.

### Method `ULinkRPC.Server.RpcSession.Register(System.Int32,System.Int32,ULinkRPC.Server.RpcHandler)`

Registers a low-level request handler for one service method.

Parameters:
- `serviceId`: Stable service id.
- `methodId`: Stable method id.
- `handler`: Request handler.

### Method `ULinkRPC.Server.RpcSession.RunAsync(System.Threading.CancellationToken)`

Starts the session, waits for completion, and stops it in a finally block.

Parameters:
- `ct`: Cancellation token linked to the session loop.

### Method `ULinkRPC.Server.RpcSession.StartAsync(System.Threading.CancellationToken)`

Connects the transport and starts the session receive loop.

Remarks: A session represents one accepted connection. Calling `StartAsync` after the session has already started, stopped, disconnected, or been disposed throws `InvalidOperationException`.

Parameters:
- `ct`: Cancellation token for the initial transport connection.

Exceptions:
- `System.InvalidOperationException`: Thrown when the session has already been started.

### Method `ULinkRPC.Server.RpcSession.StopAsync`

Requests session shutdown and waits for in-flight requests to complete.

Remarks: `StopAsync` is idempotent cleanup, not a pause operation. After it completes, the session is terminal and cannot be started again.

### Method `ULinkRPC.Server.RpcSession.WaitForCompletionAsync`

Waits until the session receive loop and in-flight requests complete.

### Property `ULinkRPC.Server.RpcServerHostBuilder.KeepAlive`

Keepalive configuration used for accepted sessions.

### Property `ULinkRPC.Server.RpcServerHostBuilder.Limits`

Server back-pressure limits.

### Property `ULinkRPC.Server.RpcServerHostBuilder.Port`

Explicit server port set by command line parsing or `ULinkRPC.Server.RpcServerHostBuilder.UsePort(System.Int32)`.

### Property `ULinkRPC.Server.RpcServerHostBuilder.Security`

Frame security configuration applied to accepted transports.

### Property `ULinkRPC.Server.RpcServerHostBuilder.ServiceRegistry`

Registry used for generated and manually configured service handlers.

### Property `ULinkRPC.Server.RpcSession.ContextId`

Unique identifier for this connection session.

### Property `ULinkRPC.Server.RpcSession.LastReceiveAt`

Last UTC timestamp at which this session received a frame.

### Property `ULinkRPC.Server.RpcSession.LastSendAt`

Last UTC timestamp at which this session sent a frame.

### Property `ULinkRPC.Server.RpcSession.RemoteEndPoint`

Remote endpoint of the connected client, if the underlying transport supports it.

### Type `ULinkRPC.Server.RpcHandler`

Low-level handler for a decoded RPC request.

Parameters:
- `req`: Request envelope.
- `ct`: Cancellation token for request processing.

Returns: Response envelope to send back to the client.

### Type `ULinkRPC.Server.RpcServerHostBuilder`

Builder for a multi-session RPC server host.

Remarks: The builder composes serializer, transport acceptor, generated service binding, keepalive, frame security, logging, and back-pressure limits. `ULinkRPC.Server.RpcServerHostBuilder.Build` validates required configuration before creating the host.

### Type `ULinkRPC.Server.RpcSession`

Runtime for one accepted client connection.

Remarks: A session owns receive, dispatch, optional keepalive, and server push for one transport connection. Generated server binders usually create session-scoped service instances through `ULinkRPC.Server.RpcSession.GetOrAddScopedService(System.Int32,System.Func)`.

