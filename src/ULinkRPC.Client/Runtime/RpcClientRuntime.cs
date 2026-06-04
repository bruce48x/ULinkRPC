using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace ULinkRPC.Client
{
    /// <summary>
    ///     Handles a serialized server-to-client push payload.
    /// </summary>
    /// <param name="payload">Serialized push payload.</param>
    public delegate void RpcPushPayloadHandler(ReadOnlySpan<byte> payload);

    /// <summary>
    ///     Default client runtime for ULinkRPC request/response calls and server push dispatch.
    /// </summary>
    /// <remarks>
    ///     The runtime owns background receive, push, and keepalive loops after <see cref="StartAsync"/>.
    ///     Push handlers run on the runtime push loop and are not marshalled to the Unity main thread.
    /// </remarks>
    public sealed class RpcClientRuntime : IAsyncDisposable, IRpcClient
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly RpcKeepAliveState _keepAliveState;
        private readonly SerializedFrameSender _sender;
        private readonly RpcPendingRequestCollection _pending = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(int serviceId, int methodId), RpcPushPayloadHandler> _pushHandlers = new();
        private readonly Channel<RpcPushFrame> _pushQueue = Channel.CreateUnbounded<RpcPushFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly RpcKeepAliveOptions _keepAlive;
        private int _disposed;
        private int _nextId;
        private int _started;
        private long _disconnectReasonSet;

        private Task? _recvLoop;
        private Task? _keepAliveLoop;
        private Task? _pushLoop;
        private Exception? _disconnectReason;

        /// <summary>
        ///     Creates a runtime from client options.
        /// </summary>
        /// <param name="options">Client options containing transport, serializer, keepalive, and security settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public RpcClientRuntime(RpcClientOptions options)
            : this(
                (options ?? throw new ArgumentNullException(nameof(options))).CreateConfiguredTransport(),
                options.Serializer,
                options.KeepAlive)
        {
        }

        /// <summary>
        ///     Creates a runtime from explicit transport and serializer instances.
        /// </summary>
        /// <param name="transport">Connected or connectable transport used by the runtime.</param>
        /// <param name="serializer">Serializer used for RPC payloads.</param>
        /// <param name="keepAlive">Optional keepalive configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="transport"/> or <paramref name="serializer"/> is null.</exception>
        public RpcClientRuntime(ITransport transport, IRpcSerializer serializer, RpcKeepAliveOptions? keepAlive = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _keepAlive = keepAlive ?? RpcKeepAliveOptions.Disabled;
            _keepAliveState = new RpcKeepAliveState(_keepAlive.MeasureRtt);
            _sender = new SerializedFrameSender(_transport, _keepAliveState);
        }

        /// <summary>
        ///     Raised when the receive loop ends.
        /// </summary>
        /// <remarks>
        ///     The event argument is the disconnect reason when one is available. A null value means a normal or
        ///     locally requested shutdown.
        /// </remarks>
        public event Action<Exception?>? Disconnected;

        /// <summary>
        ///     Last UTC timestamp at which the runtime sent a frame.
        /// </summary>
        public DateTimeOffset LastSendAt => _keepAliveState.LastSendAt;

        /// <summary>
        ///     Last UTC timestamp at which the runtime received a frame.
        /// </summary>
        public DateTimeOffset LastReceiveAt => _keepAliveState.LastReceiveAt;

        /// <summary>
        ///     Last measured keepalive round-trip time, when RTT measurement is enabled.
        /// </summary>
        public TimeSpan? LastRtt => _keepAliveState.LastRtt;

        /// <summary>
        ///     Indicates whether the runtime stopped because keepalive timed out.
        /// </summary>
        public bool TimedOutByKeepAlive => _keepAliveState.TimedOut;

        /// <summary>
        ///     Connects the transport and starts background runtime loops.
        /// </summary>
        /// <param name="ct">Cancellation token for the initial transport connection.</param>
        /// <exception cref="InvalidOperationException">Thrown when the runtime has already been started.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the runtime has been disposed.</exception>
        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("RpcClient already started.");

            try
            {
                await _transport.ConnectAsync(ct);
                _keepAliveState.MarkSent();
                _keepAliveState.MarkReceived();
                _pushLoop = Task.Run(ProcessPushLoopAsync);
                _recvLoop = Task.Run(ReceiveLoopAsync);
                if (_keepAlive.Enabled)
                    _keepAliveLoop = Task.Run(KeepAliveLoopAsync);
            }
            catch
            {
                Interlocked.Exchange(ref _started, 0);
                throw;
            }
        }

        /// <inheritdoc />
        public void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, Action<TArg> handler)
        {
            ThrowIfDisposed();
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            _pushHandlers[(method.ServiceId, method.MethodId)] = payload =>
            {
                if (typeof(TArg) == typeof(RpcVoid))
                {
                    handler((TArg)(object)RpcVoid.Instance);
                    return;
                }

                var value = _serializer.Deserialize<TArg>(payload);
                handler(value);
            };
        }

        /// <inheritdoc />
        public async ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg? arg,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var reservation = _pending.Reserve(ref _nextId);
            var id = reservation.RequestId;
            var tcs = reservation.CompletionSource;

            try
            {
                using var argFrame = arg is null ? TransportFrame.Empty : _serializer.SerializeFrame(arg);
                var req = new RpcRequestEnvelope
                {
                    RequestId = id,
                    ServiceId = method.ServiceId,
                    MethodId = method.MethodId,
                    Payload = argFrame.Memory
                };

                using var reqBytes = RpcEnvelopeCodec.EncodeRequest(req);
                await SendFrameAsyncSerialized(reqBytes.Memory, ct).ConfigureAwait(false);

                using var reg = ct.Register(() =>
                {
                    _pending.TryCancel(id, ct);
                });

                using var resp = await tcs.Task.ConfigureAwait(false);
                if (resp.Status != RpcStatus.Ok)
                    throw new RpcException(resp.Status, resp.ErrorMessage, id, method.ServiceId, method.MethodId);

                if (typeof(TResult) == typeof(RpcVoid))
                    return (TResult)(object)RpcVoid.Instance;

                return _serializer.Deserialize<TResult>(resp.Payload.Memory)!;
            }
            finally
            {
                _pending.Remove(id);
            }
        }

        /// <summary>
        ///     Stops background loops, fails pending requests, and disposes the transport.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            _pending.FailAll(new ObjectDisposedException(nameof(RpcClientRuntime)));
            Interlocked.Exchange(ref _started, 0);
            if (_recvLoop is not null)
                try
                {
                    await _recvLoop.ConfigureAwait(false);
                }
                catch
                {
                }

            if (_keepAliveLoop is not null)
                try
                {
                    await _keepAliveLoop.ConfigureAwait(false);
                }
                catch
                {
                }

            _pushQueue.Writer.TryComplete();
            if (_pushLoop is not null)
                try
                {
                    await _pushLoop.ConfigureAwait(false);
                }
                catch
                {
                }

            await _transport.DisposeAsync().ConfigureAwait(false);
            _sender.Dispose();
            try { _cts.Dispose(); } catch (ObjectDisposedException) { }
        }

        private async Task ReceiveLoopAsync()
        {
            var ct = _cts.Token;
            Exception? err = null;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    using var frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
                    if (frame.IsEmpty)
                        throw new InvalidOperationException("Transport closed.");

                    _keepAliveState.MarkReceived();
                    var frameType = RpcEnvelopeCodec.PeekFrameType(frame.Span);
                    switch (frameType)
                    {
                        case RpcFrameType.Response:
                        {
                            var resp = RpcEnvelopeCodec.DecodeResponse(frame);
                            _pending.TrySetResult(resp);
                            break;
                        }
                        case RpcFrameType.Push:
                        {
                            var push = RpcEnvelopeCodec.DecodePush(frame);
                            _pushQueue.Writer.TryWrite(push);
                            break;
                        }
                        case RpcFrameType.KeepAlivePing:
                        {
                            var ping = RpcEnvelopeCodec.DecodeKeepAlivePing(frame.Span);
                            using var pong = RpcEnvelopeCodec.EncodeKeepAlivePong(new RpcKeepAlivePongEnvelope
                            {
                                TimestampTicksUtc = ping.TimestampTicksUtc
                            });
                            await _sender.SendAsync(pong.Memory, ct).ConfigureAwait(false);
                            break;
                        }
                        case RpcFrameType.KeepAlivePong:
                        {
                            var pong = RpcEnvelopeCodec.DecodeKeepAlivePong(frame.Span);
                            _keepAliveState.RecordPong(pong.TimestampTicksUtc);
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    err = ex;
            }
            finally
            {
                if (err is null)
                    err = _disconnectReason;
                if (err is not null)
                    _pending.FailAll(err);

                _pushQueue.Writer.TryComplete();
                Disconnected?.Invoke(err);
            }
        }

        private async Task ProcessPushLoopAsync()
        {
            try
            {
                await foreach (var push in _pushQueue.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
                {
                    using (push)
                    {
                        if (!_pushHandlers.TryGetValue((push.ServiceId, push.MethodId), out var handler))
                            continue;

                        try
                        {
                            handler(push.Payload.Span);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ChannelClosedException)
            {
            }
        }

        private async Task KeepAliveLoopAsync()
        {
            var coordinator = new RpcKeepAliveCoordinator(
                _transport,
                _sender,
                _keepAliveState,
                _keepAlive,
                "RPC keepalive timed out.",
                ex =>
                {
                    SetDisconnectReason(ex);
                    try
                    {
                        _cts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                },
                markTimedOut: true);

            await coordinator.RunAsync(_cts.Token).ConfigureAwait(false);
        }

        private ValueTask SendFrameAsyncSerialized(ReadOnlyMemory<byte> frame, CancellationToken ct)
        {
            return _sender.SendAsync(frame, ct);
        }

        private void SetDisconnectReason(Exception ex)
        {
            if (Interlocked.CompareExchange(ref _disconnectReasonSet, 1, 0) == 0)
                _disconnectReason = ex;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(RpcClientRuntime));
        }

    }
}
