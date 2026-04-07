using System.Threading;
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace ULinkRPC.Client
{
    public delegate void RpcPushPayloadHandler(ReadOnlySpan<byte> payload);

    public sealed class RpcClientRuntime : IAsyncDisposable, IRpcClient
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly RpcKeepAliveState _keepAliveState;
        private readonly RpcPendingRequestCollection _pending = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(int serviceId, int methodId), RpcPushPayloadHandler> _pushHandlers = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly RpcKeepAliveOptions _keepAlive;
        private int _disposed;
        private int _nextId;
        private int _started;
        private long _disconnectReasonSet;

        private Task? _recvLoop;
        private Task? _keepAliveLoop;
        private Exception? _disconnectReason;

        public RpcClientRuntime(ITransport transport, IRpcSerializer serializer, RpcKeepAliveOptions? keepAlive = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _keepAlive = keepAlive ?? RpcKeepAliveOptions.Disabled;
            _keepAliveState = new RpcKeepAliveState(_keepAlive.MeasureRtt);
        }

        public event Action<Exception?>? Disconnected;

        public DateTimeOffset LastSendAt => _keepAliveState.LastSendAt;

        public DateTimeOffset LastReceiveAt => _keepAliveState.LastReceiveAt;

        public TimeSpan? LastRtt => _keepAliveState.LastRtt;

        public bool TimedOutByKeepAlive => _keepAliveState.TimedOut;

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

        public async ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg? arg,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            var id = NextRequestId();
            var tcs = _pending.Add(id);

            try
            {
                var argBytes = arg is null ? Array.Empty<byte>() : _serializer.Serialize(arg);

                var req = new RpcRequestEnvelope
                {
                    RequestId = id,
                    ServiceId = method.ServiceId,
                    MethodId = method.MethodId,
                    Payload = argBytes
                };

                var reqBytes = RpcEnvelopeCodec.EncodeRequest(req);
                await SendFrameAsyncSerialized(reqBytes, ct).ConfigureAwait(false);

                using var reg = ct.Register(() =>
                {
                    _pending.TryCancel(id, ct);
                });

                var resp = await tcs.Task.ConfigureAwait(false);
                if (resp.Status != RpcStatus.Ok)
                    throw new InvalidOperationException($"RPC failed: {resp.Status}, {resp.ErrorMessage}");

                if (typeof(TResult) == typeof(RpcVoid))
                    return (TResult)(object)RpcVoid.Instance;

                return _serializer.Deserialize<TResult>(resp.Payload.AsSpan())!;
            }
            finally
            {
                _pending.Remove(id);
            }
        }

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

            await _transport.DisposeAsync().ConfigureAwait(false);
            _sendLock.Dispose();
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
                    var frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
                    if (frame.IsEmpty)
                        throw new InvalidOperationException("Transport closed.");

                    _keepAliveState.MarkReceived();
                    var frameType = RpcEnvelopeCodec.PeekFrameType(frame.Span);
                    switch (frameType)
                    {
                        case RpcFrameType.Response:
                        {
                            var resp = RpcEnvelopeCodec.DecodeResponse(frame.Span);
                            _pending.TrySetResult(resp);
                            break;
                        }
                        case RpcFrameType.Push:
                        {
                            var push = RpcEnvelopeCodec.DecodePush(frame.Span);
                            if (_pushHandlers.TryGetValue((push.ServiceId, push.MethodId), out var handler))
                            {
                                try
                                {
                                    handler(push.Payload.AsSpan());
                                }
                                catch
                                {
                                }
                            }
                            break;
                        }
                        case RpcFrameType.KeepAlivePing:
                        {
                            var ping = RpcEnvelopeCodec.DecodeKeepAlivePing(frame.Span);
                            var pong = RpcEnvelopeCodec.EncodeKeepAlivePong(new RpcKeepAlivePongEnvelope
                            {
                                TimestampTicksUtc = ping.TimestampTicksUtc
                            });
                            await SendFrameAsyncSerialized(pong, ct).ConfigureAwait(false);
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

                Disconnected?.Invoke(err);
            }
        }

        private async Task KeepAliveLoopAsync()
        {
            var interval = _keepAlive.Interval;
            var timeout = _keepAlive.Timeout;
            if (interval <= TimeSpan.Zero || timeout <= TimeSpan.Zero)
                return;

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, _cts.Token).ConfigureAwait(false);
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
                    {
                        _keepAliveState.MarkTimedOut();
                        SetDisconnectReason(new TimeoutException("RPC keepalive timed out."));
                        try
                        {
                            _cts.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        return;
                    }
                    case RpcKeepAliveAction.SendPing:
                        break;
                }

                var pingTimestamp = DateTimeOffset.UtcNow.UtcTicks;
                var ping = RpcEnvelopeCodec.EncodeKeepAlivePing(new RpcKeepAlivePingEnvelope
                {
                    TimestampTicksUtc = pingTimestamp
                });

                try
                {
                    await SendFrameAsyncSerialized(ping, _cts.Token).ConfigureAwait(false);
                    _keepAliveState.MarkPingSent(pingTimestamp);
                }
                catch (OperationCanceledException)
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

        private uint NextRequestId()
        {
            uint id;
            do
            {
                id = unchecked((uint)Interlocked.Increment(ref _nextId));
            } while (id == 0);

            return id;
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
