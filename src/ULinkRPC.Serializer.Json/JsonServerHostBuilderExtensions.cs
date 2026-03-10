#if NET10_0_OR_GREATER
using System.Text.Json;
using ULinkRPC.Server;

namespace ULinkRPC.Serializer.Json;

public static class JsonServerHostBuilderExtensions
{
    public static RpcServerHostBuilder UseJson(
        this RpcServerHostBuilder builder,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseSerializer(new JsonRpcSerializer(options));
    }
}
#endif
