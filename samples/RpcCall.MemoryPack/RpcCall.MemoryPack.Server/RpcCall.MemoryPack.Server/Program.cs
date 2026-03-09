using System.Net;
using System.Net.Sockets;
using Game.Rpc.Server.Generated;
using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Tcp;

const int defaultTcpPort = 20000;
var tcpPort = defaultTcpPort;
var security = new TransportSecurityConfig();

var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    if (!arg.StartsWith("--", StringComparison.Ordinal))
    {
        positional.Add(arg);
        continue;
    }

    if (arg.StartsWith("--compress", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableCompression = true;
        var parts = arg.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out var threshold))
            security.CompressionThresholdBytes = threshold;
        continue;
    }

    if (arg.StartsWith("--compress-threshold", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableCompression = true;
        if (TryReadNext(args, ref i, out var value) && int.TryParse(value, out var threshold))
            security.CompressionThresholdBytes = threshold;
        continue;
    }

    if (arg.StartsWith("--encrypt-key", StringComparison.OrdinalIgnoreCase))
    {
        security.EnableEncryption = true;
        var parts = arg.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
            security.EncryptionKeyBase64 = parts[1];
        else if (TryReadNext(args, ref i, out var value))
            security.EncryptionKeyBase64 = value;
        continue;
    }

    if (arg.Equals("--encrypt", StringComparison.OrdinalIgnoreCase)) security.EnableEncryption = true;
}

if (positional.Count > 0 && int.TryParse(positional[0], out var p))
    tcpPort = p;
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"RpcCall Server TCP listening on 0.0.0.0:{tcpPort}. Press Ctrl+C to stop.");

var tcpTask = RunTcpListenerAsync(tcpPort, cts.Token);

try
{
    await tcpTask.ConfigureAwait(false);
}
finally
{
    Console.WriteLine("Server stopped.");
}

async Task RunTcpListenerAsync(int port, CancellationToken hostCt)
{
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();

    try
    {
        while (!hostCt.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(hostCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var transport = new TcpServerTransport(client);
            _ = RunConnectionAsync(WrapSecurity(transport), client.Client.RemoteEndPoint?.ToString() ?? "?", hostCt);
        }
    }
    finally
    {
        listener.Stop();
    }
}

async Task RunConnectionAsync(ITransport transport, string remote, CancellationToken hostCt)
{
    RpcServer? server = null;

    try
    {
        server = new RpcServer(transport, new MemoryPackRpcSerializer());

        AllServicesBinder.BindAll(server);
        await server.StartAsync(hostCt).ConfigureAwait(false);
        await server.WaitForCompletionAsync().ConfigureAwait(false);
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
        if (server is not null)
            await server.StopAsync().ConfigureAwait(false);

        await transport.DisposeAsync().ConfigureAwait(false);
    }

    Console.WriteLine($"[{remote}] Disconnected.");
}

ITransport WrapSecurity(ITransport transport)
{
    if (!security.IsEnabled)
        return transport;

    return new TransformingTransport(transport, security);
}

static bool TryReadNext(string[] args, ref int index, out string value)
{
    var next = index + 1;
    if (next >= args.Length)
    {
        value = string.Empty;
        return false;
    }

    index = next;
    value = args[next];
    return true;
}
