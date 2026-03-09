using System.Net;
using System.Net.WebSockets;
using Game.Rpc.Server.Generated;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using ULinkRPC.Server;
using ULinkRPC.Serializer.Json;
using ULinkRPC.Transport.WebSocket;

const int defaultPort = 20000;
const string webSocketPath = "/ws";
var port = defaultPort;
var serviceRegistry = new RpcServiceRegistry();
AllServicesBinder.BindAll(serviceRegistry);
if (args.Length > 0 && int.TryParse(args[0], out var p))
    port = p;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();
app.UseWebSockets();
app.Map(webSocketPath, wsApp =>
{
    wsApp.Run(context => HandleWebSocketAsync(context, cts.Token));
});
app.MapGet("/", () => Results.Text($"RpcCall.Json WebSocket endpoint: ws://127.0.0.1:{port}{webSocketPath}"));

try
{
    Console.WriteLine($"RpcCall Server WebSocket listening on ws://0.0.0.0:{port}{webSocketPath}. Press Ctrl+C to stop.");
    await app.StartAsync(cts.Token).ConfigureAwait(false);
    await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    // Ctrl+C
}
finally
{
    await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine("Server stopped.");
}

async Task HandleWebSocketAsync(HttpContext context, CancellationToken hostCt)
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket upgrade request.", hostCt).ConfigureAwait(false);
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
    var remoteEndPoint = context.Connection.RemoteIpAddress is null
        ? null
        : new IPEndPoint(context.Connection.RemoteIpAddress, context.Connection.RemotePort);
    var remote = remoteEndPoint?.ToString() ?? "?";
    var transport = new WsServerTransport(webSocket, remoteEndPoint);
    RpcSession? session = null;

    try
    {
        session = new RpcSession(transport, new JsonRpcSerializer(), serviceRegistry);
        await session.StartAsync(hostCt).ConfigureAwait(false);
        await session.WaitForCompletionAsync().ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Host shutdown
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{remote}] Error: {ex}");
    }
    finally
    {
        if (session is not null)
            await session.StopAsync().ConfigureAwait(false);

        await transport.DisposeAsync().ConfigureAwait(false);
    }

    Console.WriteLine($"[{remote}] Disconnected.");
}
