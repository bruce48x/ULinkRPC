using System.Diagnostics;
using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.Loopback;

namespace ULinkRPC.Tests;

public class RpcSessionTests
{
    private static (ITransport clientTransport, RpcSession server) CreateServerWithHandler(
        int serviceId, int methodId, RpcHandler handler)
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);
        server.Register(serviceId, methodId, handler);
        return (clientTransport, server);
    }

    [Fact]
    public async Task Register_HandlerReceivesRequest()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        RpcRequestEnvelope? receivedReq = null;
        server.Register(1, 1, (req, ct) =>
        {
            receivedReq = req;
            return ValueTask.FromResult(new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = Array.Empty<byte>()
            });
        });

        await server.StartAsync();
        await clientTransport.ConnectAsync();

        var reqEnv = new RpcRequestEnvelope
        {
            RequestId = 42,
            ServiceId = 1,
            MethodId = 1,
            Payload = serializer.Serialize("hello")
        };
        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(reqEnv));

        var respFrame = await clientTransport.ReceiveFrameAsync();
        var resp = RpcEnvelopeCodec.DecodeResponse(respFrame.Span);

        Assert.Equal(RpcStatus.Ok, resp.Status);
        Assert.NotNull(receivedReq);
        Assert.Equal(42u, receivedReq!.RequestId);

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task UnregisteredHandler_ReturnsNotFound()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        await server.StartAsync();
        await clientTransport.ConnectAsync();

        var reqEnv = new RpcRequestEnvelope
        {
            RequestId = 1,
            ServiceId = 999,
            MethodId = 999,
            Payload = Array.Empty<byte>()
        };
        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(reqEnv));

        var respFrame = await clientTransport.ReceiveFrameAsync();
        var resp = RpcEnvelopeCodec.DecodeResponse(respFrame.Span);

        Assert.Equal(RpcStatus.NotFound, resp.Status);
        Assert.Contains("No handler", resp.ErrorMessage);

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task HandlerThrows_ReturnsException()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        server.Register(1, 1, (req, ct) => throw new InvalidOperationException("test error"));

        await server.StartAsync();
        await clientTransport.ConnectAsync();

        var reqEnv = new RpcRequestEnvelope
        {
            RequestId = 1,
            ServiceId = 1,
            MethodId = 1,
            Payload = Array.Empty<byte>()
        };
        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(reqEnv));

        var respFrame = await clientTransport.ReceiveFrameAsync();
        var resp = RpcEnvelopeCodec.DecodeResponse(respFrame.Span);

        Assert.Equal(RpcStatus.Exception, resp.Status);
        Assert.Contains("test error", resp.ErrorMessage);

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_CalledTwice_Throws()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        await server.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => server.StartAsync().AsTask());

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_ThenStartAgain_Succeeds()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        await server.StartAsync();
        await server.StopAsync();

        // After stop, _started is reset to 0, so creating a new transport to re-start
        LoopbackTransport.CreatePair(out var clientTransport2, out var serverTransport2);
        var server2 = new RpcSession(serverTransport2, serializer);
        await server2.StartAsync();
        await server2.StopAsync();

        await clientTransport.DisposeAsync();
        await clientTransport2.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentRegisterAndServe_HandlerDictionaryIsThreadSafe()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        server.Register(1, 1, (req, ct) => ValueTask.FromResult(new RpcResponseEnvelope
        {
            RequestId = req.RequestId,
            Status = RpcStatus.Ok,
            Payload = Array.Empty<byte>()
        }));

        await server.StartAsync();
        await clientTransport.ConnectAsync();

        var registerTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                server.Register(100 + i, 1, (req, ct) => ValueTask.FromResult(new RpcResponseEnvelope
                {
                    RequestId = req.RequestId,
                    Status = RpcStatus.Ok,
                    Payload = Array.Empty<byte>()
                }));
            }
        });

        var callTask = Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                var reqEnv = new RpcRequestEnvelope
                {
                    RequestId = (uint)(i + 1),
                    ServiceId = 1,
                    MethodId = 1,
                    Payload = Array.Empty<byte>()
                };
                await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(reqEnv));
                var respFrame = await clientTransport.ReceiveFrameAsync();
                var resp = RpcEnvelopeCodec.DecodeResponse(respFrame.Span);
                Assert.Equal(RpcStatus.Ok, resp.Status);
            }
        });

        await Task.WhenAll(registerTask, callTask);

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task SharedRegistry_CanServeMultipleConnections()
    {
        var serializer = new JsonRpcSerializer();
        var registry = new RpcServiceRegistry();
        var handledConnections = 0;

        registry.Register(1, 1, (server, req, ct) =>
        {
            Interlocked.Increment(ref handledConnections);
            return ValueTask.FromResult(new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = server.Serializer.Serialize(server.ContextId)
            });
        });

        LoopbackTransport.CreatePair(out var clientTransport1, out var serverTransport1);
        LoopbackTransport.CreatePair(out var clientTransport2, out var serverTransport2);
        var server1 = new RpcSession(serverTransport1, serializer, registry);
        var server2 = new RpcSession(serverTransport2, serializer, registry);

        await server1.StartAsync();
        await server2.StartAsync();
        await clientTransport1.ConnectAsync();
        await clientTransport2.ConnectAsync();

        await clientTransport1.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(new RpcRequestEnvelope
        {
            RequestId = 1,
            ServiceId = 1,
            MethodId = 1,
            Payload = Array.Empty<byte>()
        }));
        await clientTransport2.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(new RpcRequestEnvelope
        {
            RequestId = 2,
            ServiceId = 1,
            MethodId = 1,
            Payload = Array.Empty<byte>()
        }));

        var resp1 = RpcEnvelopeCodec.DecodeResponse((await clientTransport1.ReceiveFrameAsync()).Span);
        var resp2 = RpcEnvelopeCodec.DecodeResponse((await clientTransport2.ReceiveFrameAsync()).Span);

        Assert.Equal(RpcStatus.Ok, resp1.Status);
        Assert.Equal(RpcStatus.Ok, resp2.Status);
        Assert.NotEqual(serializer.Deserialize<string>(resp1.Payload.AsSpan()), serializer.Deserialize<string>(resp2.Payload.AsSpan()));
        Assert.Equal(2, handledConnections);

        await server1.StopAsync();
        await server2.StopAsync();
        await clientTransport1.DisposeAsync();
        await clientTransport2.DisposeAsync();
    }

    [Fact]
    public async Task ServerLifetime_IndependentOfStartCancellationToken()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        server.Register(1, 1, (req, ct) => ValueTask.FromResult(new RpcResponseEnvelope
        {
            RequestId = req.RequestId,
            Status = RpcStatus.Ok,
            Payload = Array.Empty<byte>()
        }));

        using var startCts = new CancellationTokenSource();
        await server.StartAsync(startCts.Token);
        await clientTransport.ConnectAsync();

        startCts.Cancel();

        await Task.Delay(100);

        var reqEnv = new RpcRequestEnvelope
        {
            RequestId = 1,
            ServiceId = 1,
            MethodId = 1,
            Payload = Array.Empty<byte>()
        };
        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(reqEnv));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var respFrame = await clientTransport.ReceiveFrameAsync(readCts.Token);
        var resp = RpcEnvelopeCodec.DecodeResponse(respFrame.Span);

        Assert.Equal(RpcStatus.Ok, resp.Status);

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task ScopedServiceCache_IsPerConnectionAndClearedOnStop()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var server = new RpcSession(serverTransport, new JsonRpcSerializer());

        var first = server.GetOrAddScopedService(1, _ => new object());
        var second = server.GetOrAddScopedService(1, _ => new object());
        Assert.Same(first, second);

        await server.StopAsync();

        var third = server.GetOrAddScopedService(1, _ => new object());
        Assert.NotSame(first, third);

        await clientTransport.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public void Constructor_NullTransport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RpcSession(null!, new JsonRpcSerializer()));
    }

    [Fact]
    public void Constructor_NullSerializer_Throws()
    {
        LoopbackTransport.CreatePair(out var client, out var server);
        Assert.Throws<ArgumentNullException>(() => new RpcSession(client, null!));
    }

    [Fact]
    public async Task ConcurrentRequests_FastHandlerNotBlockedBySlowHandler()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        server.Register(1, 1, async (req, ct) =>
        {
            await Task.Delay(300, ct);
            return new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = Array.Empty<byte>()
            };
        });

        server.Register(1, 2, (req, ct) =>
            ValueTask.FromResult(new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = Array.Empty<byte>()
            }));

        await server.StartAsync();
        await clientTransport.ConnectAsync();

        var slowReq = new RpcRequestEnvelope { RequestId = 1, ServiceId = 1, MethodId = 1, Payload = Array.Empty<byte>() };
        var fastReq = new RpcRequestEnvelope { RequestId = 2, ServiceId = 1, MethodId = 2, Payload = Array.Empty<byte>() };

        var sw = Stopwatch.StartNew();
        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(slowReq));
        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeRequest(fastReq));

        var firstResp = RpcEnvelopeCodec.DecodeResponse((await clientTransport.ReceiveFrameAsync()).Span);
        var fastResponseElapsed = sw.ElapsedMilliseconds;
        var secondResp = RpcEnvelopeCodec.DecodeResponse((await clientTransport.ReceiveFrameAsync()).Span);

        Assert.Equal(2u, firstResp.RequestId);
        Assert.Equal(1u, secondResp.RequestId);
        Assert.True(fastResponseElapsed < 250, $"Fast response was delayed: {fastResponseElapsed}ms");

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task PushAsync_ConcurrentCalls_AreSerializedOnTransport()
    {
        var transport = new ConcurrentSendDetectTransport();
        var server = new RpcSession(transport, new JsonRpcSerializer());

        var sends = Enumerable.Range(0, 24)
            .Select(i => server.PushAsync(1, 1, i).AsTask())
            .ToArray();

        await Task.WhenAll(sends);

        Assert.Equal(1, transport.MaxConcurrentSends);
        await server.DisposeAsync();
    }

    [Fact]
    public async Task RemoteClose_ResetsServerStateAndRaisesDisconnected()
    {
        var transport = new ReconnectableEmptyFrameTransport();
        var server = new RpcSession(transport, new JsonRpcSerializer());

        var disconnectedCount = 0;
        server.Disconnected += ex =>
        {
            Assert.Null(ex);
            Interlocked.Increment(ref disconnectedCount);
        };

        await server.StartAsync();
        await server.WaitForCompletionAsync();

        await server.StartAsync();
        await server.WaitForCompletionAsync();

        Assert.Equal(2, transport.ConnectCount);
        Assert.Equal(2, disconnectedCount);

        await server.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenOwningTransport_DisposesTransportOnce()
    {
        var transport = new ConcurrentSendDetectTransport();
        var server = new RpcSession(transport, new JsonRpcSerializer(), ownsTransport: true);

        await server.DisposeAsync();
        await server.DisposeAsync();

        Assert.Equal(1, transport.DisposeCount);
    }

    [Fact]
    public async Task KeepAlivePing_ReceivesPong()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcSession(serverTransport, serializer);

        await server.StartAsync();
        await clientTransport.ConnectAsync();

        var ping = new RpcKeepAlivePingEnvelope
        {
            TimestampTicksUtc = DateTimeOffset.UtcNow.UtcTicks
        };

        await clientTransport.SendFrameAsync(RpcEnvelopeCodec.EncodeKeepAlivePing(ping));
        var frame = await clientTransport.ReceiveFrameAsync();
        var pong = RpcEnvelopeCodec.DecodeKeepAlivePong(frame.Span);

        Assert.Equal(ping.TimestampTicksUtc, pong.TimestampTicksUtc);

        await server.StopAsync();
        await clientTransport.DisposeAsync();
    }

    [Fact]
    public async Task KeepAliveTimeout_DisconnectsIdleSession()
    {
        var transport = new IdleSessionTransport();
        var server = new RpcSession(
            transport,
            new JsonRpcSerializer(),
            registry: null,
            contextId: "keepalive-test",
            ownsTransport: false,
            keepAlive: new RpcKeepAliveOptions
            {
                Enabled = true,
                Interval = TimeSpan.FromMilliseconds(40),
                Timeout = TimeSpan.FromMilliseconds(120),
                MeasureRtt = false
            });

        var disconnectedTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.Disconnected += ex => disconnectedTcs.TrySetResult(ex);

        await server.StartAsync();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var registration = timeoutCts.Token.Register(() => disconnectedTcs.TrySetCanceled());
        var error = await disconnectedTcs.Task;

        var timeout = Assert.IsType<TimeoutException>(error);
        Assert.Contains("keepalive", timeout.Message, StringComparison.OrdinalIgnoreCase);

        await server.DisposeAsync();
    }

    private sealed class ConcurrentSendDetectTransport : ITransport
    {
        private int _currentSends;
        private int _disposeCount;
        private int _maxConcurrentSends;

        public int MaxConcurrentSends => Volatile.Read(ref _maxConcurrentSends);
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public async ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            var current = Interlocked.Increment(ref _currentSends);
            UpdateMaxConcurrentSends(current);

            await Task.Delay(5, ct);
            Interlocked.Decrement(ref _currentSends);
        }

        public ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }

        private void UpdateMaxConcurrentSends(int value)
        {
            while (true)
            {
                var snapshot = MaxConcurrentSends;
                if (value <= snapshot)
                    return;

                if (Interlocked.CompareExchange(ref _maxConcurrentSends, value, snapshot) == snapshot)
                    return;
            }
        }
    }

    private sealed class ReconnectableEmptyFrameTransport : ITransport
    {
        public int ConnectCount { get; private set; }
        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            ConnectCount++;
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class IdleSessionTransport : ITransport
    {
        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
