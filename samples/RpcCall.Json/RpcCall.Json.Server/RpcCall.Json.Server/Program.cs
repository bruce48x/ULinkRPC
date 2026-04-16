using ULinkRPC.Core;
using System;
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.WebSocket;

var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseSerializer(new JsonRpcSerializer())
    .UseKeepAlive(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45));

builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(
    20000,
    "/ws",
    builder.Limits.MaxPendingAcceptedConnections,
    ct));

await builder.RunAsync();
