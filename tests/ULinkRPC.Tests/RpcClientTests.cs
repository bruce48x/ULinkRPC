using ULinkRPC.Client;
using ULinkRPC.Core;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Server;
using ULinkRPC.Transport.Loopback;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace ULinkRPC.Tests;

public class RpcClientRuntimeTests
{
    private static readonly RpcMethod<string, string> EchoMethod = new(1, 1);
    private static readonly RpcMethod<string, RpcVoid> VoidMethod = new(1, 1);
    private static readonly RpcMethod<int, int> DoubleMethod = new(1, 1);
    private static readonly RpcPushMethod<string> NotifyPushMethod = new(1, 1);

    [Fact]
    public async Task CallAsync_ReturnsResult()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        var server = new RpcSession(serverTransport, serializer);
        server.Register(1, 1, (req, ct) =>
        {
            var arg = serializer.Deserialize<string>(req.Payload.AsSpan());
            var result = serializer.Serialize($"Hello {arg}");
            return ValueTask.FromResult(new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = result
            });
        });

        await server.StartAsync();

        var client = new RpcClientRuntime(clientTransport, serializer);
        await client.StartAsync();

        var response = await client.CallAsync(EchoMethod, "World");
        Assert.Equal("Hello World", response);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task CallAsync_VoidReturn()
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

        var client = new RpcClientRuntime(clientTransport, serializer);
        await client.StartAsync();

        var result = await client.CallAsync(VoidMethod, "test");
        Assert.Same(RpcVoid.Instance, result);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task CallAsync_ServerError_ThrowsOnClient()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        var server = new RpcSession(serverTransport, serializer);
        server.Register(1, 1, (req, ct) =>
            throw new InvalidOperationException("handler exploded"));

        await server.StartAsync();

        var client = new RpcClientRuntime(clientTransport, serializer);
        await client.StartAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CallAsync(EchoMethod, "test").AsTask());
        Assert.Contains("Exception", ex.Message);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task CallAsync_Cancellation_PropagatedCorrectly()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        // Don't start a server so the call will hang
        var client = new RpcClientRuntime(clientTransport, serializer);
        await client.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.CallAsync(EchoMethod, "test", cts.Token).AsTask());

        await client.DisposeAsync();
        await serverTransport.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_CalledTwice_Throws()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(clientTransport, serializer);

        await client.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StartAsync().AsTask());

        await client.DisposeAsync();
        await serverTransport.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(clientTransport, serializer);

        await client.StartAsync();

        await client.DisposeAsync();
        await client.DisposeAsync();

        await serverTransport.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_FailsPendingCalls()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(clientTransport, serializer);

        await client.StartAsync();

        var callTask = client.CallAsync(EchoMethod, "pending").AsTask();
        await Task.Delay(50);
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => callTask);
        await serverTransport.DisposeAsync();
    }

    [Fact]
    public async Task Disconnected_EventFired_OnTransportClose()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(clientTransport, serializer);

        var disconnectedTcs = new TaskCompletionSource<Exception?>();
        client.Disconnected += ex => disconnectedTcs.TrySetResult(ex);

        await client.StartAsync();

        await serverTransport.DisposeAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        cts.Token.Register(() => disconnectedTcs.TrySetCanceled());

        var disconnectError = await disconnectedTcs.Task;
        // Event was fired (could be null for graceful or non-null for error)

        await client.DisposeAsync();
    }

    [Fact]
    public async Task PushHandler_ReceivesPushFromServer()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        var server = new RpcSession(serverTransport, serializer);
        await server.StartAsync();

        var client = new RpcClientRuntime(clientTransport, serializer);

        string? receivedMessage = null;
        var pushReceived = new TaskCompletionSource<bool>();
        client.RegisterPushHandler(NotifyPushMethod, (payload) =>
        {
            receivedMessage = payload;
            pushReceived.TrySetResult(true);
        });

        await client.StartAsync();

        await server.PushAsync(1, 1, "hello from server");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        cts.Token.Register(() => pushReceived.TrySetCanceled());
        await pushReceived.Task;

        Assert.Equal("hello from server", receivedMessage);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task PushHandler_Exception_DoesNotDisconnectClient()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        var server = new RpcSession(serverTransport, serializer);
        server.Register(1, 1, (req, ct) =>
        {
            var arg = serializer.Deserialize<string>(req.Payload.AsSpan());
            return ValueTask.FromResult(new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = serializer.Serialize(arg.ToUpperInvariant())
            });
        });

        await server.StartAsync();

        var client = new RpcClientRuntime(clientTransport, serializer);
        client.RegisterPushHandler(NotifyPushMethod, _ => throw new InvalidOperationException("push exploded"));

        await client.StartAsync();
        await server.PushAsync(1, 1, "boom");
        await Task.Delay(50);

        var response = await client.CallAsync(EchoMethod, "still-alive");
        Assert.Equal("STILL-ALIVE", response);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public void RegisterPushHandler_NullHandler_Throws()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out _);
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(clientTransport, serializer);

        Assert.Throws<ArgumentNullException>(() =>
            client.RegisterPushHandler(NotifyPushMethod, null!));
    }

    [Fact]
    public void Constructor_NullTransport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RpcClientRuntime(null!, new JsonRpcSerializer()));
    }

    [Fact]
    public void Constructor_NullSerializer_Throws()
    {
        LoopbackTransport.CreatePair(out var client, out var server);
        Assert.Throws<ArgumentNullException>(() => new RpcClientRuntime(client, null!));
    }

    [Fact]
    public async Task MultipleConcurrentCalls_AllResolveCorrectly()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        var server = new RpcSession(serverTransport, serializer);
        server.Register(1, 1, async (req, ct) =>
        {
            var arg = serializer.Deserialize<int>(req.Payload.AsSpan());
            await Task.Delay(10, ct);
            return new RpcResponseEnvelope
            {
                RequestId = req.RequestId,
                Status = RpcStatus.Ok,
                Payload = serializer.Serialize(arg * 2)
            };
        });

        await server.StartAsync();

        var client = new RpcClientRuntime(clientTransport, serializer);
        await client.StartAsync();

        var tasks = Enumerable.Range(1, 20)
            .Select(i => client.CallAsync(DoubleMethod, i).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < 20; i++)
            Assert.Equal((i + 1) * 2, results[i]);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task CallAsync_RequestIdCollision_ReusesNextAvailableId()
    {
        var serializer = new JsonRpcSerializer();
        var transport = new ControlledClientTransport();
        var client = new RpcClientRuntime(transport, serializer);
        await client.StartAsync();

        SetNextRequestId(client, 0);
        var firstCall = client.CallAsync(EchoMethod, "first").AsTask();
        await transport.WaitForSentRequestCountAsync(1);

        SetNextRequestId(client, 0);
        var secondCall = client.CallAsync(EchoMethod, "second").AsTask();
        await transport.WaitForSentRequestCountAsync(2);

        Assert.Equal(new uint[] { 1, 2 }, transport.SentRequestIds.ToArray());

        transport.Complete(1, serializer.Serialize("ok-1"));
        transport.Complete(2, serializer.Serialize("ok-2"));

        Assert.Equal("ok-1", await firstCall);
        Assert.Equal("ok-2", await secondCall);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task CallAsync_RequestIdWraparound_SkipsZeroAndCollisions()
    {
        var serializer = new JsonRpcSerializer();
        var transport = new ControlledClientTransport();
        var client = new RpcClientRuntime(transport, serializer);
        await client.StartAsync();

        SetNextRequestId(client, -2);
        var firstCall = client.CallAsync(EchoMethod, "first").AsTask();
        await transport.WaitForSentRequestCountAsync(1);

        SetNextRequestId(client, -2);
        var secondCall = client.CallAsync(EchoMethod, "second").AsTask();
        await transport.WaitForSentRequestCountAsync(2);

        Assert.Equal(new uint[] { uint.MaxValue, 1 }, transport.SentRequestIds.ToArray());

        transport.Complete(uint.MaxValue, serializer.Serialize("wrapped-max"));
        transport.Complete(1, serializer.Serialize("wrapped-one"));

        Assert.Equal("wrapped-max", await firstCall);
        Assert.Equal("wrapped-one", await secondCall);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task KeepAlive_WithServerPong_UpdatesLastRtt()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();

        var server = new RpcSession(serverTransport, serializer);
        await server.StartAsync();

        var client = new RpcClientRuntime(clientTransport, serializer, new RpcKeepAliveOptions
        {
            Enabled = true,
            Interval = TimeSpan.FromMilliseconds(50),
            Timeout = TimeSpan.FromMilliseconds(250),
            MeasureRtt = true
        });

        await client.StartAsync();

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (client.LastRtt is null && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.NotNull(client.LastRtt);
        Assert.False(client.TimedOutByKeepAlive);

        await client.DisposeAsync();
        await server.StopAsync();
    }

    [Fact]
    public async Task KeepAlive_TimeoutDisconnectsClient()
    {
        var transport = new IdleClientTransport();
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(transport, serializer, new RpcKeepAliveOptions
        {
            Enabled = true,
            Interval = TimeSpan.FromMilliseconds(40),
            Timeout = TimeSpan.FromMilliseconds(120),
            MeasureRtt = true
        });

        var disconnectedTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += ex => disconnectedTcs.TrySetResult(ex);

        await client.StartAsync();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var registration = timeoutCts.Token.Register(() => disconnectedTcs.TrySetCanceled());
        var error = await disconnectedTcs.Task;

        var timeout = Assert.IsType<TimeoutException>(error);
        Assert.Contains("keepalive", timeout.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(client.TimedOutByKeepAlive);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task KeepAlive_OutgoingTrafficDoesNotSuppressTimeout()
    {
        var transport = new IdleClientTransport();
        var serializer = new JsonRpcSerializer();
        var client = new RpcClientRuntime(transport, serializer, new RpcKeepAliveOptions
        {
            Enabled = true,
            Interval = TimeSpan.FromMilliseconds(40),
            Timeout = TimeSpan.FromMilliseconds(120),
            MeasureRtt = true
        });

        var disconnectedTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += ex => disconnectedTcs.TrySetResult(ex);

        await client.StartAsync();

        using var spamCts = new CancellationTokenSource();
        var spamTask = Task.Run(async () =>
        {
            while (!spamCts.Token.IsCancellationRequested)
            {
                using var callCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
                try
                {
                    await client.CallAsync(EchoMethod, "spam", callCts.Token);
                }
                catch
                {
                }

                try
                {
                    await Task.Delay(20, spamCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var registration = timeoutCts.Token.Register(() => disconnectedTcs.TrySetCanceled());
        var error = await disconnectedTcs.Task;

        spamCts.Cancel();
        await spamTask;

        var timeout = Assert.IsType<TimeoutException>(error);
        Assert.Contains("keepalive", timeout.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(client.TimedOutByKeepAlive);

        await client.DisposeAsync();
    }

    private sealed class IdleClientTransport : ITransport
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

    private sealed class ControlledClientTransport : ITransport
    {
        private readonly Channel<ReadOnlyMemory<byte>> _responses = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentQueue<uint> _sentRequestIds = new();

        public IReadOnlyCollection<uint> SentRequestIds => _sentRequestIds;

        public bool IsConnected { get; private set; }

        public ValueTask ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            var request = RpcEnvelopeCodec.DecodeRequest(frame.Span);
            _sentRequestIds.Enqueue(request.RequestId);
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(CancellationToken ct = default)
        {
            return await _responses.Reader.ReadAsync(ct);
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            _responses.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public void Complete(uint requestId, byte[] payload)
        {
            _responses.Writer.TryWrite(RpcEnvelopeCodec.EncodeResponse(new RpcResponseEnvelope
            {
                RequestId = requestId,
                Status = RpcStatus.Ok,
                Payload = payload
            }));
        }

        public async Task WaitForSentRequestCountAsync(int count)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (_sentRequestIds.Count < count)
                await Task.Delay(10, cts.Token);
        }
    }

    private static void SetNextRequestId(RpcClientRuntime client, int value)
    {
        typeof(RpcClientRuntime)
            .GetField("_nextId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(client, value);
    }
}
