using System.Reflection;
using Microsoft.Extensions.Logging;
using ULinkRPC.Core;

namespace ULinkRPC.Server;

public sealed class RpcServerHostBuilder
{
    private Func<CancellationToken, ValueTask<IRpcConnectionAcceptor>>? _acceptorFactory;
    private RpcKeepAliveOptions _keepAlive = RpcKeepAliveOptions.Disabled;
    private ILogger _logger = DefaultRpcLogging.CreateLogger<RpcServerHost>();
    private bool _servicesConfigured;
    private IRpcSerializer? _serializer;

    public int? Port { get; private set; }

    public RpcServiceRegistry ServiceRegistry { get; } = new();

    public TransportSecurityConfig Security { get; } = new();

    public RpcKeepAliveOptions KeepAlive => _keepAlive;

    public static RpcServerHostBuilder Create()
    {
        return new RpcServerHostBuilder();
    }

    public RpcServerHostBuilder UseCommandLine(string[]? args)
    {
        if (args is null || args.Length == 0)
            return this;

        RpcServerHostCommandLineParser.Apply(this, args);
        return this;
    }

    public RpcServerHostBuilder UsePort(int port)
    {
        if (port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        Port = port;
        return this;
    }

    public RpcServerHostBuilder UseSerializer(IRpcSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    public RpcServerHostBuilder UseSerializer<TSerializer>()
        where TSerializer : IRpcSerializer, new()
    {
        _serializer = new TSerializer();
        return this;
    }

    public RpcServerHostBuilder UseSecurity(Action<TransportSecurityConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(Security);
        return this;
    }

    public RpcServerHostBuilder UseLogger(Action<string> logger)
    {
        _logger = new DelegateLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        return this;
    }

    public RpcServerHostBuilder UseLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    public RpcServerHostBuilder UseKeepAlive(RpcKeepAliveOptions keepAlive)
    {
        _keepAlive = keepAlive ?? throw new ArgumentNullException(nameof(keepAlive));
        return this;
    }

    public RpcServerHostBuilder UseKeepAlive(TimeSpan interval, TimeSpan timeout)
    {
        _keepAlive = new RpcKeepAliveOptions
        {
            Enabled = true,
            Interval = interval,
            Timeout = timeout,
            MeasureRtt = false
        };
        return this;
    }

    public RpcServerHostBuilder ConfigureServices(Action<RpcServiceRegistry> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(ServiceRegistry);
        _servicesConfigured = true;
        return this;
    }

    public RpcServerHostBuilder BindGeneratedServicesFromAssembly(Assembly assembly)
    {
        RpcGeneratedServiceBinder.BindFromAssembly(assembly, ServiceRegistry);
        _servicesConfigured = true;
        return this;
    }

    public RpcServerHostBuilder BindGeneratedServicesFromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Unable to resolve the entry assembly for generated RPC service binding.");

        return BindGeneratedServicesFromAssembly(assembly);
    }

    public RpcServerHostBuilder BindGeneratedServicesFromAssemblyContaining<T>()
    {
        return BindGeneratedServicesFromAssembly(typeof(T).Assembly);
    }

    public RpcServerHost Build()
    {
        if (_serializer is null)
            throw new InvalidOperationException("RPC serializer is not configured.");

        if (_acceptorFactory is null)
            throw new InvalidOperationException("RPC transport is not configured.");

        if (!_servicesConfigured && ServiceRegistry.IsEmpty)
            BindGeneratedServicesFromEntryAssembly();

        return new RpcServerHost(_serializer, ServiceRegistry, Security, _keepAlive, _acceptorFactory, _logger);
    }

    public ValueTask RunAsync(CancellationToken ct = default)
    {
        return Build().RunAsync(ct);
    }

    public int ResolvePort(int defaultPort)
    {
        return Port ?? defaultPort;
    }

    public RpcServerHostBuilder UseAcceptor(Func<CancellationToken, ValueTask<IRpcConnectionAcceptor>> acceptorFactory)
    {
        _acceptorFactory = acceptorFactory ?? throw new ArgumentNullException(nameof(acceptorFactory));
        return this;
    }

    public RpcServerHostBuilder UseAcceptor(IRpcConnectionAcceptor acceptor)
    {
        ArgumentNullException.ThrowIfNull(acceptor);
        _acceptorFactory = _ => ValueTask.FromResult(acceptor);
        return this;
    }
}
