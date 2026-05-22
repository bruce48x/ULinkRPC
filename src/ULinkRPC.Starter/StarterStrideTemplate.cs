namespace ULinkRPC.Starter;

internal static class StarterStrideTemplate
{
    public static void Generate(StarterTemplateContext context)
    {
        EnsureClientDirectories(context.Paths.ClientPath);

        var gameProjectPath = Path.Combine(context.Paths.ClientPath, "Client");
        var windowsProjectPath = Path.Combine(context.Paths.ClientPath, "Client.Windows");

        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Client.sln"), BuildSolution());
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "README.md"), BuildReadme(context));
        StarterFileWriter.Write(Path.Combine(gameProjectPath, "Client.csproj"), BuildGameProject(context));
        StarterFileWriter.Write(Path.Combine(gameProjectPath, "Client.sdpkg"), BuildStridePackage("Client", includeEffects: true));
        StarterFileWriter.Write(Path.Combine(gameProjectPath, "Assets", "GameSettings.sdgamesettings"), BuildGameSettings());
        StarterFileWriter.Write(Path.Combine(gameProjectPath, "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"), BuildTesterScript(context));
        StarterFileWriter.Write(Path.Combine(windowsProjectPath, "Client.Windows.csproj"), BuildWindowsProject());
        StarterFileWriter.Write(Path.Combine(windowsProjectPath, "Client.Windows.sdpkg"), BuildStridePackage("Client.Windows", includeEffects: false));
        StarterFileWriter.Write(Path.Combine(windowsProjectPath, "Program.cs"), BuildProgram());
    }

    private static void EnsureClientDirectories(string clientPath)
    {
        var gameProjectPath = Path.Combine(clientPath, "Client");
        var windowsProjectPath = Path.Combine(clientPath, "Client.Windows");

        Directory.CreateDirectory(Path.Combine(gameProjectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(gameProjectPath, "Effects"));
        Directory.CreateDirectory(Path.Combine(gameProjectPath, "Resources"));
        Directory.CreateDirectory(Path.Combine(gameProjectPath, "Scripts", "Rpc", "Generated"));
        Directory.CreateDirectory(Path.Combine(gameProjectPath, "Scripts", "Rpc", "Testing"));
        Directory.CreateDirectory(Path.Combine(windowsProjectPath, "Assets"));
        Directory.CreateDirectory(Path.Combine(windowsProjectPath, "Resources"));
    }

    private static string BuildSolution() => """
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 16
VisualStudioVersion = 16.0.0.0
MinimumVisualStudioVersion = 16.0.0.0
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Client.Windows", "Client.Windows\Client.Windows.csproj", "{FA0CB295-B1D1-4C10-B7EF-3426C186432B}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Client", "Client\Client.csproj", "{6752B930-1A1A-465B-9B09-4E804BA69C2B}"
EndProject
Global
EndGlobal
""";

    private static string BuildGameProject(StarterTemplateContext context)
    {
        var packageReferences = RenderPackageReferences(StarterDependencyPlanner.Create(context, StarterProjectRole.StrideClient));

        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-windows</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Client</RootNamespace>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\Shared.csproj" />
{{packageReferences}}
  </ItemGroup>

{{StarterCodeGenHookTemplates.RenderStrideClientTargets()}}
</Project>
""";
    }

    private static string BuildWindowsProject() => """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <RootNamespace>Client</RootNamespace>
    <OutputPath>..\Bin\Windows\$(Configuration)\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <DisableFastUpToDateCheck>true</DisableFastUpToDateCheck>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Client\Client.csproj" />
  </ItemGroup>
</Project>
""";

    private static string RenderPackageReferences(StarterDependencyPlan plan) =>
        string.Join(Environment.NewLine, plan.PackageReferences.Select(RenderPackageReference));

    private static string RenderPackageReference(StarterPackageReference reference)
    {
        if (reference.PrivateAssets is null && reference.IncludeAssets is null)
        {
            return $"    <PackageReference Include=\"{reference.Id}\" Version=\"{reference.Version}\" />";
        }

        var metadata = new List<string>();
        if (reference.PrivateAssets is not null)
        {
            metadata.Add($"      <PrivateAssets>{reference.PrivateAssets}</PrivateAssets>");
        }

        if (reference.IncludeAssets is not null)
        {
            metadata.Add($"      <IncludeAssets>{reference.IncludeAssets}</IncludeAssets>");
        }

        return string.Join(
            Environment.NewLine,
            $"    <PackageReference Include=\"{reference.Id}\" Version=\"{reference.Version}\">",
            string.Join(Environment.NewLine, metadata),
            "    </PackageReference>");
    }

    private static string BuildStridePackage(string name, bool includeEffects)
    {
        var effectFolders = includeEffects ? "    -   Path: !dir Effects\n" : string.Empty;
        return $$"""
!Package
SerializedVersion: {Assets: 3.1.0.0}
Meta:
    Name: {{name}}
    Version: 1.0.0
    Authors: []
    Owners: []
    Dependencies: null
AssetFolders:
    -   Path: !dir Assets
{{effectFolders}}ResourceFolders:
    - !dir Resources
OutputGroupDirectories: {}
ExplicitFolders: []
Bundles: []
TemplateFolders: []
RootAssets: []
""";
    }

    private static string BuildGameSettings() => """
!GameSettingsAsset
Id: 5b22d130-0f4d-4b44-b3bc-52dcb040c5cc
SerializedVersion: {Stride: 3.1.0.1}
Tags: []
Defaults:
    - !Stride.Audio.AudioEngineSettings,Stride.Audio
        HrtfSupport: false
    - !Stride.Assets.EditorSettings,Stride.Assets
        RenderingMode: HDR
    - !Stride.Graphics.RenderingSettings,Stride.Graphics
        DefaultBackBufferWidth: 1280
        DefaultBackBufferHeight: 720
        AdaptBackBufferToScreen: false
        DefaultGraphicsProfile: Level_10_0
        ColorSpace: Linear
        DisplayOrientation: LandscapeRight
    - !Stride.Streaming.StreamingSettings,Stride.Rendering
        ManagerUpdatesInterval: 0:00:00:00.0330000
        ResourceLiveTimeout: 0:00:00:08.0000000
    - !Stride.Assets.Textures.TextureSettings,Stride.Assets
        TextureQuality: Fast
Overrides: []
PlatformFilters: []
SplashScreenColor: {R: 0, G: 0, B: 0, A: 255}
""";

    private static string BuildReadme(StarterTemplateContext context) => $$"""
# Stride3D Client Starter (Stride 4.3)

1. Install .NET 10 SDK and the Stride 4.3 prerequisites.
2. Open `Client.sln` from Stride Launcher / Game Studio, or restore it with `dotnet restore Client.sln`.
3. Start the server from the project root: `dotnet run --project Server/Server/Server.csproj`.
4. Run this client: `dotnet run --project Client.Windows/Client.Windows.csproj`.

The generated client uses the standard Stride solution layout with a game project, a Windows launcher project, and a minimal RPC ping startup hook.

Selected transport: {{context.Transport}}
Selected serializer: {{context.Serializer}}
""";

    private static string BuildProgram() => """
using System.Threading.Tasks;
using Client.Rpc.Testing;
using Stride.Engine;

using var game = new RpcStarterGame();
game.Run();

internal sealed class RpcStarterGame : Game
{
    private readonly RpcConnectionTester _tester = new();

    protected override async Task LoadContent()
    {
        await base.LoadContent();
        _ = Task.Run(_tester.ConnectAndPingAsync);
    }

    protected override void Destroy()
    {
        try
        {
            _tester.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            base.Destroy();
        }
    }
}
""";

    private static string BuildTesterScript(StarterTemplateContext context)
    {
        var transportUsing = context.Transport switch
        {
            TransportKind.Tcp => "using ULinkRPC.Transport.Tcp;",
            TransportKind.WebSocket => "using ULinkRPC.Transport.WebSocket;",
            TransportKind.Kcp => "using ULinkRPC.Transport.Kcp;",
            _ => throw new ArgumentOutOfRangeException()
        };

        var serializerUsing = context.Serializer switch
        {
            SerializerKind.Json => "using ULinkRPC.Serializer.Json;",
            SerializerKind.MemoryPack => "using ULinkRPC.Serializer.MemoryPack;",
            _ => throw new ArgumentOutOfRangeException()
        };

        var transportConstruction = context.Transport switch
        {
            TransportKind.Tcp => "new TcpTransport(_host, _port)",
            TransportKind.WebSocket => "new WsTransport($\"ws://{_host}:{_port}{NormalizePath(_path)}\")",
            TransportKind.Kcp => "new KcpTransport(_host, _port)",
            _ => throw new ArgumentOutOfRangeException()
        };

        var serializerConstruction = context.Serializer switch
        {
            SerializerKind.Json => "new JsonRpcSerializer()",
            SerializerKind.MemoryPack => "new MemoryPackRpcSerializer()",
            _ => throw new ArgumentOutOfRangeException()
        };

        var defaultPath = context.Transport == TransportKind.WebSocket ? "/ws" : string.Empty;

        return $$"""
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Rpc.Generated;
using Shared.Interfaces;
using ULinkRPC.Client;
using ULinkRPC.Core;
{{transportUsing}}
{{serializerUsing}}

namespace Client.Rpc.Testing;

public sealed class RpcConnectionTester : IAsyncDisposable
{
    private readonly string _host = "127.0.0.1";
    private readonly int _port = 20000;
    private readonly string _path = "{{defaultPath}}";
    private readonly string _message = "hello";
    private readonly CancellationTokenSource _cts = new();

    private RpcClient? _client;
    private bool _isDisposed;

    public async Task ConnectAndPingAsync()
    {
        if (_isDisposed || _client is not null)
            return;

        Console.WriteLine($"Connecting to {DescribeEndpoint()}");

        try
        {
            _client = new RpcClient(new RpcClientOptions(
                {{transportConstruction}},
                {{serializerConstruction}})
                .UseSecurity(ConfigureTransportSecurity));

            await _client.ConnectAsync(_cts.Token).ConfigureAwait(false);

            var reply = await _client.Api.Shared.Ping.PingAsync(new PingRequest
            {
                Message = _message
            }).ConfigureAwait(false);

            Console.WriteLine($"Ping ok: message={reply.Message}, serverTimeUtc={reply.ServerTimeUtc}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Connect failed: {ex}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _cts.Cancel();

        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }

        _cts.Dispose();
    }

    private string DescribeEndpoint()
    {
        var path = NormalizePath(_path);
        return string.IsNullOrEmpty(path) ? $"{_host}:{_port}" : $"{_host}:{_port}{path}";
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private static void ConfigureTransportSecurity(TransportSecurityConfig security)
    {
        security.EnableCompression = false;
        security.CompressionThresholdBytes = 1024;
        security.EnableEncryption = false;
        security.EncryptionKeyBase64 = null;
    }
}
""";
    }
}
