using System.Text;
using System.Text.Json;

namespace ULinkRPC.Starter;

internal static class Program
{
    private static readonly HttpClient Http = new();

    private enum TransportKind
    {
        Tcp,
        WebSocket,
        Kcp,
        Loopback
    }

    private enum SerializerKind
    {
        Json,
        MemoryPack
    }

    private static async Task<int> Main(string[] args)
    {
        if (!TryParseArgs(args, out var projectName, out var outputDir, out var transport, out var serializer, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        transport ??= PromptTransport();
        serializer ??= PromptSerializer();

        var rootPath = Path.GetFullPath(Path.Combine(outputDir, projectName));
        if (Directory.Exists(rootPath) && Directory.EnumerateFileSystemEntries(rootPath).Any())
        {
            Console.Error.WriteLine($"Target directory already exists and is not empty: {rootPath}");
            return 1;
        }

        Directory.CreateDirectory(rootPath);

        var versions = await ResolveVersionsAsync(transport.Value, serializer.Value);
        GenerateTemplate(rootPath, projectName, transport.Value, serializer.Value, versions);

        Console.WriteLine($"Created ULinkRPC starter template at: {rootPath}");
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1) cd \"{rootPath}\"");
        Console.WriteLine("  2) dotnet run --project \"Server/Server.csproj\"");
        Console.WriteLine("  3) Open \"Client\" with Unity 2022 LTS.");

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: ulinkrpc-starter [--name MyGame] [--output ./out] [--transport tcp|websocket|kcp|loopback] [--serializer json|memorypack]");
    }

    private static bool TryParseArgs(
        string[] args,
        out string projectName,
        out string outputDir,
        out TransportKind? transport,
        out SerializerKind? serializer,
        out string error)
    {
        projectName = "ULinkApp";
        outputDir = Directory.GetCurrentDirectory();
        transport = null;
        serializer = null;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--name" && i + 1 < args.Length)
            {
                projectName = args[++i];
                continue;
            }

            if (arg is "--output" && i + 1 < args.Length)
            {
                outputDir = args[++i];
                continue;
            }

            if (arg is "--transport" && i + 1 < args.Length)
            {
                if (!TryParseTransport(args[++i], out var parsed))
                {
                    error = "Invalid --transport value.";
                    return false;
                }

                transport = parsed;
                continue;
            }

            if (arg is "--serializer" && i + 1 < args.Length)
            {
                if (!TryParseSerializer(args[++i], out var parsed))
                {
                    error = "Invalid --serializer value.";
                    return false;
                }

                serializer = parsed;
                continue;
            }

            error = $"Unknown or incomplete option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            error = "--name cannot be empty.";
            return false;
        }

        return true;
    }

    private static bool TryParseTransport(string raw, out TransportKind transport)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "tcp": transport = TransportKind.Tcp; return true;
            case "websocket":
            case "ws": transport = TransportKind.WebSocket; return true;
            case "kcp": transport = TransportKind.Kcp; return true;
            case "loopback": transport = TransportKind.Loopback; return true;
            default: transport = default; return false;
        }
    }

    private static bool TryParseSerializer(string raw, out SerializerKind serializer)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "json": serializer = SerializerKind.Json; return true;
            case "memorypack": serializer = SerializerKind.MemoryPack; return true;
            default: serializer = default; return false;
        }
    }

    private static TransportKind PromptTransport()
    {
        Console.WriteLine("Select transport:");
        Console.WriteLine("  1) TCP");
        Console.WriteLine("  2) WebSocket");
        Console.WriteLine("  3) KCP");
        Console.WriteLine("  4) Loopback");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return TransportKind.Tcp;
                case "2": return TransportKind.WebSocket;
                case "3": return TransportKind.Kcp;
                case "4": return TransportKind.Loopback;
            }

            Console.WriteLine("Please enter 1-4.");
        }
    }

    private static SerializerKind PromptSerializer()
    {
        Console.WriteLine("Select serializer:");
        Console.WriteLine("  1) JSON");
        Console.WriteLine("  2) MemoryPack");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return SerializerKind.Json;
                case "2": return SerializerKind.MemoryPack;
            }

            Console.WriteLine("Please enter 1-2.");
        }
    }

    private static async Task<ResolvedVersions> ResolveVersionsAsync(TransportKind transport, SerializerKind serializer)
    {
        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);

        var serverVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Server");
        var clientVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Client");
        var transportVersion = await ResolveLatestStableVersionAsync(transportPackage);
        var serializerVersion = await ResolveLatestStableVersionAsync(serializerPackage);

        return new ResolvedVersions(serverVersion, clientVersion, transportVersion, serializerVersion);
    }

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

    private static string GetTransportPackage(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "ULinkRPC.Transport.Tcp",
        TransportKind.WebSocket => "ULinkRPC.Transport.WebSocket",
        TransportKind.Kcp => "ULinkRPC.Transport.Kcp",
        TransportKind.Loopback => "ULinkRPC.Transport.Loopback",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetSerializerPackage(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "ULinkRPC.Serializer.Json",
        SerializerKind.MemoryPack => "ULinkRPC.Serializer.MemoryPack",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static void GenerateTemplate(string rootPath, string projectName, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        const string sharedProjectName = "Shared";
        const string serverProjectName = "Server";
        const string clientProjectName = "Client";

        var sharedPath = Path.Combine(rootPath, sharedProjectName);
        var serverPath = Path.Combine(rootPath, serverProjectName);
        var clientPath = Path.Combine(rootPath, clientProjectName);

        Directory.CreateDirectory(sharedPath);
        Directory.CreateDirectory(serverPath);
        Directory.CreateDirectory(clientPath);

        var companyId = MakeCompanyId(projectName);

        GenerateShared(sharedPath, projectName, companyId);
        GenerateServer(serverPath, projectName, sharedProjectName, transport, serializer, versions);
        GenerateUnityClient(clientPath, sharedProjectName, companyId, transport, serializer, versions);
    }

    private static string MakeCompanyId(string projectName)
    {
        var filtered = new string(projectName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "ulinkrpc.sample" : $"ulinkrpc.{filtered.ToLowerInvariant()}";
    }

    private static void GenerateShared(string sharedPath, string rootName, string companyId)
    {
        var projectName = Path.GetFileName(sharedPath);
        var csproj = $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>9.0</LangVersion>
  </PropertyGroup>
</Project>
""";

        var contracts = $$"""
namespace {{rootName}}.Shared;

public sealed class PingRequest
{
    public string Message { get; set; } = string.Empty;
}

public sealed class PingReply
{
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ServerTime { get; set; }
}
""";

        var packageDir = Path.Combine(sharedPath, "UnityPackage");
        Directory.CreateDirectory(packageDir);

        var packageJson = $$"""
{
  "name": "com.{{companyId}}.shared",
  "version": "1.0.0",
  "displayName": "{{projectName}} Shared",
  "description": "Shared DTO and utility code for {{rootName}}",
  "unity": "2022.3",
  "author": {
    "name": "{{rootName}}"
  }
}
""";

        var asmdef = $$"""
{
  "name": "{{projectName}}",
  "rootNamespace": "{{rootName}}.Shared",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
""";

        WriteFile(Path.Combine(sharedPath, $"{projectName}.csproj"), csproj);
        WriteFile(Path.Combine(sharedPath, "UnityPackage", "SharedDtos.cs"), contracts);
        WriteFile(Path.Combine(sharedPath, "UnityPackage", $"{projectName}.asmdef"), asmdef);
        WriteFile(Path.Combine(sharedPath, "UnityPackage", "package.json"), packageJson);
    }

    private static void GenerateServer(
        string serverPath,
        string rootName,
        string sharedProjectName,
        TransportKind transport,
        SerializerKind serializer,
        ResolvedVersions versions)
    {
        var serverProjectName = Path.GetFileName(serverPath);
        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);

        var csproj = $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\{{sharedProjectName}}\{{sharedProjectName}}.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ULinkRPC.Server" Version="{{versions.Server}}" />
    <PackageReference Include="{{transportPackage}}" Version="{{versions.Transport}}" />
    <PackageReference Include="{{serializerPackage}}" Version="{{versions.Serializer}}" />
  </ItemGroup>
</Project>
""";

        var program = $$"""
using {{rootName}}.Shared;

Console.WriteLine("{{serverProjectName}} started.");
Console.WriteLine("Selected transport: {{transport}}");
Console.WriteLine("Selected serializer: {{serializer}}");

var demo = new PingReply
{
    Message = "ULinkRPC starter is ready.",
    ServerTime = DateTimeOffset.UtcNow
};

Console.WriteLine($"Demo shared DTO => {demo.Message} @ {demo.ServerTime:O}");
""";

        WriteFile(Path.Combine(serverPath, $"{serverProjectName}.csproj"), csproj);
        WriteFile(Path.Combine(serverPath, "Program.cs"), program);
    }

    private static void GenerateUnityClient(string clientPath, string sharedProjectName, string companyId, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        Directory.CreateDirectory(Path.Combine(clientPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Packages"));
        Directory.CreateDirectory(Path.Combine(clientPath, "ProjectSettings"));

        var manifest = $$"""
{
  "dependencies": {
    "com.github-glitchenzo.nugetforunity": "4.5.0",
    "com.unity.ide.rider": "3.0.39",
    "com.unity.ide.visualstudio": "2.0.23",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.ugui": "1.0.0",
    "com.{{companyId}}.shared": "file:../../{{sharedProjectName}}/UnityPackage"
  },
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.github-glitchenzo.nugetforunity"
      ]
    }
  ]
}
""";

        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);
        var packagesConfig = $$"""
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="ULinkRPC.Client" version="{{versions.Client}}" />
  <package id="{{transportPackage}}" version="{{versions.Transport}}" />
  <package id="{{serializerPackage}}" version="{{versions.Serializer}}" />
</packages>
""";

        var nugetConfig = """
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" enableCredentialProvider="false" />
  </packageSources>
  <config>
    <add key="packageInstallLocation" value="CustomWithinAssets" />
    <add key="repositoryPath" value="./Packages" />
    <add key="PackagesConfigDirectoryPath" value="." />
    <add key="slimRestore" value="true" />
    <add key="PreferNetStandardOverNetFramework" value="true" />
  </config>
</configuration>
""";

        var readme = $$"""
# Unity Client Starter (Unity 2022 LTS)

1. Open this folder with Unity 2022 LTS.
2. Wait for `NuGetForUnity` import.
3. In Unity: `NuGet -> Restore Packages` to install ULinkRPC latest packages.
4. Shared code is provided by local UPM package:
   - `com.{{companyId}}.shared` -> `../../{{sharedProjectName}}/UnityPackage`

Selected transport: {{transport}}
Selected serializer: {{serializer}}
""";

        var projectVersion = "m_EditorVersion: 2022.3.0f1\nm_EditorVersionWithRevision: 2022.3.0f1 (example)\n";

        WriteFile(Path.Combine(clientPath, "Packages", "manifest.json"), manifest);
        WriteFile(Path.Combine(clientPath, "Assets", "packages.config"), packagesConfig);
        WriteFile(Path.Combine(clientPath, "Assets", "NuGet.config"), nugetConfig);
        WriteFile(Path.Combine(clientPath, "README.md"), readme);
        WriteFile(Path.Combine(clientPath, "ProjectSettings", "ProjectVersion.txt"), projectVersion);
    }

    private static void WriteFile(string path, string content)
    {
        var normalized = content.Replace("\r\n", "\n").TrimStart('\ufeff');
        if (!normalized.EndsWith('\n'))
        {
            normalized += "\n";
        }

        File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private sealed record ResolvedVersions(string Server, string Client, string Transport, string Serializer);
}
