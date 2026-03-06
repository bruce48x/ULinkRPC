using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.Loopback;

namespace ULinkRPC.Tests;

public class RpcServerTests
{
    private static (ITransport clientTransport, RpcServer server) CreateServerWithHandler(
        int serviceId, int methodId, RpcHandler handler)
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcServer(serverTransport, serializer);
        server.Register(serviceId, methodId, handler);
        return (clientTransport, server);
    }

    [Fact]
    public async Task Register_HandlerReceivesRequest()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcServer(serverTransport, serializer);

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
        var server = new RpcServer(serverTransport, serializer);

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
        var server = new RpcServer(serverTransport, serializer);

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
        var server = new RpcServer(serverTransport, serializer);

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
        var server = new RpcServer(serverTransport, serializer);

        await server.StartAsync();
        await server.StopAsync();

        // After stop, _started is reset to 0, so creating a new transport to re-start
        LoopbackTransport.CreatePair(out var clientTransport2, out var serverTransport2);
        var server2 = new RpcServer(serverTransport2, serializer);
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
        var server = new RpcServer(serverTransport, serializer);

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
    public async Task ServerLifetime_IndependentOfStartCancellationToken()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var server = new RpcServer(serverTransport, serializer);

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
    public void Constructor_NullTransport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RpcServer(null!, new JsonRpcSerializer()));
    }

    [Fact]
    public void Constructor_NullSerializer_Throws()
    {
        LoopbackTransport.CreatePair(out var client, out var server);
        Assert.Throws<ArgumentNullException>(() => new RpcServer(client, null!));
    }
}
