using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Kcp;

var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new MemoryPackRpcSerializer());

builder.UseAcceptor(new KcpConnectionAcceptor(
    20000,
    builder.Limits.MaxPendingAcceptedConnections));

await builder.RunAsync();
