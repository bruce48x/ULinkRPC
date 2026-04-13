using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;
using System.Text;
using ULinkRPC.Core;
using ULinkRPC.Transport.Kcp;

namespace ULinkRPC.Transport.Tests;

public class KcpTransportTests
{
    [Fact]
    public async Task KcpTransport_HandshakeHonorsCancellation()
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndPoint = (IPEndPoint)serverSocket.LocalEndPoint!;
        var handshakeObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            var buffer = new byte[32];
            EndPoint any = new IPEndPoint(IPAddress.Any, 0);
            var received = await serverSocket.ReceiveFromAsync(buffer, SocketFlags.None, any);
            if (received.ReceivedBytes > 0)
                handshakeObserved.TrySetResult();
        });

        await using var client = new KcpTransport(IPAddress.Loopback.ToString(), serverEndPoint.Port);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        using var observeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAsync(cts.Token).AsTask());
        await WithTimeout(handshakeObserved.Task, observeCts.Token);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task KcpTransport_Roundtrip()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await using var listener = new KcpListener(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndPoint = (IPEndPoint)listener.LocalEndPoint!;
        var acceptTask = listener.AcceptAsync(cts.Token).AsTask();

        await using var client = new KcpTransport(IPAddress.Loopback.ToString(), serverEndPoint.Port);
        await client.ConnectAsync(cts.Token);

        var accepted = await WithTimeout(acceptTask, cts.Token);
        await using var serverTransport = accepted.Transport;

        var payload = Encoding.UTF8.GetBytes("ping-kcp");
        await client.SendFrameAsync(payload, cts.Token);
        var serverReceived = await WithTimeout(serverTransport.ReceiveFrameAsync(cts.Token), cts.Token);
        Assert.Equal(payload, serverReceived.ToArray());

        var reply = Encoding.UTF8.GetBytes("pong-kcp");
        await serverTransport.SendFrameAsync(reply, cts.Token);
        var clientReceived = await WithTimeout(client.ReceiveFrameAsync(cts.Token), cts.Token);
        Assert.Equal(reply, clientReceived.ToArray());
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

    private static async ValueTask<T> WithTimeout<T>(ValueTask<T> task, CancellationToken ct)
    {
        return await WithTimeout(task.AsTask(), ct);
    }
}
