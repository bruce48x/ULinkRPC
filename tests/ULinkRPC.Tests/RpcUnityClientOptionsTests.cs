using ULinkRPC.Client.Unity;
using Xunit;

namespace ULinkRPC.Tests;

public class RpcUnityClientOptionsTests
{
    [Fact]
    public void JsonTcp_CreatesBuilderAndClient()
    {
        var options = RpcUnityClientOptions.Create()
            .UseJson()
            .UseTcp("127.0.0.1", 20000);

        var client = options.CreateBuilder().Build();

        Assert.NotNull(client);
    }

    [Fact]
    public void ConfigureBuilder_ComposesSteps()
    {
        var options = RpcUnityClientOptions.Create()
            .ConfigureBuilder(static _ => { })
            .ConfigureBuilder(static _ => { });

        var builder = options.CreateBuilder();

        Assert.NotNull(builder);
    }
}
