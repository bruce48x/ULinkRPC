using ULinkRPC.Client.Unity;

namespace ULinkRPC.Client.Unity;

public static class KcpUnityClientOptionsExtensions
{
    public static RpcUnityClientOptions UseKcp(this RpcUnityClientOptions options, string host, int port)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        return options.ConfigureBuilder(builder => builder.UseKcp(host, port));
    }
}
