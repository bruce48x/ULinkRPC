using System.Collections.Concurrent;
using ULinkRPC.Core;

namespace ULinkRPC.Server
{
    public delegate ValueTask<RpcResponseEnvelope> RpcHandler(RpcRequestEnvelope req, CancellationToken ct);

    public sealed class RpcServer
    {
        private readonly ConcurrentDictionary<(int serviceId, int methodId), RpcHandler> _handlers = new();
        private readonly ITransport _transport;
        private readonly IRpcSerializer _serializer;

        private CancellationTokenSource? _cts;
        private Task? _loop;
        private int _started;

        public RpcServer(ITransport transport, IRpcSerializer serializer)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public IRpcSerializer Serializer => _serializer;

        public void Register(int serviceId, int methodId, RpcHandler handler)
        {
            _handlers[(serviceId, methodId)] = handler;
        }

        public async ValueTask PushAsync<TArg>(int serviceId, int methodId, TArg arg, CancellationToken ct = default)
        {
            var payload = arg is null ? Array.Empty<byte>() : _serializer.Serialize(arg);
            var push = new RpcPushEnvelope
            {
                ServiceId = serviceId,
                MethodId = methodId,
                Payload = payload
            };
            var bytes = RpcEnvelopeCodec.EncodePush(push);
            await _transport.SendFrameAsync(bytes, ct).ConfigureAwait(false);
        }

        public async ValueTask StartAsync(CancellationToken ct = default)
        {
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("RpcServer already started.");

            try
            {
                await _transport.ConnectAsync(ct);
                _cts = new CancellationTokenSource();
                _loop = Task.Run(LoopAsync);
            }
            catch
            {
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
        }

        private async Task LoopAsync()
        {
            if (_cts is null) return;
            var ct = _cts.Token;

            while (!ct.IsCancellationRequested)
            {
                ReadOnlyMemory<byte> frame;
                try
                {
                    frame = await _transport.ReceiveFrameAsync(ct).ConfigureAwait(false);
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

                RpcResponseEnvelope resp;
                if (_handlers.TryGetValue((req.ServiceId, req.MethodId), out var handler))
                    try
                    {
                        resp = await handler(req, ct).ConfigureAwait(false);
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
                else
                    resp = new RpcResponseEnvelope
                    {
                        RequestId = req.RequestId,
                        Status = RpcStatus.NotFound,
                        Payload = Array.Empty<byte>(),
                        ErrorMessage = $"No handler for {req.ServiceId}:{req.MethodId}"
                    };

                var respBytes = RpcEnvelopeCodec.EncodeResponse(resp);
                await _transport.SendFrameAsync(respBytes, ct).ConfigureAwait(false);
            }
        }

        public async ValueTask StopAsync()
        {
            if (_cts is null) return;
            _cts.Cancel();
            if (_loop is not null)
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

            _cts.Dispose();
            _cts = null;
            _loop = null;
            Interlocked.Exchange(ref _started, 0);
        }
    }
}
