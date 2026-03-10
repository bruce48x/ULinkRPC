using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;

[assembly: RpcGeneratedServicesBinder(typeof(ULinkRPC.Tests.TestGeneratedBinder))]

namespace ULinkRPC.Tests;

public class RpcServerHostBuilderTests
{
    [Fact]
    public void UseCommandLine_ParsesPortCompressionAndEncryption()
    {
        var builder = RpcServerHostBuilder.Create()
            .UseCommandLine(["21000", "--compress-threshold", "4096", "--encrypt-key", "AQIDBA=="]);

        Assert.Equal(21000, builder.Port);
        Assert.True(builder.Security.EnableCompression);
        Assert.Equal(4096, builder.Security.CompressionThresholdBytes);
        Assert.True(builder.Security.EnableEncryption);
        Assert.Equal("AQIDBA==", builder.Security.EncryptionKeyBase64);
    }

    [Fact]
    public void Build_WhenServicesConfiguredExplicitly_DoesNotRequireGeneratedBinderDiscovery()
    {
        var builder = RpcServerHostBuilder.Create()
            .UseSerializer(new JsonRpcSerializer())
            .UseAcceptor(_ => ValueTask.FromResult<IRpcConnectionAcceptor>(new NoopConnectionAcceptor()))
            .ConfigureServices(_ => { });

        var host = builder.Build();

        Assert.NotNull(host);
    }

    [Fact]
    public void BindFromAssembly_UsesAssemblyLevelGeneratedBinderAttribute()
    {
        var registry = new RpcServiceRegistry();

        RpcGeneratedServiceBinder.BindFromAssembly(typeof(TestGeneratedBinder).Assembly, registry);

        Assert.False(registry.IsEmpty);
        Assert.True(registry.TryGetHandler(7, 9, out _));
    }

    private sealed class NoopConnectionAcceptor : IRpcConnectionAcceptor
    {
        public string ListenAddress => "test://noop";

        public ValueTask<RpcAcceptedConnection> AcceptAsync(CancellationToken ct = default)
        {
            return ValueTask.FromException<RpcAcceptedConnection>(new NotSupportedException());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

public static class TestGeneratedBinder
{
    public static void BindAll(RpcServiceRegistry registry)
    {
        registry.Register(7, 9, static (session, request, ct) => ValueTask.FromResult(new RpcResponseEnvelope
        {
            RequestId = request.RequestId,
            Status = RpcStatus.Ok,
            Payload = Array.Empty<byte>()
        }));
    }
}
