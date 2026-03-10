using Game.Rpc.Server.Generated;
using ULinkRPC.Core;
using ULinkRPC.Server;
using ULinkRPC.Serializer.MemoryPack;
using ULinkRPC.Transport.Kcp;

const int defaultKcpPort = 20000;
var kcpPort = defaultKcpPort;
var security = new TransportSecurityConfig();
var serviceRegistry = new RpcServiceRegistry();
AllServicesBinder.BindAll(serviceRegistry);

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

    if (arg.Equals("--encrypt", StringComparison.OrdinalIgnoreCase))
        security.EnableEncryption = true;
}

if (positional.Count > 0 && int.TryParse(positional[0], out var parsedPort))
    kcpPort = parsedPort;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await using var listener = new KcpListener(kcpPort);
Console.WriteLine($"RpcCall.Kcp server listening on udp://0.0.0.0:{kcpPort}. Press Ctrl+C to stop.");

try
{
    while (!cts.IsCancellationRequested)
    {
        KcpAcceptResult accepted;
        try
        {
            accepted = await listener.AcceptAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        Console.WriteLine($"[{accepted.RemoteEndPoint}] accepted on udp://0.0.0.0:{accepted.LocalPort} conv={accepted.ConversationId}");
        _ = RunConnectionAsync(accepted.Transport, accepted.RemoteEndPoint.ToString() ?? "?", cts.Token);
    }
}
finally
{
    Console.WriteLine("Server stopped.");
}

async Task RunConnectionAsync(ITransport transport, string remote, CancellationToken hostCt)
{
    var wrapped = WrapSecurity(transport);
    RpcSession? session = null;

    try
    {
        session = new RpcSession(wrapped, new MemoryPackRpcSerializer(), serviceRegistry);
        await session.StartAsync(hostCt).ConfigureAwait(false);
        await session.WaitForCompletionAsync().ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{remote}] Error: {ex}");
    }
    finally
    {
        if (session is not null)
            await session.StopAsync().ConfigureAwait(false);

        await wrapped.DisposeAsync().ConfigureAwait(false);
    }

    Console.WriteLine($"[{remote}] disconnected.");
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
