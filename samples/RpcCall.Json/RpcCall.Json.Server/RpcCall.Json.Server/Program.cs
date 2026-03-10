using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.WebSocket;

await RpcServerHostBuilder.Create()
    .UseCommandLine(args)
    .UseJson()
    .UseWebSocket(defaultPort: 20000, path: "/ws")
    .RunAsync();
