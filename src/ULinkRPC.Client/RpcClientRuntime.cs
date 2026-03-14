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
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;
        private int _nextId;
        private int _started;

        private Task? _recvLoop;

        public RpcClientRuntime(ITransport transport, IRpcSerializer serializer)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public event Action<Exception?>? Disconnected;

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("RpcClient already started.");

            try
            {
                await _transport.ConnectAsync(ct);
                _recvLoop = Task.Run(ReceiveLoopAsync);
            }
            catch
            {
                Interlocked.Exchange(ref _started, 0);
                throw;
            }
        }

        public void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, Action<TArg> handler)
        {
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
                await _transport.SendFrameAsync(reqBytes, ct);

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
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            Interlocked.Exchange(ref _started, 0);
            if (_recvLoop is not null)
                try
                {
                    await _recvLoop.ConfigureAwait(false);
                }
                catch
                {
                }

            await _transport.DisposeAsync().ConfigureAwait(false);
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
                                handler(push.Payload.AsSpan());
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
                if (err is not null)
                    FailAllPending(err);

                Disconnected?.Invoke(err);
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
    }
}
