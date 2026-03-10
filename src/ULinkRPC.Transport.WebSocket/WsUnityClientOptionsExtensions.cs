using ULinkRPC.Client.Unity;

namespace ULinkRPC.Client.Unity;

public static class WsUnityClientOptionsExtensions
{
    public static RpcUnityClientOptions UseWebSocket(this RpcUnityClientOptions options, string url)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        return options.ConfigureBuilder(builder => builder.UseWebSocket(url));
    }
}
