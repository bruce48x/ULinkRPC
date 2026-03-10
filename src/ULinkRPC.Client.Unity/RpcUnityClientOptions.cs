using ULinkRPC.Client;

namespace ULinkRPC.Client.Unity;

public sealed class RpcUnityClientOptions
{
    private readonly List<Action<RpcClientBuilder>> _configurators = [];

    public static RpcUnityClientOptions Create()
    {
        return new RpcUnityClientOptions();
    }

    public RpcUnityClientOptions ConfigureBuilder(Action<RpcClientBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        _configurators.Add(configure);
        return this;
    }

    public RpcClientBuilder CreateBuilder()
    {
        var builder = RpcClientBuilder.Create();
        foreach (var configure in _configurators)
            configure(builder);
        return builder;
    }
}
