using ULinkRPC.Client;

namespace ULinkRPC.Client.Unity;

public enum RpcUnityTransportKind
{
    Tcp,
    WebSocket,
    Kcp
}

public enum RpcUnitySerializerKind
{
    MemoryPack,
    Json
}

public sealed class RpcUnityClientOptions
{
    private RpcUnityClientOptions(
        RpcUnityTransportKind transport,
        RpcUnitySerializerKind serializer,
        string? host,
        int port,
        string? webSocketUrl)
    {
        Transport = transport;
        Serializer = serializer;
        Host = host;
        Port = port;
        WebSocketUrl = webSocketUrl;
    }

    public string? Host { get; }

    public int Port { get; }

    public RpcUnitySerializerKind Serializer { get; }

    public RpcUnityTransportKind Transport { get; }

    public string? WebSocketUrl { get; }

    public static RpcUnityClientOptions Tcp(string host, int port, RpcUnitySerializerKind serializer = RpcUnitySerializerKind.MemoryPack)
    {
        return new RpcUnityClientOptions(RpcUnityTransportKind.Tcp, serializer, host, port, webSocketUrl: null);
    }

    public static RpcUnityClientOptions WebSocket(string url, RpcUnitySerializerKind serializer = RpcUnitySerializerKind.Json)
    {
        return new RpcUnityClientOptions(RpcUnityTransportKind.WebSocket, serializer, host: null, port: 0, url);
    }

    public static RpcUnityClientOptions Kcp(string host, int port, RpcUnitySerializerKind serializer = RpcUnitySerializerKind.MemoryPack)
    {
        return new RpcUnityClientOptions(RpcUnityTransportKind.Kcp, serializer, host, port, webSocketUrl: null);
    }

    public static RpcUnityClientOptions MemoryPackTcp(string host, int port) =>
        Tcp(host, port, RpcUnitySerializerKind.MemoryPack);

    public static RpcUnityClientOptions MemoryPackWebSocket(string url) =>
        WebSocket(url, RpcUnitySerializerKind.MemoryPack);

    public static RpcUnityClientOptions MemoryPackKcp(string host, int port) =>
        Kcp(host, port, RpcUnitySerializerKind.MemoryPack);

    public static RpcUnityClientOptions JsonTcp(string host, int port) =>
        Tcp(host, port, RpcUnitySerializerKind.Json);

    public static RpcUnityClientOptions JsonWebSocket(string url) =>
        WebSocket(url, RpcUnitySerializerKind.Json);

    public static RpcUnityClientOptions JsonKcp(string host, int port) =>
        Kcp(host, port, RpcUnitySerializerKind.Json);

    public RpcClientBuilder CreateBuilder()
    {
        var builder = RpcClientBuilder.Create();

        switch (Serializer)
        {
            case RpcUnitySerializerKind.MemoryPack:
                builder.UseMemoryPack();
                break;
            case RpcUnitySerializerKind.Json:
                builder.UseJson();
                break;
            default:
                throw new InvalidOperationException($"Unsupported serializer kind: {Serializer}.");
        }

        switch (Transport)
        {
            case RpcUnityTransportKind.Tcp:
                builder.UseTcp(RequireHost(), RequirePort());
                break;
            case RpcUnityTransportKind.WebSocket:
                builder.UseWebSocket(RequireWebSocketUrl());
                break;
            case RpcUnityTransportKind.Kcp:
                builder.UseKcp(RequireHost(), RequirePort());
                break;
            default:
                throw new InvalidOperationException($"Unsupported transport kind: {Transport}.");
        }

        return builder;
    }

    private string RequireHost()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("Host is required for the selected transport.");

        return Host;
    }

    private int RequirePort()
    {
        if (Port <= 0)
            throw new InvalidOperationException("Port must be greater than 0 for the selected transport.");

        return Port;
    }

    private string RequireWebSocketUrl()
    {
        if (string.IsNullOrWhiteSpace(WebSocketUrl))
            throw new InvalidOperationException("WebSocketUrl is required for the selected transport.");

        return WebSocketUrl;
    }
}
