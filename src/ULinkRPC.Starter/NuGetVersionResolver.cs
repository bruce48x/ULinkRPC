namespace ULinkRPC.Starter;

internal static class NuGetVersionResolver
{
    public static ResolvedVersions ResolveVersions(TransportKind transport, SerializerKind serializer)
    {
        return new ResolvedVersions(
            StarterReleaseVersions.Core,
            StarterReleaseVersions.Server,
            StarterReleaseVersions.Client,
            GetTransportVersion(transport),
            GetSerializerVersion(serializer),
            StarterReleaseVersions.CodeGen,
            serializer is SerializerKind.MemoryPack ? StarterReleaseVersions.MemoryPackRuntime : null,
            serializer is SerializerKind.MemoryPack ? StarterReleaseVersions.MemoryPackRuntimeCore : null);
    }

    public static string GetTransportPackage(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "ULinkRPC.Transport.Tcp",
        TransportKind.WebSocket => "ULinkRPC.Transport.WebSocket",
        TransportKind.Kcp => "ULinkRPC.Transport.Kcp",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    public static string GetSerializerPackage(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "ULinkRPC.Serializer.Json",
        SerializerKind.MemoryPack => "ULinkRPC.Serializer.MemoryPack",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetTransportVersion(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => StarterReleaseVersions.TransportTcp,
        TransportKind.WebSocket => StarterReleaseVersions.TransportWebSocket,
        TransportKind.Kcp => StarterReleaseVersions.TransportKcp,
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetSerializerVersion(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => StarterReleaseVersions.SerializerJson,
        SerializerKind.MemoryPack => StarterReleaseVersions.SerializerMemoryPack,
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };
}

internal static class StarterReleaseVersions
{
    public const string Core = "0.11.1";
    public const string Server = "0.11.5";
    public const string Client = "0.11.0";
    public const string TransportTcp = "0.11.0";
    public const string TransportWebSocket = "0.11.1";
    public const string TransportKcp = "0.11.3";
    public const string SerializerJson = "0.11.0";
    public const string SerializerMemoryPack = "0.11.0";
    public const string CodeGen = "0.16.0";
    public const string MemoryPackRuntime = "1.21.4";
    public const string MemoryPackRuntimeCore = "1.21.4";
}
