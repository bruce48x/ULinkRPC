using ULinkRPC.Server;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Kcp;

await RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseMemoryPack()
    .UseKcp(defaultPort: 20000)
    .RunAsync();
