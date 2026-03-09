using System.Net;
using System.Net.Sockets;
using System.Text;
using ULinkRPC.Transport.WebSocket;
using Xunit;

namespace RpcCall.Json.Server.Tests;

public class WebSocketTransportTests
{
    [Fact]
    public async Task WebSocketTransport_Roundtrip()
    {
        var port = GetFreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/ws/");
        listener.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(async () =>
        {
            var ctx = await WithTimeout(listener.GetContextAsync(), cts.Token);
            var wsContext = await WithTimeout(ctx.AcceptWebSocketAsync(null), cts.Token);
            await using var transport = new WsServerTransport(wsContext.WebSocket);

            var payload = await WithTimeout(transport.ReceiveFrameAsync(cts.Token), cts.Token);
            Assert.Equal("ping-ws", Encoding.UTF8.GetString(payload.Span));

            await WithTimeout(
                transport.SendFrameAsync(Encoding.UTF8.GetBytes("pong-ws"), cts.Token),
                cts.Token);
        }, cts.Token);

        try
        {
            await using var client = new WsTransport($"ws://127.0.0.1:{port}/ws/");
            await WithTimeout(client.ConnectAsync(cts.Token), cts.Token);
            await WithTimeout(client.SendFrameAsync(Encoding.UTF8.GetBytes("ping-ws"), cts.Token), cts.Token);
            var response = await WithTimeout(client.ReceiveFrameAsync(cts.Token), cts.Token);
            Assert.Equal("pong-ws", Encoding.UTF8.GetString(response.Span));

            await WithTimeout(serverTask, cts.Token);
        }
        finally
        {
            try
            {
                listener.Stop();
            }
            catch
            {
            }

            if (!serverTask.IsCompleted)
                try
                {
                    await WithTimeout(serverTask, cts.Token);
                }
                catch
                {
                }
        }
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
