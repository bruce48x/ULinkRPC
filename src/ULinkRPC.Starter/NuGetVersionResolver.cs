using System.Text.Json;
using System.Xml.Linq;

namespace ULinkRPC.Starter;

internal static class NuGetVersionResolver
{
    private static readonly HttpClient Http = new();

    public static async Task<ResolvedVersions> ResolveVersionsAsync(TransportKind transport, SerializerKind serializer)
    {
        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);

        var coreVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Core");
        var serverVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Server");
        var clientVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Client");
        var transportVersion = await ResolveLatestStableVersionAsync(transportPackage);
        var serializerVersion = await ResolveLatestStableVersionAsync(serializerPackage);
        var codeGenVersion = await ResolveLatestStableVersionAsync("ULinkRPC.CodeGen");
        var serializerRuntime = default(string);
        var serializerRuntimeCore = default(string);

        if (serializer is SerializerKind.MemoryPack)
        {
            serializerRuntime = await ResolveDependencyVersionAsync(serializerPackage, serializerVersion, ".NETStandard2.1", "MemoryPack");
            serializerRuntimeCore = await ResolveDependencyVersionAsync("MemoryPack", serializerRuntime, ".NETStandard2.1", "MemoryPack.Core");
        }

        return new ResolvedVersions(
            coreVersion,
            serverVersion,
            clientVersion,
            transportVersion,
            serializerVersion,
            codeGenVersion,
            serializerRuntime,
            serializerRuntimeCore);
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

    private static async Task<string> ResolveLatestStableVersionAsync(string packageId)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
        using var stream = await Http.GetStreamAsync(url);
        using var doc = await JsonDocument.ParseAsync(stream);
        var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
            .Select(v => v.GetString())
            .Where(v => !string.IsNullOrWhiteSpace(v) && !v!.Contains('-', StringComparison.Ordinal))
            .ToList();

        if (versions.Count == 0)
        {
            throw new InvalidOperationException($"No stable NuGet versions found for package '{packageId}'.");
        }

        return versions[^1]!;
    }

    private static async Task<string> ResolveDependencyVersionAsync(string packageId, string packageVersion, string targetFramework, string dependencyId)
    {
        var nuspec = await LoadNuSpecAsync(packageId, packageVersion);
        var dependency = nuspec
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "group" &&
                string.Equals((string?)element.Attribute("targetFramework"), targetFramework, StringComparison.OrdinalIgnoreCase))
            ?.Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == "dependency" &&
                string.Equals((string?)element.Attribute("id"), dependencyId, StringComparison.OrdinalIgnoreCase));

        var version = (string?)dependency?.Attribute("version");
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException(
                $"Unable to resolve dependency '{dependencyId}' from package '{packageId}' {packageVersion} for target framework '{targetFramework}'.");
        }

        return version;
    }

    private static async Task<XDocument> LoadNuSpecAsync(string packageId, string packageVersion)
    {
        var lowerId = packageId.ToLowerInvariant();
        var lowerVersion = packageVersion.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{lowerId}/{lowerVersion}/{lowerId}.nuspec";
        using var stream = await Http.GetStreamAsync(url);
        return XDocument.Load(stream);
    }
}
