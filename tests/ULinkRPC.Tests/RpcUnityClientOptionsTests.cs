using ULinkRPC.Client.Unity;
using Xunit;

namespace ULinkRPC.Tests;

public class RpcUnityClientOptionsTests
{
    [Fact]
    public void MemoryPackTcp_CreatesBuilderAndClient()
    {
        var options = RpcUnityClientOptions.MemoryPackTcp("127.0.0.1", 20000);

        var client = options.CreateBuilder().Build();

        Assert.NotNull(client);
    }

    [Fact]
    public void JsonWebSocket_CreatesBuilderAndClient()
    {
        var options = RpcUnityClientOptions.JsonWebSocket("ws://127.0.0.1:20000/ws");

        var client = options.CreateBuilder().Build();

        Assert.NotNull(client);
    }

    [Fact]
    public void Kcp_UsesRequestedKinds()
    {
        var options = RpcUnityClientOptions.Kcp("127.0.0.1", 20000, RpcUnitySerializerKind.Json);

        Assert.Equal(RpcUnityTransportKind.Kcp, options.Transport);
        Assert.Equal(RpcUnitySerializerKind.Json, options.Serializer);
        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(20000, options.Port);
    }
}
