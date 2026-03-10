using System.Text.Json;
using ULinkRPC.Client;

namespace ULinkRPC.Client;

public static class JsonClientBuilderExtensions
{
    public static RpcClientBuilder UseJson(
        this RpcClientBuilder builder,
        JsonSerializerOptions? options = null)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.UseSerializer(new global::ULinkRPC.Serializer.Json.JsonRpcSerializer(options));
    }
}
