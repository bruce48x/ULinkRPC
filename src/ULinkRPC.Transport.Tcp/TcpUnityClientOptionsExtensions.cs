using ULinkRPC.Client.Unity;

namespace ULinkRPC.Client.Unity;

public static class TcpUnityClientOptionsExtensions
{
    public static RpcUnityClientOptions UseTcp(this RpcUnityClientOptions options, string host, int port)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        return options.ConfigureBuilder(builder => builder.UseTcp(host, port));
    }
}
