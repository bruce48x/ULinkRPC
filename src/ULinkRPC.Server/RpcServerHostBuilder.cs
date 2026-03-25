using System.Reflection;
using ULinkRPC.Core;

namespace ULinkRPC.Server;

public sealed class RpcServerHostBuilder
{
    private Func<CancellationToken, ValueTask<IRpcConnectionAcceptor>>? _acceptorFactory;
    private RpcKeepAliveOptions _keepAlive = RpcKeepAliveOptions.Disabled;
    private Action<string> _logger = Console.WriteLine;
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

        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            if (arg.StartsWith("--port", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadInlineValue(arg, out var inlinePort))
                    UsePort(ParseInt32(inlinePort, "--port"));
                else if (TryReadNext(args, ref i, out var nextPort))
                    UsePort(ParseInt32(nextPort, "--port"));

                continue;
            }

            if (arg.StartsWith("--compress-threshold", StringComparison.OrdinalIgnoreCase))
            {
                Security.EnableCompression = true;
                if (TryReadInlineValue(arg, out var inlineThreshold))
                    Security.CompressionThresholdBytes = ParseInt32(inlineThreshold, "--compress-threshold");
                else if (TryReadNext(args, ref i, out var threshold))
                    Security.CompressionThresholdBytes = ParseInt32(threshold, "--compress-threshold");

                continue;
            }

            if (arg.StartsWith("--compress", StringComparison.OrdinalIgnoreCase))
            {
                Security.EnableCompression = true;
                if (TryReadInlineValue(arg, out var inlineThreshold))
                    Security.CompressionThresholdBytes = ParseInt32(inlineThreshold, "--compress");
                continue;
            }

            if (arg.Equals("--encrypt", StringComparison.OrdinalIgnoreCase))
            {
                Security.EnableEncryption = true;
                continue;
            }

            if (arg.StartsWith("--encrypt-key", StringComparison.OrdinalIgnoreCase))
            {
                Security.EnableEncryption = true;
                if (TryReadInlineValue(arg, out var inlineKey))
                    Security.EncryptionKeyBase64 = inlineKey;
                else if (TryReadNext(args, ref i, out var key))
                    Security.EncryptionKeyBase64 = key;

                continue;
            }

            if (arg.Equals("--keepalive", StringComparison.OrdinalIgnoreCase))
            {
                UseKeepAlive(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45));
                continue;
            }

            if (arg.StartsWith("--keepalive-interval", StringComparison.OrdinalIgnoreCase))
            {
                var current = _keepAlive.Enabled ? _keepAlive : CreateDefaultKeepAlive();

                if (TryReadInlineValue(arg, out var inlineInterval))
                    _keepAlive = CopyKeepAlive(current, ParseTimeSpan(inlineInterval, "--keepalive-interval"), current.Timeout);
                else if (TryReadNext(args, ref i, out var nextInterval))
                    _keepAlive = CopyKeepAlive(current, ParseTimeSpan(nextInterval, "--keepalive-interval"), current.Timeout);

                continue;
            }

            if (arg.StartsWith("--keepalive-timeout", StringComparison.OrdinalIgnoreCase))
            {
                var current = _keepAlive.Enabled ? _keepAlive : CreateDefaultKeepAlive();

                if (TryReadInlineValue(arg, out var inlineTimeout))
                    _keepAlive = CopyKeepAlive(current, current.Interval, ParseTimeSpan(inlineTimeout, "--keepalive-timeout"));
                else if (TryReadNext(args, ref i, out var nextTimeout))
                    _keepAlive = CopyKeepAlive(current, current.Interval, ParseTimeSpan(nextTimeout, "--keepalive-timeout"));

                continue;
            }
        }

        if (Port is null && positional.Count > 0 && int.TryParse(positional[0], out var positionalPort))
            UsePort(positionalPort);

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

    private static bool TryReadInlineValue(string arg, out string value)
    {
        var parts = arg.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            value = parts[1];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadNext(string[] args, ref int index, out string value)
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

    private static int ParseInt32(string value, string optionName)
    {
        if (!int.TryParse(value, out var result))
        {
            throw new InvalidOperationException(
                $"Option '{optionName}' expects an integer value, but received '{value}'.");
        }

        return result;
    }

    private static TimeSpan ParseTimeSpan(string value, string optionName)
    {
        if (TimeSpan.TryParse(value, out var timeSpan) && timeSpan > TimeSpan.Zero)
            return timeSpan;

        throw new InvalidOperationException(
            $"Option '{optionName}' expects a positive TimeSpan value, but received '{value}'.");
    }

    private static RpcKeepAliveOptions CopyKeepAlive(RpcKeepAliveOptions current, TimeSpan interval, TimeSpan timeout)
    {
        return new RpcKeepAliveOptions
        {
            Enabled = true,
            Interval = interval,
            Timeout = timeout,
            MeasureRtt = current.MeasureRtt
        };
    }

    private static RpcKeepAliveOptions CreateDefaultKeepAlive()
    {
        return new RpcKeepAliveOptions
        {
            Enabled = true,
            Interval = TimeSpan.FromSeconds(15),
            Timeout = TimeSpan.FromSeconds(45),
            MeasureRtt = false
        };
    }
}
