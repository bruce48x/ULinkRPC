using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using ULinkRPC.Core;

namespace ULinkRPC.Server
{
    public delegate ValueTask<RpcResponseEnvelope> RpcHandler(RpcRequestEnvelope req, CancellationToken ct);

    public sealed class RpcSession : IAsyncDisposable
    {
        private const string HandlerExecutionErrorMessage = "RPC handler failed.";
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(int serviceId, int methodId), RpcHandler> _handlers = new();
        private readonly TrackedTaskCollection _inflightRequests = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, object> _scopedServices = new();
        private readonly RpcKeepAliveState _keepAliveState;
        private readonly RpcServiceRegistry? _registry;
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly RpcKeepAliveOptions _keepAlive;
        private readonly RpcServerLimits _limits;
        private readonly ILogger _logger;
        private readonly bool _ownsTransport;
        private readonly SemaphoreSlim _requestConcurrencyGate;
        private readonly SemaphoreSlim _requestBudget;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private Task? _keepAliveLoop;
        private Task? _loop;
        private int _disposed;
        private int _started;
        private int _transportDisposed;
        private long _disconnectReasonSet;
        private Exception? _disconnectReason;

        public RpcSession(ITransport transport, IRpcSerializer serializer)
            : this(transport, serializer, registry: null, Guid.NewGuid().ToString("N"), false, keepAlive: null)
        {
        }

        public RpcSession(ITransport transport, IRpcSerializer serializer, bool ownsTransport)
            : this(transport, serializer, registry: null, Guid.NewGuid().ToString("N"), ownsTransport, keepAlive: null)
        {
        }

        public RpcSession(ITransport transport, IRpcSerializer serializer, string contextId)
            : this(transport, serializer, registry: null, contextId, false, keepAlive: null)
        {
        }

        public RpcSession(ITransport transport, IRpcSerializer serializer, string contextId, bool ownsTransport)
            : this(transport, serializer, registry: null, contextId, ownsTransport, keepAlive: null)
        {
        }

        public RpcSession(ITransport transport, IRpcSerializer serializer, RpcServiceRegistry registry)
            : this(transport, serializer, registry, Guid.NewGuid().ToString("N"), false, keepAlive: null)
        {
        }

        public RpcSession(ITransport transport, IRpcSerializer serializer, RpcServiceRegistry registry, bool ownsTransport)
            : this(transport, serializer, registry, Guid.NewGuid().ToString("N"), ownsTransport, keepAlive: null)
        {
        }

        public RpcSession(ITransport transport, IRpcSerializer serializer, RpcServiceRegistry registry, string contextId)
            : this(transport, serializer, registry, contextId, false, keepAlive: null)
        {
        }

        public RpcSession(
            ITransport transport,
            IRpcSerializer serializer,
            RpcServiceRegistry? registry,
            string contextId,
            bool ownsTransport,
            RpcKeepAliveOptions? keepAlive = null,
            ILogger? logger = null,
            RpcServerLimits? limits = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _registry = registry;
            _ownsTransport = ownsTransport;
            _keepAlive = keepAlive ?? RpcKeepAliveOptions.Disabled;
            _logger = logger ?? DefaultRpcLogging.CreateLogger<RpcSession>();
            _limits = limits?.Clone() ?? new RpcServerLimits();
            _limits.Validate();
            _requestConcurrencyGate = new SemaphoreSlim(
                _limits.MaxConcurrentRequestsPerSession,
                _limits.MaxConcurrentRequestsPerSession);
            checked
            {
                var requestBudget = _limits.MaxConcurrentRequestsPerSession + _limits.MaxQueuedRequestsPerSession;
                _requestBudget = new SemaphoreSlim(requestBudget, requestBudget);
            }
            _keepAliveState = new RpcKeepAliveState(_keepAlive.MeasureRtt);
            ContextId = contextId ?? throw new ArgumentNullException(nameof(contextId));
            RemoteEndPoint = ResolveRemoteEndPoint(_transport);
        }

        /// <summary>
        ///     Unique identifier for this connection session.
        /// </summary>
        public string ContextId { get; }

        /// <summary>
        ///     Remote endpoint of the connected client, if the underlying transport supports it.
        /// </summary>
        public IPEndPoint? RemoteEndPoint { get; private set; }

        public string? RemoteAddress => RemoteEndPoint?.Address.ToString();

        public int? RemotePort => RemoteEndPoint?.Port;

        public IRpcSerializer Serializer => _serializer;

        public DateTimeOffset LastSendAt => _keepAliveState.LastSendAt;

        public DateTimeOffset LastReceiveAt => _keepAliveState.LastReceiveAt;

        public event Action<Exception?>? Disconnected;

        public void Register(int serviceId, int methodId, RpcHandler handler)
        {
            ThrowIfDisposed();
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            _handlers[(serviceId, methodId)] = handler;
        }

        public TService GetOrAddScopedService<TService>(int serviceId, Func<RpcSession, TService> factory)
            where TService : class
        {
            ThrowIfDisposed();
            if (factory is null) throw new ArgumentNullException(nameof(factory));

            var service = _scopedServices.GetOrAdd(serviceId, _ =>
                factory(this) ?? throw new InvalidOperationException($"Service factory returned null for service id {serviceId}."));

            return (TService)service;
        }

        public async ValueTask PushAsync<TArg>(int serviceId, int methodId, TArg arg, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            using var payload = arg is null ? TransportFrame.Empty : _serializer.SerializeFrame(arg);
            var push = new RpcPushEnvelope
            {
                ServiceId = serviceId,
                MethodId = methodId,
                Payload = payload.Memory
            };
            using var bytes = RpcEnvelopeCodec.EncodePush(push);
            await SendFrameAsyncSerialized(bytes.Memory, ct).ConfigureAwait(false);
        }

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("RpcSession already started.");

            try
            {
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                _keepAliveState.MarkSent();
                _keepAliveState.MarkReceived();
                RemoteEndPoint ??= ResolveRemoteEndPoint(_transport);
                _cts = new CancellationTokenSource();
                var serverCts = _cts;
                _loop = Task.Run(() => LoopAsync(serverCts));
                if (_keepAlive.Enabled)
                    _keepAliveLoop = Task.Run(() => KeepAliveLoopAsync(serverCts));
            }
            catch
            {
                if (_cts is not null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
                _loop = null;
                Interlocked.Exchange(ref _started, 0);
                throw;
            }
        }

        public async ValueTask WaitForCompletionAsync()
        {
            if (_loop is null)
                return;

            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            await _inflightRequests.WaitAsync().ConfigureAwait(false);
        }

        public async ValueTask RunAsync(CancellationToken ct = default)
        {
            await StartAsync(ct).ConfigureAwait(false);

            try
            {
                await WaitForCompletionAsync().ConfigureAwait(false);
            }
            finally
            {
                await StopAsync().ConfigureAwait(false);
            }
        }

        private async Task LoopAsync(CancellationTokenSource? serverCts)
        {
            if (serverCts is null) return;

            var ct = serverCts.Token;
            Exception? disconnectError = null;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TransportFrame frame;
                    try
                    {
                        frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (InvalidOperationException) when (!_transport.IsConnected)
                    {
                        break;
                    }

                    using (frame)
                    {
                    if (frame.Length == 0)
                        break;

                    _keepAliveState.MarkReceived();
                    var frameType = RpcEnvelopeCodec.PeekFrameType(frame.Span);
                    if (frameType == RpcFrameType.KeepAlivePing)
                    {
                        var ping = RpcEnvelopeCodec.DecodeKeepAlivePing(frame.Span);
                        using var pongBytes = RpcEnvelopeCodec.EncodeKeepAlivePong(new RpcKeepAlivePongEnvelope
                        {
                            TimestampTicksUtc = ping.TimestampTicksUtc
                        });
                        await SendFrameAsyncSerialized(pongBytes.Memory, ct).ConfigureAwait(false);
                        continue;
                    }

                    if (frameType != RpcFrameType.Request)
                        continue;

                    var req = RpcEnvelopeCodec.DecodeRequest(frame);
                    EnqueueRequestProcessing(req, ct);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    disconnectError = ex;
            }
            finally
            {
                if (disconnectError is null)
                    disconnectError = _disconnectReason;
                await _inflightRequests.WaitAsync().ConfigureAwait(false);
                ResetRuntimeState(serverCts);
                Disconnected?.Invoke(disconnectError);
            }
        }

        private async Task KeepAliveLoopAsync(CancellationTokenSource? serverCts)
        {
            if (serverCts is null)
                return;

            var ct = serverCts.Token;
            var interval = _keepAlive.Interval;
            var timeout = _keepAlive.Timeout;
            if (interval <= TimeSpan.Zero || timeout <= TimeSpan.Zero)
                return;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
                switch (_keepAliveState.GetNextAction(nowTicks, interval, timeout))
                {
                    case RpcKeepAliveAction.None:
                        continue;
                    case RpcKeepAliveAction.TimedOut:
                        SetDisconnectReason(new TimeoutException("RPC session keepalive timed out."));
                        try
                        {
                            serverCts.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        return;
                    case RpcKeepAliveAction.SendPing:
                        break;
                }

                try
                {
                    var pingTimestamp = DateTimeOffset.UtcNow.UtcTicks;
                    using var pingBytes = RpcEnvelopeCodec.EncodeKeepAlivePing(new RpcKeepAlivePingEnvelope
                    {
                        TimestampTicksUtc = pingTimestamp
                    });
                    await SendFrameAsyncSerialized(pingBytes.Memory, ct).ConfigureAwait(false);
                    _keepAliveState.MarkPingSent(pingTimestamp);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException) when (!_transport.IsConnected)
                {
                    return;
                }
            }
        }

        private void EnqueueRequestProcessing(RpcRequestFrame req, CancellationToken ct)
        {
            if (!_requestBudget.Wait(0))
            {
                _logger.LogWarning(
                    "[{ContextId}] Rejected request {RequestId} for service {ServiceId} method {MethodId} because the session request queue is full.",
                    ContextId,
                    req.RequestId,
                    req.ServiceId,
                    req.MethodId);
                var requestId = req.RequestId;
                req.Dispose();
                _inflightRequests.Track(SendOverloadedResponseAsync(requestId, ct));
                return;
            }

            var task = ProcessRequestAsync(req, ct);
            _inflightRequests.Track(task);
        }

        private async Task ProcessRequestAsync(RpcRequestFrame req, CancellationToken ct)
        {
            var enteredConcurrencyGate = false;
            RpcResponseEnvelope resp;
            try
            {
                await _requestConcurrencyGate.WaitAsync(ct).ConfigureAwait(false);
                enteredConcurrencyGate = true;

                if (_handlers.TryGetValue((req.ServiceId, req.MethodId), out var handler))
                {
                    try
                    {
                        resp = await handler(new RpcRequestEnvelope
                        {
                            RequestId = req.RequestId,
                            ServiceId = req.ServiceId,
                            MethodId = req.MethodId,
                            Payload = req.Payload.Memory
                        }, ct).ConfigureAwait(false);
                        if (resp is null)
                        {
                            resp = new RpcResponseEnvelope
                            {
                                RequestId = req.RequestId,
                                Status = RpcStatus.Exception,
                                Payload = Array.Empty<byte>(),
                                ErrorMessage = "RPC handler returned null response."
                            };
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogHandlerFailure(req, ex);
                        resp = new RpcResponseEnvelope
                        {
                            RequestId = req.RequestId,
                            Status = RpcStatus.Exception,
                            Payload = Array.Empty<byte>(),
                            ErrorMessage = HandlerExecutionErrorMessage
                        };
                    }
                }
                else if (_registry is not null && _registry.TryGetHandler(req.ServiceId, req.MethodId, out var sessionHandler))
                {
                    try
                    {
                        resp = await sessionHandler(this, new RpcRequestEnvelope
                        {
                            RequestId = req.RequestId,
                            ServiceId = req.ServiceId,
                            MethodId = req.MethodId,
                            Payload = req.Payload.Memory
                        }, ct).ConfigureAwait(false);
                        if (resp is null)
                        {
                            resp = new RpcResponseEnvelope
                            {
                                RequestId = req.RequestId,
                                Status = RpcStatus.Exception,
                                Payload = Array.Empty<byte>(),
                                ErrorMessage = "RPC handler returned null response."
                            };
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogHandlerFailure(req, ex);
                        resp = new RpcResponseEnvelope
                        {
                            RequestId = req.RequestId,
                            Status = RpcStatus.Exception,
                            Payload = Array.Empty<byte>(),
                            ErrorMessage = HandlerExecutionErrorMessage
                        };
                    }
                }
                else
                {
                    resp = new RpcResponseEnvelope
                    {
                        RequestId = req.RequestId,
                        Status = RpcStatus.NotFound,
                        Payload = Array.Empty<byte>(),
                        ErrorMessage = $"No handler for {req.ServiceId}:{req.MethodId}"
                    };
                }

                using var respBytes = RpcEnvelopeCodec.EncodeResponse(resp);
                await SendFrameAsyncSerialized(respBytes.Memory, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) when (!_transport.IsConnected)
            {
            }
            finally
            {
                req.Dispose();
                if (enteredConcurrencyGate)
                    _requestConcurrencyGate.Release();

                _requestBudget.Release();
            }
        }

        private async Task SendOverloadedResponseAsync(uint requestId, CancellationToken ct)
        {
            var response = new RpcResponseEnvelope
            {
                RequestId = requestId,
                Status = RpcStatus.Exception,
                Payload = Array.Empty<byte>(),
                ErrorMessage = "RPC server is overloaded; request queue is full."
            };

            try
            {
                using var respBytes = RpcEnvelopeCodec.EncodeResponse(response);
                await SendFrameAsyncSerialized(respBytes.Memory, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) when (!_transport.IsConnected)
            {
            }
        }

        private async ValueTask SendFrameAsyncSerialized(ReadOnlyMemory<byte> frame, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _transport.SendFrameAsync(frame, ct).ConfigureAwait(false);
                _keepAliveState.MarkSent();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void LogHandlerFailure(RpcRequestFrame req, Exception ex)
        {
            _logger.LogError(
                ex,
                "RPC handler failed for request {RequestId} service {ServiceId} method {MethodId} in session {ContextId}.",
                req.RequestId,
                req.ServiceId,
                req.MethodId,
                ContextId);
        }

        private void ResetRuntimeState(CancellationTokenSource serverCts)
        {
            _scopedServices.Clear();

            if (ReferenceEquals(_cts, serverCts))
            {
                _cts = null;
                try
                {
                    serverCts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _loop = null;
            _keepAliveLoop = null;
            Interlocked.Exchange(ref _started, 0);
        }

        public async ValueTask StopAsync()
        {
            var cts = _cts;
            var loop = _loop;
            var keepAliveLoop = _keepAliveLoop;

            if (cts is not null)
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

            if (loop is not null)
                try
                {
                    await loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

            if (keepAliveLoop is not null)
                try
                {
                    await keepAliveLoop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

            await _inflightRequests.WaitAsync().ConfigureAwait(false);

            if (cts is not null && ReferenceEquals(_cts, cts))
            {
                _cts = null;
                try
                {
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _loop = null;
            _keepAliveLoop = null;
            Interlocked.Exchange(ref _started, 0);
            _scopedServices.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await StopAsync().ConfigureAwait(false);
            await DisposeOwnedTransportIfNeededAsync().ConfigureAwait(false);
            _requestConcurrencyGate.Dispose();
            _requestBudget.Dispose();
            _sendLock.Dispose();
        }

        private async ValueTask DisposeOwnedTransportIfNeededAsync()
        {
            if (!_ownsTransport)
                return;

            if (Interlocked.Exchange(ref _transportDisposed, 1) != 0)
                return;

            await _transport.DisposeAsync().ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(RpcSession));
        }

        private static IPEndPoint? ResolveRemoteEndPoint(ITransport transport)
        {
            return (transport as IRemoteEndPointProvider)?.RemoteEndPoint as IPEndPoint;
        }

        private void SetDisconnectReason(Exception ex)
        {
            if (Interlocked.CompareExchange(ref _disconnectReasonSet, 1, 0) == 0)
                _disconnectReason = ex;
        }
    }
}
