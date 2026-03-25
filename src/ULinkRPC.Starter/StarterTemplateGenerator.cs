using System.Text;

namespace ULinkRPC.Starter;

internal enum TransportKind
{
    Tcp,
    WebSocket,
    Kcp
}

internal enum SerializerKind
{
    Json,
    MemoryPack
}

internal sealed record ResolvedVersions(
    string Core,
    string Server,
    string Client,
    string Transport,
    string Serializer,
    string CodeGen,
    string? SerializerRuntime,
    string? SerializerRuntimeCore);

internal static class UnityPackageVersions
{
    public const string SystemCollectionsImmutable = "6.0.0";
    public const string SystemRuntimeCompilerServicesUnsafe = "6.1.2";
    public const string SystemIoPipelines = "10.0.3";
}

internal sealed class StarterTemplateGenerator(Action<string, string> runDotNet, Action<string, string> runGit)
{
    public void GenerateTemplate(string rootPath, string projectName, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        const string sharedProjectName = "Shared";
        const string clientProjectName = "Client";

        var sharedPath = Path.Combine(rootPath, sharedProjectName);
        var serverPath = Path.Combine(rootPath, "Server");
        var serverAppPath = Path.Combine(serverPath, "Server");
        var clientPath = Path.Combine(rootPath, clientProjectName);

        Directory.CreateDirectory(sharedPath);
        Directory.CreateDirectory(serverPath);
        Directory.CreateDirectory(serverAppPath);
        Directory.CreateDirectory(clientPath);

        var companyId = MakeCompanyId(projectName);

        GenerateGitIgnore(rootPath);
        GenerateShared(sharedPath, companyId, versions);
        GenerateServer(serverAppPath, transport, serializer, versions);
        GenerateSolution(serverPath);
        GenerateUnityClient(clientPath, sharedProjectName, companyId, transport, serializer, versions);
        GenerateCodeGenToolManifest(rootPath, versions);
        RunCodeGen(sharedPath, serverAppPath, clientPath);
        InitializeGit(rootPath);
    }

    private void InitializeGit(string rootPath)
    {
        runGit(rootPath, "init");
    }

    private void GenerateSolution(string serverPath)
    {
        var solutionPath = Path.Combine(serverPath, "Server.slnx");
        runDotNet(serverPath, "new sln -n \"Server\" --format slnx");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"");
    }

    private static string MakeCompanyId(string projectName)
    {
        var filtered = new string(projectName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "ulinkrpc.sample" : $"ulinkrpc.{filtered.ToLowerInvariant()}";
    }

    private void GenerateCodeGenToolManifest(string rootPath, ResolvedVersions versions)
    {
        runDotNet(rootPath, "new tool-manifest");
        runDotNet(rootPath, $"tool install ULinkRPC.CodeGen --version {versions.CodeGen}");
    }

    private void RunCodeGen(string sharedPath, string serverAppPath, string clientPath)
    {
        runDotNet(
            serverAppPath,
            $"tool run ulinkrpc-codegen -- --contracts \"{sharedPath}\" --mode server --server-output \"Generated\" --server-namespace \"Server.Generated\"");

        runDotNet(
            clientPath,
            $"tool run ulinkrpc-codegen -- --contracts \"{sharedPath}\" --mode unity --output \"Assets{Path.DirectorySeparatorChar}Scripts{Path.DirectorySeparatorChar}Rpc{Path.DirectorySeparatorChar}RpcGenerated\" --namespace \"Client.Generated\"");
    }

    private static void GenerateShared(string sharedPath, string companyId, ResolvedVersions versions)
    {
        var projectName = Path.GetFileName(sharedPath);

        var directoryBuildProps = """
<Project>
  <PropertyGroup>
    <MSBuildProjectExtensionsPath>..\_artifacts\Shared\obj\</MSBuildProjectExtensionsPath>
    <BaseIntermediateOutputPath>..\_artifacts\Shared\obj\</BaseIntermediateOutputPath>
    <BaseOutputPath>..\_artifacts\Shared\bin\</BaseOutputPath>
  </PropertyGroup>
</Project>
""";

        // Shared code is consumed by Unity 2022, so generated source must stay within C# 9.0.
        var csproj = $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>9.0</LangVersion>
    <RootNamespace>Shared</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ULinkRPC.Core" Version="{{versions.Core}}" />
  </ItemGroup>
</Project>
""";

        var contracts = """
namespace Shared.Interfaces
{
    public sealed class PingRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PingReply
    {
        public string Message { get; set; } = string.Empty;
        public string ServerTimeUtc { get; set; } = string.Empty;
    }
}
""";

        var serviceContract = """
using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces
{
    [RpcService(1)]
    public interface IPingService
    {
        [RpcMethod(1)]
        ValueTask<PingReply> PingAsync(PingRequest request);
    }
}
""";

        var interfacesDir = Path.Combine(sharedPath, "Interfaces");
        Directory.CreateDirectory(interfacesDir);

        var packageJson = $$"""
{
  "name": "com.{{companyId}}.shared",
  "version": "1.0.0",
  "displayName": "{{projectName}} Shared",
  "description": "Shared DTO and utility code",
  "unity": "2022.3",
  "author": {
    "name": "Shared"
  }
}
""";

        var asmdef = """
{
  "name": "Shared",
  "rootNamespace": "Shared",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": [
    "ULinkRPC.Core.dll"
  ],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
""";

        WriteFile(Path.Combine(sharedPath, "Directory.Build.props"), directoryBuildProps);
        WriteFile(Path.Combine(sharedPath, $"{projectName}.csproj"), csproj);
        WriteFile(Path.Combine(sharedPath, "Interfaces", "SharedDtos.cs"), contracts);
        WriteFile(Path.Combine(sharedPath, "Interfaces", "IPingService.cs"), serviceContract);
        WriteFile(Path.Combine(sharedPath, $"{projectName}.asmdef"), asmdef);
        WriteFile(Path.Combine(sharedPath, "package.json"), packageJson);
    }

    private static void GenerateGitIgnore(string rootPath)
    {
        var gitIgnore = """
# OS / Editor
.DS_Store
Thumbs.db
.idea/
.vs/
*.suo
*.user
*.userprefs
*.DotSettings.user

# .NET build outputs
**/bin/
**/obj/
/_artifacts/

# Unity generated folders
/Client/[Ll]ibrary/
/Client/[Tt]emp/
/Client/[Ll]ogs/
/Client/[Uu]ser[Ss]ettings/
/Client/[Oo]bj/
/Client/[Bb]uild/
/Client/[Bb]uilds/
/Client/[Mm]emoryCaptures/
/Client/[Rr]ecordings/

# Unity generated project/IDE files
/Client/*.csproj
/Client/*.sln
/Client/*.slnx
/Client/*.unityproj
/Client/*.pidb
/Client/*.booproj
/Client/*.svd
/Client/*.pdb
/Client/*.mdb
/Client/*.opendb
/Client/*.VC.db

# NuGetForUnity restored packages
/Client/Assets/Packages/

# Logs
*.log
""";

        WriteFile(Path.Combine(rootPath, ".gitignore"), gitIgnore);
    }

    private static void GenerateServer(
        string serverPath,
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
    <RootNamespace>Server</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ULinkRPC.Server" Version="{{versions.Server}}" />
    <PackageReference Include="{{transportPackage}}" Version="{{versions.Transport}}" />
    <PackageReference Include="{{serializerPackage}}" Version="{{versions.Serializer}}" />
  </ItemGroup>
</Project>
""";

        var programUsings = GetServerProgramUsings(serializer, transport);
        var programBody = GetServerProgramBody(serializer, transport);
        var pingService = """
using Shared.Interfaces;

namespace Server.Services
{
    public sealed class PingService : IPingService
    {
        public ValueTask<PingReply> PingAsync(PingRequest request)
        {
            return ValueTask.FromResult(new PingReply
            {
                Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : "pong: " + request.Message,
                ServerTimeUtc = DateTime.UtcNow.ToString("O")
            });
        }
    }
}
""";

        var servicesPath = Path.Combine(serverPath, "Services");
        Directory.CreateDirectory(servicesPath);

        var program = $$"""
{{programUsings}}

{{programBody}}
""";

        WriteFile(Path.Combine(serverPath, $"{serverProjectName}.csproj"), csproj);
        WriteFile(Path.Combine(serverPath, "Program.cs"), program);
        WriteFile(Path.Combine(servicesPath, "PingService.cs"), pingService);
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
    "com.{{companyId}}.shared": "file:../../{{sharedProjectName}}"
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
  <package id="ULinkRPC.Core" version="{{versions.Core}}" />
  <package id="ULinkRPC.Client" version="{{versions.Client}}" manuallyInstalled="true" />
  <package id="{{transportPackage}}" version="{{versions.Transport}}" manuallyInstalled="true" />
  <package id="{{serializerPackage}}" version="{{versions.Serializer}}" manuallyInstalled="true" />
{{GetUnitySerializerDependencyPackages(serializer, versions)}}
</packages>
""";

        var nugetConfig = """
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" enableCredentialProvider="false" />
  </packageSources>
  <disabledPackageSources />
  <activePackageSource>
    <add key="All" value="(Aggregate source)" />
  </activePackageSource>
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
   - `com.{{companyId}}.shared` -> `../../{{sharedProjectName}}`

Selected transport: {{transport}}
Selected serializer: {{serializer}}
""";

        var projectVersion = "m_EditorVersion: 2022.3.62f3c1\nm_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)\n";

        WriteFile(Path.Combine(clientPath, "Packages", "manifest.json"), manifest);
        WriteFile(Path.Combine(clientPath, "Assets", "packages.config"), packagesConfig);
        WriteFile(Path.Combine(clientPath, "Assets", "NuGet.config"), nugetConfig);
        WriteFile(Path.Combine(clientPath, "README.md"), readme);
        WriteFile(Path.Combine(clientPath, "ProjectSettings", "ProjectVersion.txt"), projectVersion);
    }

    private static string GetTransportPackage(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "ULinkRPC.Transport.Tcp",
        TransportKind.WebSocket => "ULinkRPC.Transport.WebSocket",
        TransportKind.Kcp => "ULinkRPC.Transport.Kcp",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetSerializerPackage(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "ULinkRPC.Serializer.Json",
        SerializerKind.MemoryPack => "ULinkRPC.Serializer.MemoryPack",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetUnitySerializerDependencyPackages(SerializerKind serializer, ResolvedVersions versions) => serializer switch
    {
        SerializerKind.Json => string.Empty,
        SerializerKind.MemoryPack => BuildMemoryPackUnityDependencies(versions),
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string BuildMemoryPackUnityDependencies(ResolvedVersions versions)
    {
        if (string.IsNullOrWhiteSpace(versions.SerializerRuntime) || string.IsNullOrWhiteSpace(versions.SerializerRuntimeCore))
        {
            throw new InvalidOperationException("MemoryPack serializer requires explicit Unity package dependencies, but they were not resolved.");
        }

        return string.Join(
            Environment.NewLine,
            $"  <package id=\"MemoryPack\" version=\"{versions.SerializerRuntime}\" manuallyInstalled=\"true\" />",
            $"  <package id=\"MemoryPack.Core\" version=\"{versions.SerializerRuntimeCore}\" />",
            $"  <package id=\"System.Collections.Immutable\" version=\"{UnityPackageVersions.SystemCollectionsImmutable}\" />",
            $"  <package id=\"System.Runtime.CompilerServices.Unsafe\" version=\"{UnityPackageVersions.SystemRuntimeCompilerServicesUnsafe}\" />",
            $"  <package id=\"System.IO.Pipelines\" version=\"{UnityPackageVersions.SystemIoPipelines}\" />");
    }

    private static string GetServerSerializerSetup(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => ".UseJson()",
        SerializerKind.MemoryPack => ".UseMemoryPack()",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetServerTransportSetup(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => ".UseTcp(defaultPort: 20000)",
        TransportKind.WebSocket => ".UseWebSocket(defaultPort: 20000, path: \"/ws\")",
        TransportKind.Kcp => ".UseKcp(defaultPort: 20000)",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetServerProgramUsings(SerializerKind serializer, TransportKind transport)
    {
        var lines = new List<string>
        {
            "using ULinkRPC.Server;"
        };

        lines.Add(serializer switch
        {
            SerializerKind.Json => "using ULinkRPC.Serializer.Json;",
            SerializerKind.MemoryPack => "using ULinkRPC.Serializer.MemoryPack;",
            _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
        });

        lines.Add(transport switch
        {
            TransportKind.Tcp => "using ULinkRPC.Transport.Tcp;",
            TransportKind.WebSocket => "using ULinkRPC.Transport.WebSocket;",
            TransportKind.Kcp => "using ULinkRPC.Transport.Kcp;",
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        });

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetServerProgramBody(SerializerKind serializer, TransportKind transport)
    {
        var serializerSetup = GetServerSerializerSetup(serializer);
        var transportSetup = GetServerTransportSetup(transport);
        return $$"""
var commandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();

await RpcServerHostBuilder.Create()
    .UseCommandLine(commandLineArgs)
    {{serializerSetup}}
    {{transportSetup}}
    .RunAsync();
""";
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
}
