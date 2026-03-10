#if NET10_0_OR_GREATER
using ULinkRPC.Server;

namespace ULinkRPC.Serializer.MemoryPack;

public static class MemoryPackServerHostBuilderExtensions
{
    public static RpcServerHostBuilder UseMemoryPack(this RpcServerHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSerializer<MemoryPackRpcSerializer>();
    }
}
#endif
