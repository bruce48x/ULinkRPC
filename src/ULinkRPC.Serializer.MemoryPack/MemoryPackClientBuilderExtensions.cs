using ULinkRPC.Client;

namespace ULinkRPC.Client;

public static class MemoryPackClientBuilderExtensions
{
    public static RpcClientBuilder UseMemoryPack(this RpcClientBuilder builder)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        return builder.UseSerializer(new global::ULinkRPC.Serializer.MemoryPack.MemoryPackRpcSerializer());
    }
}
