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
    public const string Kcp = "2.7.0";
    public const string MicrosoftBclAsyncInterfaces = "10.0.2";
    public const string SystemBuffers = "4.6.1";
    public const string SystemCollectionsImmutable = "6.0.0";
    public const string SystemIoPipelinesForJson = "10.0.2";
    public const string SystemMemoryForJson = "4.6.3";
    public const string SystemMemoryForKcp = "4.5.4";
    public const string SystemTextEncodingsWeb = "10.0.2";
    public const string SystemTextJson = "10.0.2";
    public const string SystemThreadingTasksExtensionsForJson = "4.6.3";
    public const string SystemThreadingTasksExtensionsForKcp = "4.5.4";
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
        Directory.CreateDirectory(Path.Combine(clientPath, "Assets", "Scenes"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Assets", "Scripts", "Rpc", "Testing"));

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
{{GetUnityTransportDependencyPackages(transport)}}
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
5. Open `Assets/Scenes/{{GetUnitySceneName(transport)}}.unity` and press Play to run the default connection example.

Selected transport: {{transport}}
Selected serializer: {{serializer}}
""";

        var projectVersion = "m_EditorVersion: 2022.3.62f3c1\nm_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)\n";
        var testerScriptPath = Path.Combine(clientPath, "Assets", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs");
        var scenePath = Path.Combine(clientPath, "Assets", "Scenes", $"{GetUnitySceneName(transport)}.unity");

        WriteFile(Path.Combine(clientPath, "Packages", "manifest.json"), manifest);
        WriteFile(Path.Combine(clientPath, "Assets", "packages.config"), packagesConfig);
        WriteFile(Path.Combine(clientPath, "Assets", "NuGet.config"), nugetConfig);
        WriteFile(testerScriptPath, GetUnityTesterScript(transport, serializer));
        WriteFile(Path.Combine(clientPath, "Assets", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs.meta"), GetUnityTesterScriptMeta());
        WriteFile(scenePath, GetUnitySceneContent(transport));
        WriteFile(Path.Combine(clientPath, "Assets", "Scenes", $"{GetUnitySceneName(transport)}.unity.meta"), GetUnitySceneMeta());
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

    private static string GetUnityTransportDependencyPackages(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => string.Empty,
        TransportKind.WebSocket => string.Empty,
        TransportKind.Kcp => string.Join(
            Environment.NewLine,
            $"  <package id=\"Kcp\" version=\"{UnityPackageVersions.Kcp}\" />",
            $"  <package id=\"System.Memory\" version=\"{UnityPackageVersions.SystemMemoryForKcp}\" />",
            $"  <package id=\"System.Threading.Tasks.Extensions\" version=\"{UnityPackageVersions.SystemThreadingTasksExtensionsForKcp}\" />"),
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetUnitySerializerDependencyPackages(SerializerKind serializer, ResolvedVersions versions) => serializer switch
    {
        SerializerKind.Json => string.Join(
            Environment.NewLine,
            $"  <package id=\"Microsoft.Bcl.AsyncInterfaces\" version=\"{UnityPackageVersions.MicrosoftBclAsyncInterfaces}\" />",
            $"  <package id=\"System.IO.Pipelines\" version=\"{UnityPackageVersions.SystemIoPipelinesForJson}\" />",
            $"  <package id=\"System.Text.Encodings.Web\" version=\"{UnityPackageVersions.SystemTextEncodingsWeb}\" />",
            $"  <package id=\"System.Buffers\" version=\"{UnityPackageVersions.SystemBuffers}\" />",
            $"  <package id=\"System.Memory\" version=\"{UnityPackageVersions.SystemMemoryForJson}\" />",
            $"  <package id=\"System.Runtime.CompilerServices.Unsafe\" version=\"{UnityPackageVersions.SystemRuntimeCompilerServicesUnsafe}\" />",
            $"  <package id=\"System.Threading.Tasks.Extensions\" version=\"{UnityPackageVersions.SystemThreadingTasksExtensionsForJson}\" />",
            $"  <package id=\"System.Text.Json\" version=\"{UnityPackageVersions.SystemTextJson}\" />"),
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

    private static string GetServerSerializerConstruction(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "new JsonRpcSerializer()",
        SerializerKind.MemoryPack => "new MemoryPackRpcSerializer()",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetServerTransportConstruction(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(20000)));",
        TransportKind.WebSocket => "builder.UseAcceptor(ct => WsConnectionAcceptor.CreateAsync(builder.ResolvePort(20000), \"/ws\", ct));",
        TransportKind.Kcp => "builder.UseAcceptor(new KcpConnectionAcceptor(builder.ResolvePort(20000)));",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetServerProgramUsings(SerializerKind serializer, TransportKind transport)
    {
        var lines = new List<string>
        {
            "using ULinkRPC.Core;",
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
        var serializerSetup = GetServerSerializerConstruction(serializer);
        var transportSetup = GetServerTransportConstruction(transport);
        return $$"""
var commandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(commandLineArgs)
    .UseSerializer({{serializerSetup}});

{{transportSetup}}

await builder.RunAsync();
""";
    }

    private static string GetUnitySceneName(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "TcpConnectionTest",
        TransportKind.WebSocket => "WebSocketConnectionTest",
        TransportKind.Kcp => "KcpConnectionTest",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetUnityTransportUsing(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "using ULinkRPC.Transport.Tcp;",
        TransportKind.WebSocket => "using ULinkRPC.Transport.WebSocket;",
        TransportKind.Kcp => "using ULinkRPC.Transport.Kcp;",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetUnitySerializerUsing(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "using ULinkRPC.Serializer.Json;",
        SerializerKind.MemoryPack => "using ULinkRPC.Serializer.MemoryPack;",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetUnityEndpointFactory(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "        public static RpcEndpointSettings CreateDefault() => new RpcEndpointSettings { Host = \"127.0.0.1\", Port = 20000, Path = string.Empty };",
        TransportKind.WebSocket => "        public static RpcEndpointSettings CreateDefault() => new RpcEndpointSettings { Host = \"127.0.0.1\", Port = 20000, Path = \"/ws\" };",
        TransportKind.Kcp => "        public static RpcEndpointSettings CreateDefault() => new RpcEndpointSettings { Host = \"127.0.0.1\", Port = 20000, Path = string.Empty };",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetUnityTransportConstruction(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "new TcpTransport(_endpoint.Host, _endpoint.Port)",
        TransportKind.WebSocket => "new WsTransport($\"ws://{_endpoint.Host}:{_endpoint.Port}{NormalizePath(_endpoint.Path)}\")",
        TransportKind.Kcp => "new KcpTransport(_endpoint.Host, _endpoint.Port)",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetUnitySerializerConstruction(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "new JsonRpcSerializer()",
        SerializerKind.MemoryPack => "new MemoryPackRpcSerializer()",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetUnityTransportLabel(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "TCP",
        TransportKind.WebSocket => "WebSocket",
        TransportKind.Kcp => "KCP",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetUnityTesterScript(TransportKind transport, SerializerKind serializer)
    {
        var transportLabel = GetUnityTransportLabel(transport);
        var transportUsing = GetUnityTransportUsing(transport);
        var serializerUsing = GetUnitySerializerUsing(serializer);
        var endpointFactory = GetUnityEndpointFactory(transport);
        var transportConstruction = GetUnityTransportConstruction(transport);
        var serializerConstruction = GetUnitySerializerConstruction(serializer);

        return $$"""
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Client.Generated;
using Shared.Interfaces;
using ULinkRPC.Client;
{{transportUsing}}
{{serializerUsing}}
using UnityEngine;

namespace Rpc.Testing
{
    [Serializable]
    public sealed class RpcEndpointSettings
    {
        public string Host = "127.0.0.1";
        public int Port = 20000;
        public string Path = string.Empty;

{{endpointFactory}}
    }

    public sealed class RpcConnectionTester : MonoBehaviour
    {
        [SerializeField] private RpcEndpointSettings _endpoint = RpcEndpointSettings.CreateDefault();

        public string Message = "hello";
        public bool AutoConnect = true;

        private readonly CancellationTokenSource _cts = new();
        private RpcClient? _client;
        private bool _isShuttingDown;

        private async void Start()
        {
            if (!AutoConnect)
                return;

            await ConnectAndPingAsync();
        }

        private void OnDestroy()
        {
            _ = ShutdownAsync();
        }

        [ContextMenu("Connect And Ping")]
        public async Task ConnectAndPingAsync()
        {
            if (_isShuttingDown || _client is not null)
                return;

            Debug.Log($"[{{transportLabel}}] Connecting to {DescribeEndpoint()}");

            try
            {
                _client = new RpcClient(
                    new RpcClientOptions(
                        {{transportConstruction}},
                        {{serializerConstruction}}));

                await _client.ConnectAsync(_cts.Token);

                var reply = await _client.Api.Shared.Ping.PingAsync(new PingRequest
                {
                    Message = Message
                }, _cts.Token);

                Debug.Log($"[{{transportLabel}}] Ping ok: message={reply.Message}, serverTimeUtc={reply.ServerTimeUtc}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{{transportLabel}}] Connect failed: {ex}");
                await ShutdownAsync();
            }
        }

        private string DescribeEndpoint()
        {
            var path = NormalizePath(_endpoint.Path);
            return string.IsNullOrEmpty(path)
                ? $"{_endpoint.Host}:{_endpoint.Port}"
                : $"{_endpoint.Host}:{_endpoint.Port}{path}";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        }

        private async Task ShutdownAsync()
        {
            if (_isShuttingDown)
                return;

            _isShuttingDown = true;
            _cts.Cancel();

            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            _cts.Dispose();
        }
    }
}
""";
    }

    private static string GetUnityTesterScriptMeta() => """
fileFormatVersion: 2
guid: 8fbb7dbe54784d7995143ce24cf85121
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""";

    private static string GetUnitySceneMeta() => """
fileFormatVersion: 2
guid: d4d2d5faafe942e58a33f4a41e3b7cf2
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""";

    private static string GetUnitySceneContent(TransportKind transport)
    {
        var label = GetUnityTransportLabel(transport);
        var pathValue = transport == TransportKind.WebSocket ? "/ws" : string.Empty;
        return $$"""
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 2}
  - component: {fileID: 4}
  m_Layer: 0
  m_Name: RpcConnectionTester
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &2
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &4
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 8fbb7dbe54784d7995143ce24cf85121, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  _endpoint:
    Host: 127.0.0.1
    Port: 20000
    Path: {{pathValue}}
  Message: hello
  AutoConnect: 1
--- !u!29 &5
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &6
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &7
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 1
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 5
    m_PVRFilteringGaussRadiusAO: 2
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &8
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
--- !u!1 &256380733
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 256380735}
  - component: {fileID: 256380734}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!20 &256380734
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 256380733}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 1
  m_BackGroundColor: {r: 0.19215687, g: 0.3019608, b: 0.4745098, a: 0}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {x: 2, y: 11}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 0
  orthographic size: 5
  m_Depth: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!4 &256380735
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 256380733}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: -10}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 2}
  - {fileID: 256380735}
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
