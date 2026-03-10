using ULinkRPC.Client;
using ULinkRPC.Core;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Server;
using ULinkRPC.Transport.Loopback;

namespace ULinkRPC.Tests;

public class RpcClientBuilderTests
{
    private static readonly RpcMethod<string, string> EchoMethod = new(1, 1);

    [Fact]
    public void Build_WithoutTransport_Throws()
    {
        var builder = RpcClientBuilder.Create()
            .UseSerializer(new JsonRpcSerializer());

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("transport", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WithoutSerializer_Throws()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out _);
        var builder = RpcClientBuilder.Create()
            .UseTransport(clientTransport);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("serializer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectApiAsync_StartsClientAndReturnsTypedApi()
    {
        LoopbackTransport.CreatePair(out var clientTransport, out var serverTransport);
        var serializer = new JsonRpcSerializer();
        var registry = new RpcServiceRegistry();
        var configuredClient = default(IRpcClient);

        registry.Register(1, 1, (session, request, ct) =>
        {
            var arg = serializer.Deserialize<string>(request.Payload.AsSpan());
            return ValueTask.FromResult(new RpcResponseEnvelope
            {
                RequestId = request.RequestId,
                Status = RpcStatus.Ok,
                Payload = serializer.Serialize($"echo:{arg}")
            });
        });

        await using var server = new RpcSession(serverTransport, serializer, registry, ownsTransport: true);
        await server.StartAsync();

        var builder = RpcClientBuilder.Create()
            .UseTransport(clientTransport)
            .UseSerializer(serializer)
            .ConfigureClient(client => configuredClient = client);

        await using var connection = await builder.ConnectApiAsync(client => new TestApi(client));
        var result = await connection.Api.EchoAsync("hello");

        Assert.NotNull(configuredClient);
        Assert.Same(configuredClient, connection.Client);
        Assert.Equal("echo:hello", result);

        await server.StopAsync();
    }

    private sealed class TestApi
    {
        private readonly IRpcClient _client;

        public TestApi(IRpcClient client)
        {
            _client = client;
        }

        public ValueTask<string> EchoAsync(string value)
        {
            return _client.CallAsync(EchoMethod, value);
        }
    }
}
