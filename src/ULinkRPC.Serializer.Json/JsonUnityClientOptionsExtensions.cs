using System.Text.Json;
using ULinkRPC.Client.Unity;

namespace ULinkRPC.Client.Unity;

public static class JsonUnityClientOptionsExtensions
{
    public static RpcUnityClientOptions UseJson(
        this RpcUnityClientOptions options,
        JsonSerializerOptions? serializerOptions = null)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        return options.ConfigureBuilder(builder => builder.UseJson(serializerOptions));
    }
}
