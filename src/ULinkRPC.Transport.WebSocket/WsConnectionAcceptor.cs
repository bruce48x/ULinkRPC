#if NET10_0_OR_GREATER
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ULinkRPC.Core;

namespace ULinkRPC.Transport.WebSocket;

public sealed class WsConnectionAcceptor : IRpcConnectionAcceptor
{
    private readonly WebApplication _app;
    private readonly Channel<RpcAcceptedConnection> _connections = Channel.CreateUnbounded<RpcAcceptedConnection>();
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly string _listenAddress;
    private int _disposed;

    private WsConnectionAcceptor(WebApplication app, string listenAddress)
    {
        _app = app;
        _listenAddress = listenAddress;
    }

    public string ListenAddress => _listenAddress;

    public static async ValueTask<WsConnectionAcceptor> CreateAsync(int port, string path, CancellationToken ct = default)
    {
        var normalizedPath = NormalizePath(path);
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var app = builder.Build();
        var acceptor = new WsConnectionAcceptor(app, $"ws://0.0.0.0:{port}{normalizedPath}");

        app.UseWebSockets();
        app.Map(normalizedPath, acceptor.HandleAsync);

        await app.StartAsync(ct).ConfigureAwait(false);
        return acceptor;
    }

    public async ValueTask<RpcAcceptedConnection> AcceptAsync(CancellationToken ct = default)
    {
        return await _connections.Reader.ReadAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _disposeCts.Cancel();
        _connections.Writer.TryComplete();

        try
        {
            await _app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            _disposeCts.Dispose();
        }
    }

    private async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket upgrade request.", _disposeCts.Token).ConfigureAwait(false);
            return;
        }

        var remoteEndPoint = context.Connection.RemoteIpAddress is null
            ? null
            : new IPEndPoint(context.Connection.RemoteIpAddress, context.Connection.RemotePort);

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new WsServerTransport(
            await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false),
            remoteEndPoint,
            () => completion.TrySetResult(null));

        try
        {
            await _connections.Writer.WriteAsync(
                new RpcAcceptedConnection(transport, remoteEndPoint?.ToString() ?? "?", remoteEndPoint),
                _disposeCts.Token).ConfigureAwait(false);

            using var registration = _disposeCts.Token.Register(static state =>
            {
                ((TaskCompletionSource<object?>)state!).TrySetCanceled();
            }, completion);

            await completion.Task.ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/ws";

        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }
}
#endif
