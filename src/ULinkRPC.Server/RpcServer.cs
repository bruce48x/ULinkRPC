using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using ULinkRPC.Core;

namespace ULinkRPC.Server
{
    public delegate ValueTask<RpcResponseEnvelope> RpcHandler(RpcRequestEnvelope req, CancellationToken ct);

    public sealed class RpcServer : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<(int serviceId, int methodId), RpcHandler> _handlers = new();
        private readonly ConcurrentDictionary<int, Task> _inflightRequests = new();
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly bool _ownsTransport;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private Task? _loop;
        private int _disposed;
        private int _nextInFlightRequestId;
        private int _started;
        private int _transportDisposed;

        public RpcServer(ITransport transport, IRpcSerializer serializer)
            : this(transport, serializer, Guid.NewGuid().ToString("N"), false)
        {
        }

        public RpcServer(ITransport transport, IRpcSerializer serializer, bool ownsTransport)
            : this(transport, serializer, Guid.NewGuid().ToString("N"), ownsTransport)
        {
        }

        public RpcServer(ITransport transport, IRpcSerializer serializer, string contextId)
            : this(transport, serializer, contextId, false)
        {
        }

        public RpcServer(ITransport transport, IRpcSerializer serializer, string contextId, bool ownsTransport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _ownsTransport = ownsTransport;
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

        public event Action<Exception?>? Disconnected;

        public void Register(int serviceId, int methodId, RpcHandler handler)
        {
            ThrowIfDisposed();
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            _handlers[(serviceId, methodId)] = handler;
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
                throw new InvalidOperationException("RpcServer already started.");

            try
            {
                await _transport.ConnectAsync(ct).ConfigureAwait(false);
                RemoteEndPoint ??= ResolveRemoteEndPoint(_transport);
                _cts = new CancellationTokenSource();
                var serverCts = _cts;
                _loop = Task.Run(() => LoopAsync(serverCts));
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

                    var frameType = RpcEnvelopeCodec.PeekFrameType(frame.Span);
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
                await WaitForInFlightRequestsAsync().ConfigureAwait(false);
                ResetRuntimeState(serverCts);
                Disconnected?.Invoke(disconnectError);
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
            Interlocked.Exchange(ref _started, 0);
        }

        public async ValueTask StopAsync()
        {
            var cts = _cts;
            var loop = _loop;

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
            Interlocked.Exchange(ref _started, 0);
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
                throw new ObjectDisposedException(nameof(RpcServer));
        }

        private static IPEndPoint? ResolveRemoteEndPoint(ITransport transport)
        {
            return (transport as IRemoteEndPointProvider)?.RemoteEndPoint as IPEndPoint;
        }
    }
}
