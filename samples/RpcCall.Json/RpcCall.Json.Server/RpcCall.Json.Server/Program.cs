using System;
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.WebSocket;

await RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseJson()
    .UseKeepAlive(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45))
    .UseWebSocket(defaultPort: 20000, path: "/ws")
    .RunAsync();
