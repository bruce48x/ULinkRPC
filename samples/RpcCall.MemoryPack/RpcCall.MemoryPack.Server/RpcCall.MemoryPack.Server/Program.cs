using ULinkRPC.Server;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Tcp;

await RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseMemoryPack()
    .UseTcp(defaultPort: 20000)
    .RunAsync();
