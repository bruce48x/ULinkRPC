using ULinkRPC.Client.Unity;

namespace ULinkRPC.Client.Unity;

public static class MemoryPackUnityClientOptionsExtensions
{
    public static RpcUnityClientOptions UseMemoryPack(this RpcUnityClientOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        return options.ConfigureBuilder(static builder => builder.UseMemoryPack());
    }
}
