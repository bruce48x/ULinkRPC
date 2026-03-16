using System.Collections.Concurrent;
using ULinkRPC.Core;

namespace ULinkRPC.Server;

public sealed class RpcServerHost
{
    private readonly Func<CancellationToken, ValueTask<IRpcConnectionAcceptor>> _acceptorFactory;
    private readonly Action<string> _logger;
    private readonly RpcKeepAliveOptions _keepAlive;
    private readonly RpcServiceRegistry _registry;
    private readonly TransportSecurityConfig _security;
    private readonly IRpcSerializer _serializer;
    private int _nextConnectionId;

    internal RpcServerHost(
        IRpcSerializer serializer,
        RpcServiceRegistry registry,
        TransportSecurityConfig security,
        RpcKeepAliveOptions keepAlive,
        Func<CancellationToken, ValueTask<IRpcConnectionAcceptor>> acceptorFactory,
        Action<string> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _security = security ?? throw new ArgumentNullException(nameof(security));
        _keepAlive = keepAlive ?? throw new ArgumentNullException(nameof(keepAlive));
        _acceptorFactory = acceptorFactory ?? throw new ArgumentNullException(nameof(acceptorFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask RunAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ConsoleCancelEventHandler? cancelHandler = null;
        var connectionTasks = new ConcurrentDictionary<int, Task>();

        cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            await using var acceptor = await _acceptorFactory(cts.Token).ConfigureAwait(false);
            _logger($"RPC server listening on {acceptor.ListenAddress}. Press Ctrl+C to stop.");

            while (!cts.IsCancellationRequested)
            {
                RpcAcceptedConnection connection;
                try
                {
                    connection = await acceptor.AcceptAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _logger($"[{connection.DisplayName}] accepted.");

                var connectionId = Interlocked.Increment(ref _nextConnectionId);
                var connectionTask = RunConnectionAsync(connection, cts.Token);
                connectionTasks[connectionId] = connectionTask;

                _ = connectionTask.ContinueWith(
                    _ =>
                    {
                        connectionTasks.TryRemove(connectionId, out Task? _);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            cts.Cancel();
            await WaitForConnectionsAsync(connectionTasks).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            _logger("Server stopped.");
        }
    }

    private async Task RunConnectionAsync(RpcAcceptedConnection connection, CancellationToken hostCt)
    {
        var transport = WrapSecurity(connection.Transport);
        await using var session = new RpcSession(
            transport,
            _serializer,
            _registry,
            connection.DisplayName,
            ownsTransport: true,
            keepAlive: _keepAlive);

        try
        {
            await session.RunAsync(hostCt).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (hostCt.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger($"[{connection.DisplayName}] Error: {ex}");
        }
        finally
        {
            _logger($"[{connection.DisplayName}] disconnected.");
        }
    }

    private ITransport WrapSecurity(ITransport transport)
    {
        if (!_security.IsEnabled)
            return transport;

        return new TransformingTransport(transport, _security);
    }

    private static async ValueTask WaitForConnectionsAsync(ConcurrentDictionary<int, Task> connectionTasks)
    {
        while (true)
        {
            var tasks = connectionTasks.Values.ToArray();
            if (tasks.Length == 0)
                return;

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
            }

            if (connectionTasks.IsEmpty)
                return;
        }
    }
}
