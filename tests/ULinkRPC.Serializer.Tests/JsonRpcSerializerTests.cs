using ULinkRPC.Serializer.Json;

namespace ULinkRPC.Serializer.Tests;

public class JsonRpcSerializerTests
{
    [Fact]
    public void JsonSerializer_RoundTrip_LoginRequest()
    {
        var serializer = new JsonRpcSerializer();
        var input = new LoginRequestDto
        {
            Account = "server",
            Password = "secret"
        };

        var bytes = serializer.Serialize(input);
        var output = serializer.Deserialize<LoginRequestDto>(bytes.AsSpan());

        Assert.Equal(input.Account, output.Account);
        Assert.Equal(input.Password, output.Password);
    }

    [Fact]
    public void JsonSerializer_RoundTrip_LoginReply()
    {
        var serializer = new JsonRpcSerializer();
        var input = new LoginReplyDto
        {
            Code = 200,
            Token = "token"
        };

        var bytes = serializer.Serialize(input);
        var output = serializer.Deserialize<LoginReplyDto>(bytes.AsSpan());

        Assert.Equal(input.Code, output.Code);
        Assert.Equal(input.Token, output.Token);
    }

    public sealed class LoginRequestDto
    {
        public string Account { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class LoginReplyDto
    {
        public int Code { get; set; }
        public string Token { get; set; } = string.Empty;
    }
}
