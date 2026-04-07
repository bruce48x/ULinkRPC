using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace ULinkRPC.Client
{
    public delegate void RpcPushPayloadHandler(ReadOnlySpan<byte> payload);

    public sealed class RpcClientRuntime : IAsyncDisposable, IRpcClient
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<RpcResponseEnvelope>> _pending = new();
        private readonly ConcurrentDictionary<(int serviceId, int methodId), RpcPushPayloadHandler> _pushHandlers = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private readonly RpcKeepAliveOptions _keepAlive;
        private int _disposed;
        private int _nextId;
        private int _started;
        private int _keepAliveTimedOut;
        private long _lastReceiveTicksUtc;
        private long _lastRttTicks;
        private long _lastSendTicksUtc;
        private long _pendingPingSentAtTicksUtc;
        private long _disconnectReasonSet;

        private Task? _recvLoop;
        private Task? _keepAliveLoop;
        private Exception? _disconnectReason;

        public RpcClientRuntime(ITransport transport, IRpcSerializer serializer, RpcKeepAliveOptions? keepAlive = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _keepAlive = keepAlive ?? RpcKeepAliveOptions.Disabled;
            UpdateSendActivityUtc();
            UpdateReceiveActivityUtc();
        }

        public event Action<Exception?>? Disconnected;

        public DateTimeOffset LastSendAt => new(GetTimestampOrNow(_lastSendTicksUtc), TimeSpan.Zero);

        public DateTimeOffset LastReceiveAt => new(GetTimestampOrNow(_lastReceiveTicksUtc), TimeSpan.Zero);

        public TimeSpan? LastRtt
        {
            get
            {
                var ticks = Volatile.Read(ref _lastRttTicks);
                return ticks <= 0 ? null : TimeSpan.FromTicks(ticks);
            }
        }

        public bool TimedOutByKeepAlive => Volatile.Read(ref _keepAliveTimedOut) != 0;

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("RpcClient already started.");

            try
            {
                await _transport.ConnectAsync(ct);
                UpdateSendActivityUtc();
                UpdateReceiveActivityUtc();
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
            var tcs = new TaskCompletionSource<RpcResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;

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
                    if (_pending.TryRemove(id, out var p))
                        p.TrySetCanceled(ct);
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
                _pending.TryRemove(id, out _);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            FailAllPending(new ObjectDisposedException(nameof(RpcClientRuntime)));
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

                    UpdateReceiveActivityUtc();
                    ClearPendingPing();
                    var frameType = RpcEnvelopeCodec.PeekFrameType(frame.Span);
                    switch (frameType)
                    {
                        case RpcFrameType.Response:
                        {
                            var resp = RpcEnvelopeCodec.DecodeResponse(frame.Span);
                            if (_pending.TryRemove(resp.RequestId, out var tcs))
                                tcs.TrySetResult(resp);
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
                            UpdateRttFromPong(pong.TimestampTicksUtc);
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
                    FailAllPending(err);

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
                var pendingPingSentAt = Volatile.Read(ref _pendingPingSentAtTicksUtc);
                if (pendingPingSentAt > 0)
                {
                    if (new TimeSpan(nowTicks - pendingPingSentAt) >= timeout)
                    {
                        Volatile.Write(ref _keepAliveTimedOut, 1);
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

                    continue;
                }

                // Outbound traffic does not prove the server is alive.
                // Only inbound frames suppress probes, which keeps the semantics aligned with the server.
                var lastReceive = Volatile.Read(ref _lastReceiveTicksUtc);
                if (new TimeSpan(nowTicks - lastReceive) < interval)
                    continue;

                var pingTimestamp = DateTimeOffset.UtcNow.UtcTicks;
                var ping = RpcEnvelopeCodec.EncodeKeepAlivePing(new RpcKeepAlivePingEnvelope
                {
                    TimestampTicksUtc = pingTimestamp
                });

                try
                {
                    await SendFrameAsyncSerialized(ping, _cts.Token).ConfigureAwait(false);
                    Volatile.Write(ref _pendingPingSentAtTicksUtc, pingTimestamp);
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
                UpdateSendActivityUtc();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void FailAllPending(Exception ex)
        {
            foreach (var item in _pending)
                if (_pending.TryRemove(item.Key, out var tcs))
                    tcs.TrySetException(ex);
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

        private void UpdateSendActivityUtc()
        {
            UpdateSendActivityUtc(DateTimeOffset.UtcNow.UtcTicks);
        }

        private void UpdateSendActivityUtc(long utcTicks)
        {
            Volatile.Write(ref _lastSendTicksUtc, utcTicks);
        }

        private void UpdateReceiveActivityUtc()
        {
            Volatile.Write(ref _lastReceiveTicksUtc, DateTimeOffset.UtcNow.UtcTicks);
        }

        private void ClearPendingPing()
        {
            Volatile.Write(ref _pendingPingSentAtTicksUtc, 0);
        }

        private void UpdateRttFromPong(long pongTimestampTicksUtc)
        {
            if (!_keepAlive.MeasureRtt)
                return;

            if (pongTimestampTicksUtc <= 0)
                return;

            var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
            if (nowTicks <= pongTimestampTicksUtc)
                return;

            Volatile.Write(ref _lastRttTicks, nowTicks - pongTimestampTicksUtc);
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

        private static long GetTimestampOrNow(long utcTicks) =>
            utcTicks > 0 ? utcTicks : DateTimeOffset.UtcNow.UtcTicks;
    }
}
