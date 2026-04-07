using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using ULinkRPC.Core;

namespace ULinkRPC.Server
{
    public delegate ValueTask<RpcResponseEnvelope> RpcHandler(RpcRequestEnvelope req, CancellationToken ct);

    public sealed class RpcSession : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<(int serviceId, int methodId), RpcHandler> _handlers = new();
        private readonly ConcurrentDictionary<int, Task> _inflightRequests = new();
        private readonly ConcurrentDictionary<int, object> _scopedServices = new();
        private readonly RpcServiceRegistry? _registry;
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly RpcKeepAliveOptions _keepAlive;
        private readonly bool _ownsTransport;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private Task? _keepAliveLoop;
        private Task? _loop;
        private int _disposed;
        private int _nextInFlightRequestId;
        private long _pendingPingSentAtTicksUtc;
        private int _started;
        private int _transportDisposed;
        private long _disconnectReasonSet;
        private long _lastReceiveTicksUtc;
        private long _lastSendTicksUtc;
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

        public RpcSession(ITransport transport, IRpcSerializer serializer, RpcServiceRegistry? registry, string contextId, bool ownsTransport, RpcKeepAliveOptions? keepAlive = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _registry = registry;
            _ownsTransport = ownsTransport;
            _keepAlive = keepAlive ?? RpcKeepAliveOptions.Disabled;
            ContextId = contextId ?? throw new ArgumentNullException(nameof(contextId));
            RemoteEndPoint = ResolveRemoteEndPoint(_transport);
            UpdateSendActivityUtc();
            UpdateReceiveActivityUtc();
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

        public DateTimeOffset LastSendAt => new(GetTimestampOrNow(_lastSendTicksUtc), TimeSpan.Zero);

        public DateTimeOffset LastReceiveAt => new(GetTimestampOrNow(_lastReceiveTicksUtc), TimeSpan.Zero);

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
            var payload = arg is null ? Array.Empty<byte>() : _serializer.Serialize(arg);
            var push = new RpcPushEnvelope
            {
                ServiceId = serviceId,
                MethodId = methodId,
                Payload = payload
            };
            var bytes = RpcEnvelopeCodec.EncodePush(push);
            await SendFrameAsyncSerialized(bytes, ct).ConfigureAwait(false);
        }

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("RpcSession already started.");

            try
            {
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                UpdateSendActivityUtc();
                UpdateReceiveActivityUtc();
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

            await WaitForInFlightRequestsAsync().ConfigureAwait(false);
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
                    ReadOnlyMemory<byte> frame;
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

                    if (frame.Length == 0)
                        break;

                    UpdateReceiveActivityUtc();
                    ClearPendingPing();
                    var frameType = RpcEnvelopeCodec.PeekFrameType(frame.Span);
                    if (frameType == RpcFrameType.KeepAlivePing)
                    {
                        var ping = RpcEnvelopeCodec.DecodeKeepAlivePing(frame.Span);
                        var pongBytes = RpcEnvelopeCodec.EncodeKeepAlivePong(new RpcKeepAlivePongEnvelope
                        {
                            TimestampTicksUtc = ping.TimestampTicksUtc
                        });
                        await SendFrameAsyncSerialized(pongBytes, ct).ConfigureAwait(false);
                        continue;
                    }

                    if (frameType != RpcFrameType.Request)
                        continue;

                    var req = RpcEnvelopeCodec.DecodeRequest(frame.Span);
                    EnqueueRequestProcessing(req, ct);
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
                await WaitForInFlightRequestsAsync().ConfigureAwait(false);
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
                var pendingPingSentAt = Volatile.Read(ref _pendingPingSentAtTicksUtc);
                if (pendingPingSentAt > 0)
                {
                    if (new TimeSpan(nowTicks - pendingPingSentAt) < timeout)
                        continue;

                    SetDisconnectReason(new TimeoutException("RPC session keepalive timed out."));
                    try
                    {
                        serverCts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    return;
                }

                // Outbound sends can succeed against local buffers even after the peer is gone.
                // Only inbound traffic suppresses probes so keepalive remains a peer-liveness check.
                var lastReceive = Volatile.Read(ref _lastReceiveTicksUtc);
                if (new TimeSpan(nowTicks - lastReceive) < interval)
                    continue;

                try
                {
                    var pingTimestamp = DateTimeOffset.UtcNow.UtcTicks;
                    var pingBytes = RpcEnvelopeCodec.EncodeKeepAlivePing(new RpcKeepAlivePingEnvelope
                    {
                        TimestampTicksUtc = pingTimestamp
                    });
                    await SendFrameAsyncSerialized(pingBytes, ct).ConfigureAwait(false);
                    Volatile.Write(ref _pendingPingSentAtTicksUtc, pingTimestamp);
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

        private void EnqueueRequestProcessing(RpcRequestEnvelope req, CancellationToken ct)
        {
            var inflightId = Interlocked.Increment(ref _nextInFlightRequestId);
            var task = ProcessRequestAsync(req, ct);
            _inflightRequests[inflightId] = task;

            _ = task.ContinueWith(
                _ =>
                {
                    _inflightRequests.TryRemove(inflightId, out Task? _);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task ProcessRequestAsync(RpcRequestEnvelope req, CancellationToken ct)
        {
            RpcResponseEnvelope resp;
            if (_handlers.TryGetValue((req.ServiceId, req.MethodId), out var handler))
            {
                try
                {
                    resp = await handler(req, ct).ConfigureAwait(false);
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
                    resp = new RpcResponseEnvelope
                    {
                        RequestId = req.RequestId,
                        Status = RpcStatus.Exception,
                        Payload = Array.Empty<byte>(),
                        ErrorMessage = ex.ToString()
                    };
                }
            }
            else if (_registry is not null && _registry.TryGetHandler(req.ServiceId, req.MethodId, out var sessionHandler))
            {
                try
                {
                    resp = await sessionHandler(this, req, ct).ConfigureAwait(false);
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
                    resp = new RpcResponseEnvelope
                    {
                        RequestId = req.RequestId,
                        Status = RpcStatus.Exception,
                        Payload = Array.Empty<byte>(),
                        ErrorMessage = ex.ToString()
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

            try
            {
                var respBytes = RpcEnvelopeCodec.EncodeResponse(resp);
                await SendFrameAsyncSerialized(respBytes, ct).ConfigureAwait(false);
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
                UpdateSendActivityUtc();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async ValueTask WaitForInFlightRequestsAsync()
        {
            while (true)
            {
                var tasks = _inflightRequests.Values.ToArray();
                if (tasks.Length == 0)
                    return;

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch
                {
                }

                if (_inflightRequests.IsEmpty)
                    return;
            }
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

            await WaitForInFlightRequestsAsync().ConfigureAwait(false);

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

        private void UpdateSendActivityUtc()
        {
            Volatile.Write(ref _lastSendTicksUtc, DateTimeOffset.UtcNow.UtcTicks);
        }

        private void UpdateReceiveActivityUtc()
        {
            Volatile.Write(ref _lastReceiveTicksUtc, DateTimeOffset.UtcNow.UtcTicks);
        }

        private void SetDisconnectReason(Exception ex)
        {
            if (Interlocked.CompareExchange(ref _disconnectReasonSet, 1, 0) == 0)
                _disconnectReason = ex;
        }

        private void ClearPendingPing()
        {
            Volatile.Write(ref _pendingPingSentAtTicksUtc, 0);
        }

        private static long GetTimestampOrNow(long utcTicks) =>
            utcTicks > 0 ? utcTicks : DateTimeOffset.UtcNow.UtcTicks;
    }
}
