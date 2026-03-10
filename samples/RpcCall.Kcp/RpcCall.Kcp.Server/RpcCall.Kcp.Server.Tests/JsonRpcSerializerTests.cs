using Game.Rpc.Contracts;
using ULinkRPC.Core;
using ULinkRPC.Serializer.Json;
using Xunit;

namespace RpcCall.Kcp.Server.Tests;

public class JsonRpcSerializerTests
{
    [Fact]
    public void JsonSerializer_RoundTrip_LoginRequest()
    {
        var serializer = new JsonRpcSerializer();
        var input = new LoginRequest
        {
            Account = "server",
            Password = "secret"
        };

        var bytes = serializer.Serialize(input);
        var output = serializer.Deserialize<LoginRequest>(bytes.AsSpan());

        Assert.Equal(input.Account, output.Account);
        Assert.Equal(input.Password, output.Password);
    }

    [Fact]
    public void JsonSerializer_RoundTrip_LoginReply()
    {
        var serializer = new JsonRpcSerializer();
        var input = new LoginReply
        {
            Code = 200,
            Token = "token"
        };

        var bytes = serializer.Serialize(input);
        var output = serializer.Deserialize<LoginReply>(bytes.AsSpan());

        Assert.Equal(input.Code, output.Code);
        Assert.Equal(input.Token, output.Token);
    }
}
