using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using ULinkRPC.Transport.WebSocket;

namespace ULinkRPC.Transport.Tests;

public class WebSocketTransportTests
{
    [Fact]
    public async Task WebSocketTransport_Roundtrip()
    {
        var port = GetFreePort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        await using var app = builder.Build();
        app.UseWebSockets();

        var serverReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, context.RequestAborted);
            var wsContext = await context.WebSockets.AcceptWebSocketAsync();
            await using var transport = new WsServerTransport(wsContext);

            var payload = await WithTimeout(transport.ReceiveFrameAsync(linkedCts.Token), linkedCts.Token);
            Assert.Equal("ping-ws", Encoding.UTF8.GetString(payload.Span));

            await WithTimeout(
                transport.SendFrameAsync(Encoding.UTF8.GetBytes("pong-ws"), linkedCts.Token),
                linkedCts.Token);

            serverReceived.TrySetResult();
        });

        await app.StartAsync(cts.Token);

        try
        {
            await using var client = new WsTransport($"ws://127.0.0.1:{port}/ws/");
            await WithTimeout(client.ConnectAsync(cts.Token), cts.Token);
            await WithTimeout(client.SendFrameAsync(Encoding.UTF8.GetBytes("ping-ws"), cts.Token), cts.Token);
            var response = await WithTimeout(client.ReceiveFrameAsync(cts.Token), cts.Token);
            Assert.Equal("pong-ws", Encoding.UTF8.GetString(response.Span));

            await WithTimeout(serverReceived.Task, cts.Token);
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void WsConnectionAcceptor_Source_DoesNotUseUnboundedPendingConnectionQueue()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ULinkRPC.Transport.WebSocket", "WsConnectionAcceptor.cs"));

        var source = File.ReadAllText(sourcePath);
        Assert.DoesNotContain(
            "Channel.CreateUnbounded<RpcAcceptedConnection>()",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task WsConnectionAcceptor_AcceptAsync_SkipsDisconnectedQueuedConnection()
    {
        var port = GetFreePort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var acceptor = await WsConnectionAcceptor.CreateAsync(port, "/ws", 2, cts.Token);

        using var staleClient = new ClientWebSocket();
        await staleClient.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), cts.Token);
        await Task.Delay(150, cts.Token);
        staleClient.Abort();
        staleClient.Dispose();

        using var liveClient = new ClientWebSocket();
        await liveClient.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), cts.Token);

        var accepted = await WithTimeout(acceptor.AcceptAsync(cts.Token), cts.Token);
        try
        {
            using var packed = ULinkRPC.Core.LengthPrefix.Pack(Encoding.UTF8.GetBytes("live"));
            await WithTimeout(
                liveClient.SendAsync(packed.Memory, WebSocketMessageType.Binary, true, cts.Token),
                cts.Token);

            var payload = await WithTimeout(accepted.Transport.ReceiveFrameAsync(cts.Token), cts.Token);
            Assert.Equal("live", Encoding.UTF8.GetString(payload.Span));
        }
        finally
        {
            await accepted.Transport.DisposeAsync();
        }
    }

    [Fact]
    public async Task WsServerTransport_DisposeAsync_DoesNotHangAfterRemoteAbort()
    {
        var port = GetFreePort();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var acceptor = await WsConnectionAcceptor.CreateAsync(port, "/ws", 1, cts.Token);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws"), cts.Token);

        var accepted = await WithTimeout(acceptor.AcceptAsync(cts.Token), cts.Token);
        client.Abort();
        client.Dispose();

        await WithTimeout(accepted.Transport.DisposeAsync(), cts.Token);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WithTimeout(Task task, CancellationToken ct)
    {
        var delay = Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var completed = await Task.WhenAny(task, delay);
        if (completed != task)
            throw new TimeoutException("Operation timed out.");

        await task;
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, CancellationToken ct)
    {
        var delay = Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var completed = await Task.WhenAny(task, delay);
        if (completed != task)
            throw new TimeoutException("Operation timed out.");

        return await task;
    }

    private static async ValueTask WithTimeout(ValueTask task, CancellationToken ct)
    {
        await WithTimeout(task.AsTask(), ct);
    }

    private static async ValueTask<T> WithTimeout<T>(ValueTask<T> task, CancellationToken ct)
    {
        return await WithTimeout(task.AsTask(), ct);
    }
}
